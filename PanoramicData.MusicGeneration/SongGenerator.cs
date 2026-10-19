namespace PanoramicData.MusicGeneration;

/// <summary>Generates reproducible seeded songs, with optional recurring sections and an inspectable note list.</summary>
public static partial class SongGenerator
{
	/// <summary>
	/// The version of the generation algorithm. The same seed gives the same song only within one version: any change that alters the notes or the samples
	/// for an existing seed is a breaking change and increments this number.
	/// </summary>
	public const int Version = 2;

	private const double Peak = 0.9;

	/// <summary>Generates a song with the default style.</summary>
	/// <param name="seed">Chooses tempo, key, scale, harmony, melody, drum patterns, timbre and tuning.</param>
	/// <param name="seconds">Length of the song.</param>
	/// <param name="sampleRate">Output sample rate in hertz.</param>
	/// <returns>The song.</returns>
	public static GeneratedSong Generate(int seed, double seconds, int sampleRate) =>
		Generate(seed, seconds, sampleRate, new SongStyle());

	/// <summary>Generates a song.</summary>
	/// <param name="seed">Chooses tempo, key, scale, harmony, melody, drum patterns, timbre and tuning.</param>
	/// <param name="seconds">Length of the song, up to an hour.</param>
	/// <param name="sampleRate">Output sample rate in hertz, 8,000 to 96,000.</param>
	/// <param name="style">What may vary from song to song.</param>
	/// <returns>The song.</returns>
	/// <exception cref="ArgumentException">The style is inconsistent.</exception>
	/// <exception cref="ArgumentOutOfRangeException">The length or sample rate is out of range.</exception>
	public static GeneratedSong Generate(int seed, double seconds, int sampleRate, SongStyle style) => GenerateCore(seed, seconds, sampleRate, style, false);

	/// <summary>Generates the song and unnormalised per-voice buffers without changing the mix.</summary>
	public static GeneratedSong GenerateStems(int seed, double seconds, int sampleRate, SongStyle style) => GenerateCore(seed, seconds, sampleRate, style, true);

	/// <summary>Generates the song and stems with the default style.</summary>
	public static GeneratedSong GenerateStems(int seed, double seconds, int sampleRate) => GenerateStems(seed, seconds, sampleRate, new SongStyle());

	private static GeneratedSong GenerateCore(int seed, double seconds, int sampleRate, SongStyle style, bool captureStems)
	{
		ArgumentNullException.ThrowIfNull(style);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(seconds, 0);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(seconds, 3600);
		ArgumentOutOfRangeException.ThrowIfLessThan(sampleRate, 8000);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(sampleRate, 96000);
		style.Validate();

		var choice = new Pcg32(seed, 0);
		var tempo = choice.Next(style.MinTempoBpm, style.MaxTempoBpm + 1);
		var scale = style.Scales[choice.Next(0, style.Scales.Count)];
		var root = choice.Next(style.RootMidiLow, style.RootMidiHigh + 1);
		var brightness = 0.35 + (choice.NextDouble() * 0.5);
		var vibratoHz = style.VibratoMinHz + (choice.NextDouble() * (style.VibratoMaxHz - style.VibratoMinHz));
		var detune = choice.Next(-style.MaxDetuneCents, style.MaxDetuneCents + 1);
		var kickBase = 42 + (choice.NextDouble() * 24);
		var beat = 60.0 / tempo;
		var bar = 4 * beat;
		var lead = choice.NextDouble() * bar;
		var info = new SongInfo(seed, tempo, scale.Name, root, detune, lead);

		var mixer = new Mixer(sampleRate, (int)(seconds * sampleRate), info.ReferenceHz, style, captureStems);
		var context = new ArrangementContext(info, scale, brightness, vibratoHz, kickBase, style);
		if (style.Form is { } form)
		{
			var sections = SectionPlanner.Place(form, info, seconds);
			PlayForm(mixer, context, form, sections);
			return mixer.Finish(info) with { Sections = sections };
		}
		var noise = new Pcg32(seed, 2);

		var bars = (int)Math.Ceiling((seconds - lead) / bar);
		for (var b = 0; b < bars; b++)
		{
			PlayBar(mixer, noise, new Pcg32(seed, 1000L + b), style, context, lead + (b * bar));
		}

		if (style.Melody)
		{
			PlayMelody(mixer, new Pcg32(seed, 1), style, context, lead, seconds);
		}

		return mixer.Finish(info);
	}

