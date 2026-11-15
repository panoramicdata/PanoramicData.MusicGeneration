namespace PanoramicData.MusicGeneration;

public static partial class SongGenerator
{
	/// <summary>Accumulates voices into one buffer and records what was played.</summary>
	private sealed class Mixer(int sampleRate, int length, double referenceHz, SongStyle style, bool captureStems) : ISongRenderer
	{
		private double Sin(double x) => style.UsePlatformSine ? Math.Sin(x) : DeterministicMath.Sin(x);

		private int? _sectionIndex;
		private double? _sectionEnd;

		public void BeginSection(SectionEvent section)
		{
			_sectionIndex = section.Index;
			_sectionEnd = section.EndSeconds;
		}

		private readonly double[] _mix = new double[length];
		private readonly List<NoteEvent> _notes = [];
		private readonly Dictionary<VoiceKind, double[]> _stems = captureStems ? Enum.GetValues<VoiceKind>().ToDictionary(v => v, _ => new double[length]) : [];

		private void Add(VoiceKind voice, int index, double sample)
		{
			_mix[index] += sample;
			if (captureStems)
			{
				_stems[voice][index] += sample;
			}
		}

		public void Tone(VoiceKind voice, int midi, double start, double duration, Timbre timbre)
		{
			var frequency = Pitch.FrequencyOf(midi, referenceHz);
			_notes.Add(new NoteEvent(voice, start, duration, midi, frequency) { SectionIndex = _sectionIndex });
			var first = (int)(start * sampleRate);
			var count = Math.Min((int)(duration * sampleRate), _mix.Length - first);
			var phaseOffset = 0.0;
			for (var i = 0; i < count; i++)
			{
				var t = (double)i / sampleRate;
				var attack = Math.Min(1, t / timbre.AttackSeconds);
				var release = Math.Min(1, Math.Max(0, (duration - t) * 40));
				var envelope = Math.Exp(-timbre.Decay * t / duration) * attack * release;
				var modulation = VibratoFraction(t, duration, timbre.VibratoHz, style, style.UsePlatformSine);
				var f = frequency * (1 + modulation);
				var sample = 0.0;
				var weight = 1.0;
				for (var h = 1; h <= timbre.Harmonics; h++)
				{
					if (f * h < (sampleRate / 2.0) - 200)
					{
						sample += Sin((2 * Math.PI * frequency * h * ((double)(first + i) / sampleRate)) + (h * phaseOffset)) * weight;
					}

					weight *= timbre.Rolloff;
				}

				Add(voice, first + i, timbre.Level * envelope * sample);
				phaseOffset += 2 * Math.PI * frequency * modulation / sampleRate;
			}
		}

		public void Kick(double start, double baseHz)
		{
			var duration = _sectionEnd is { } end ? Math.Min(0.18, end - start) : 0.18;
			_notes.Add(new NoteEvent(VoiceKind.Kick, start, duration, 36, baseHz) { SectionIndex = _sectionIndex });
			var first = (int)(start * sampleRate);
			if (first >= _mix.Length)
			{
				return;
			}

			var count = Math.Min((int)(duration * sampleRate), _mix.Length - first);
			var phase = 0.0;
			for (var i = 0; i < count; i++)
			{
				var t = (double)i / sampleRate;
				phase += 2 * Math.PI * (baseHz + (110 * Math.Exp(-t * 28))) / sampleRate;
				Add(VoiceKind.Kick, first + i, 0.5 * Math.Exp(-t * 14) * Sin(phase));
			}
		}

		public void Noise(VoiceKind voice, int nominalMidi, double start, double seconds, double amplitude, Pcg32 rng, bool enabled)
		{
			if (enabled)
			{
				_notes.Add(new NoteEvent(voice, start, seconds, nominalMidi, 0) { SectionIndex = _sectionIndex });
			}
			var first = (int)(start * sampleRate);
			if (first >= _mix.Length)
			{
				return;
			}

			var count = Math.Min((int)(seconds * sampleRate), _mix.Length - first);
			var previous = 0.0;
			for (var i = 0; i < count; i++)
			{
				var white = (rng.NextDouble() * 2) - 1;
				if (enabled)
				{
					Add(voice, first + i, amplitude * (white - previous) * Math.Exp(-6.0 * i / count));
				}
				previous = white;
			}
		}

		public GeneratedSong Finish(SongInfo info)
		{
			var peak = 0.0;
			foreach (var v in _mix)
			{
				peak = Math.Max(peak, Math.Abs(v));
			}

			var scale = peak > 0 ? Peak / peak : 1;
			var samples = new float[_mix.Length];
			for (var i = 0; i < samples.Length; i++)
			{
				samples[i] = (float)(_mix[i] * scale);
			}

			return new GeneratedSong(info, samples, sampleRate, [.. _notes.OrderBy(n => n.StartSeconds)]) { Stems = _stems, NormalisationGain = scale };
		}
	}
}
