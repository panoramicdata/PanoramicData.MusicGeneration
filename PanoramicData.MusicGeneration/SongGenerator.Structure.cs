namespace PanoramicData.MusicGeneration;

public static partial class SongGenerator
{
	private interface ISongRenderer
	{
		void Tone(VoiceKind voice, int midi, double start, double duration, Timbre timbre);
		void Kick(double start, double baseHz);
		void Noise(VoiceKind voice, int nominalMidi, double start, double seconds, double amplitude, Pcg32 rng, bool enabled);
	}

	private sealed record ArrangementContext(SongInfo Info, ScaleDefinition Scale, double Brightness, double VibratoHz, double KickBase, SongStyle Style)
	{
		public double Beat => 60.0 / Info.TempoBpm;
		public double Bar => 4 * Beat;
	}

	private sealed record PlannedSound(NoteEvent Note, Timbre Timbre, uint NoiseSeed, double NoiseLevel);

	private sealed class ScoreBuilder(double referenceHz) : ISongRenderer
	{
		public List<PlannedSound> Sounds { get; } = [];

		public void Tone(VoiceKind voice, int midi, double start, double duration, Timbre timbre) =>
			Sounds.Add(new PlannedSound(new NoteEvent(voice, start, duration, midi, Pitch.FrequencyOf(midi, referenceHz)), timbre, 0, 0));

		public void Kick(double start, double baseHz) =>
			Sounds.Add(new PlannedSound(new NoteEvent(VoiceKind.Kick, start, 0.18, 36, baseHz), default, 0, 0));

		public void Noise(VoiceKind voice, int nominalMidi, double start, double seconds, double amplitude, Pcg32 rng, bool enabled)
		{
			var seed = rng.Next();
			if (enabled)
			{
				Sounds.Add(new PlannedSound(new NoteEvent(voice, start, seconds, nominalMidi, 0), default, seed, amplitude));
			}
		}
	}

	private static void PlayForm(Mixer mixer, ArrangementContext context, SongForm form, IReadOnlyList<SectionEvent> sections)
	{
		var material = new Dictionary<string, IReadOnlyList<PlannedSound>>(StringComparer.Ordinal);
		foreach (var placement in sections)
		{
			var section = form.Sections[placement.Index];
			var seed = MaterialSeed(context.Info.Seed, section.MaterialId);
			if (!material.TryGetValue(section.MaterialId, out var sounds))
			{
				sounds = ComposeMaterial(context, placement.PhraseBars, seed);
				material.Add(section.MaterialId, sounds);
			}
			mixer.BeginSection(placement);
			RenderMaterial(mixer, context, section, placement, sounds, seed);
			PlayPickup(mixer, context, section, placement, sounds);
			if (placement.HasTransitionFill)
			{
				PlayFill(mixer, context, section, placement, seed);
			}
			if (placement.HasCadence)
			{
				PlayCadence(mixer, context, section, placement);
			}
		}
	}

	private static List<PlannedSound> ComposeMaterial(ArrangementContext context, int bars, long seed)
	{
		var score = new ScoreBuilder(context.Info.ReferenceHz);
		var allVoices = context.Style with { Pad = true, Bass = true, Melody = true, Drums = true, Kick = true, Snare = true, HiHat = true };
		var noise = new Pcg32(seed, 2);
		for (var bar = 0; bar < bars; bar++)
		{
			PlayBar(score, noise, new Pcg32(seed, 1000L + bar), allVoices, context, bar * context.Bar);
		}
		PlayMelody(score, new Pcg32(seed, 1), allVoices, context, 0, bars * context.Bar);
		return score.Sounds;
	}

	private static long MaterialSeed(int seed, string id)
	{
		var hash = 14695981039346656037UL;
		foreach (var character in id)
		{
			hash = unchecked((hash ^ character) * 1099511628211UL);
		}
		return unchecked((long)hash ^ seed);
	}

	private static bool IsEnabled(SongStyle style, SongSection section, VoiceKind voice) =>
		(section.Voices is null || section.Voices.Contains(voice)) && voice switch
		{
			VoiceKind.Pad => style.Pad,
			VoiceKind.Bass => style.Bass,
			VoiceKind.Melody => style.Melody,
			VoiceKind.Kick => style.Drums && style.Kick,
			VoiceKind.Snare => style.Drums && style.Snare,
			VoiceKind.HiHat => style.Drums && style.HiHat,
			_ => false
		};
}