	private static void PlayBar(ISongRenderer mixer, Pcg32 noise, Pcg32 rng, SongStyle style, ArrangementContext context, double t)
	{
		var scale = context.Scale;
		var root = context.Info.RootMidi;
		var brightness = context.Brightness;
		var kickBase = context.KickBase;
		var beat = context.Beat;
		var bar = 4 * beat;
		var degree = rng.Next(0, scale.Count);
		var inversion = rng.Next(0, 3);
		if (style.Pad)
		{
			var step = 0;
			foreach (var chordStep in new[] { 0, 2, 4 })
			{
				var lifted = step++ < inversion ? scale.Count : 0;
				var midi = scale.MidiOf(root + (12 * style.PadOctaveOffset), degree + chordStep + lifted);
				while (midi >= style.MelodyLowMidi)
				{
					midi -= 12;
				}

				mixer.Tone(VoiceKind.Pad, midi, t, bar, new Timbre(style.PadLevel, 2, brightness * 0.25, 0, 0.3, style.PadAttackSeconds));
			}
		}

		var fifth = scale.Count >= 7 ? 4 : 3;
		for (var q = 0; q < 4; q++)
		{
			var at = t + (q * beat);
			var playBass = rng.NextDouble() < 0.65;
			var bassDegree = rng.NextDouble() < 0.7 ? degree : degree + fifth;
			var playKick = rng.NextDouble() < (q is 0 or 2 ? 0.8 : 0.25);
			var playSnare = q is 1 or 3 && rng.NextDouble() < 0.85;
			var playHat = rng.NextDouble() < 0.7;
			if (style.Bass && playBass)
			{
				mixer.Tone(VoiceKind.Bass, scale.MidiOf(root, bassDegree), at, beat * 1.6, new Timbre(0.22, 3, 0.5, 0, 6, 1.0 / 120));
			}

			if (!style.Drums)
			{
				continue;
			}

			if (playKick && style.Kick)
			{
				mixer.Kick(at, kickBase);
			}

			if (playSnare)
			{
				mixer.Noise(VoiceKind.Snare, 38, at, 0.12, 0.20, noise, style.Snare);
			}

			mixer.Noise(VoiceKind.HiHat, 42, at, 0.04, 0.07, noise, style.HiHat);
			if (playHat)
			{
				mixer.Noise(VoiceKind.HiHat, 42, at + (beat / 2), 0.04, 0.05, noise, style.HiHat);
			}
		}
	}

	private static void PlayMelody(ISongRenderer mixer, Pcg32 rng, SongStyle style, ArrangementContext context, double lead, double seconds)
	{
		var scale = context.Scale;
		var root = context.Info.RootMidi;
		var brightness = context.Brightness;
		var vibratoHz = context.VibratoHz;
		var beat = context.Beat;
		// The lowest note of the key at or above the bottom of the melody range; degrees count up from there.
		var low = style.MelodyLowMidi + ((((root - style.MelodyLowMidi) % 12) + 12) % 12);
		var top = 0;
		while (scale.MidiOf(low, top + 1) <= style.MelodyHighMidi)
		{
			top++;
		}

		var degree = top / 2;
		for (var cursor = lead; cursor < seconds;)
		{
			degree = Math.Clamp(degree + rng.Next(-3, 4), 0, top);
			var length = rng.Next(0, 3) switch { 0 => 0.5, 1 => 1.0, _ => 1.5 } * beat;
			if (rng.NextDouble() < 0.85)
			{
				mixer.Tone(VoiceKind.Melody, scale.MidiOf(low, degree), cursor, length * 1.1, new Timbre(0.20, 6, brightness, vibratoHz, 5, 1.0 / 120));
			}

			cursor += length;
		}
	}

	internal static double VibratoFraction(double time, double duration, double rate, SongStyle style, bool platformSine)
	{
		if (duration < 1.0 / 3 || rate <= 0 || style.VibratoDepth == 0)
		{
			return 0;
		}

		var phase = 2 * Math.PI * rate * time;
		return style.VibratoDepth * Math.Clamp((time - style.VibratoDelaySeconds) / style.VibratoRampSeconds, 0, 1)
			* (platformSine ? Math.Sin(phase) : DeterministicMath.Sin(phase));
	}

	private readonly record struct Timbre(double Level, int Harmonics, double Rolloff, double VibratoHz, double Decay, double AttackSeconds);

	/// <summary>Accumulates voices into one buffer and records what was played.</summary>
	private sealed class Mixer(int sampleRate, int length, double referenceHz, SongStyle style, bool captureStems) : ISongRenderer
	{
		private double Sin(double x) => style.UsePlatformSine ? Math.Sin(x) : DeterministicMath.Sin(x);

		public int? SectionIndex { get; set; }
		public double? SectionEnd { get; set; }

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
			_notes.Add(new NoteEvent(voice, start, duration, midi, frequency) { SectionIndex = SectionIndex });
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
			var duration = SectionEnd is { } end ? Math.Min(0.18, end - start) : 0.18;
			_notes.Add(new NoteEvent(VoiceKind.Kick, start, duration, 36, baseHz) { SectionIndex = SectionIndex });
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
				_notes.Add(new NoteEvent(voice, start, seconds, nominalMidi, 0) { SectionIndex = SectionIndex });
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
