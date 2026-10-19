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

	private static void RenderMaterial(Mixer mixer, ArrangementContext context, SongSection section, SectionEvent placement, IReadOnlyList<PlannedSound> sounds, long seed)
	{
		var templateLength = placement.PhraseBars * context.Bar;
		var cycles = (placement.Bars + placement.PhraseBars - 1) / placement.PhraseBars;
		for (var cycle = 0; cycle < cycles; cycle++)
		{
			for (var index = 0; index < sounds.Count; index++)
			{
				var sound = sounds[index];
				var at = placement.StartSeconds + (cycle * templateLength) + sound.Note.StartSeconds;
				if (!ShouldRender(context, section, placement, sound.Note, at)
					|| (sound.Note.Voice == VoiceKind.Melody && new Pcg32(seed, 9000L + index).NextDouble() >= section.MelodyDensity))
				{
					continue;
				}
				var bodyEnd = placement.HasCadence ? placement.PlannedEndSeconds - context.Bar : placement.EndSeconds;
				var duration = Math.Min(sound.Note.DurationSeconds, bodyEnd - at);
				RenderSound(mixer, context, section, sound, at, duration);
			}
		}
	}

	private static bool ShouldRender(ArrangementContext context, SongSection section, SectionEvent placement, NoteEvent note, double at)
	{
		if (at >= placement.EndSeconds || !IsEnabled(context.Style, section, note.Voice))
		{
			return false;
		}
		if (placement.HasCadence && at >= placement.PlannedEndSeconds - context.Bar)
		{
			return false;
		}
		return !placement.HasTransitionFill || note.Voice is VoiceKind.Pad or VoiceKind.Bass or VoiceKind.Melody
			|| at < placement.PlannedEndSeconds - context.Beat;
	}

	private static void RenderSound(Mixer mixer, ArrangementContext context, SongSection section, PlannedSound sound, double at, double duration)
	{
		var note = sound.Note;
		switch (note.Voice)
		{
			case VoiceKind.Kick:
				mixer.Kick(at, note.FrequencyHz);
				break;
			case VoiceKind.Snare:
			case VoiceKind.HiHat:
				mixer.Noise(note.Voice, note.MidiNote, at, duration, sound.NoiseLevel, new Pcg32(sound.NoiseSeed, 2), true);
				break;
			default:
				var midi = note.Voice == VoiceKind.Melody ? MelodyPitch(note.MidiNote, section, context.Style) : note.MidiNote;
				mixer.Tone(note.Voice, midi, at, duration, sound.Timbre);
				break;
		}
	}

	private static int MelodyPitch(int midi, SongSection section, SongStyle style)
	{
		midi += 12 * section.MelodyOctaveOffset;
		while (midi > style.MelodyHighMidi)
		{
			midi -= 12;
		}
		while (midi < style.MelodyLowMidi)
		{
			midi += 12;
		}
		return midi;
	}

	private static void PlayPickup(Mixer mixer, ArrangementContext context, SongSection section, SectionEvent placement, IReadOnlyList<PlannedSound> sounds)
	{
		if (placement.PickupStartSeconds >= placement.StartSeconds || section.MelodyDensity == 0 || !IsEnabled(context.Style, section, VoiceKind.Melody))
		{
			return;
		}
		var first = sounds.FirstOrDefault(s => s.Note.Voice == VoiceKind.Melody);
		if (first is not null)
		{
			mixer.Tone(VoiceKind.Melody, MelodyPitch(first.Note.MidiNote, section, context.Style), placement.PickupStartSeconds,
				placement.StartSeconds - placement.PickupStartSeconds, first.Timbre);
		}
	}

	private static void PlayFill(Mixer mixer, ArrangementContext context, SongSection section, SectionEvent placement, long seed)
	{
		if (!IsEnabled(context.Style, section, VoiceKind.Snare))
		{
			return;
		}
		var noise = new Pcg32(seed, 7000);
		for (var hit = 0; hit < 4; hit++)
		{
			var at = placement.PlannedEndSeconds - context.Beat + (hit * context.Beat / 4);
			mixer.Noise(VoiceKind.Snare, 38, at, Math.Min(0.08, context.Beat / 4), 0.12, noise, true);
		}
	}

	private static void PlayCadence(Mixer mixer, ArrangementContext context, SongSection section, SectionEvent placement)
	{
		var at = placement.PlannedEndSeconds - context.Bar;
		var duration = context.Bar * 0.9;
		var style = context.Style;
		if (IsEnabled(style, section, VoiceKind.Pad))
		{
			foreach (var degree in new[] { 0, 2, 4 })
			{
				var midi = context.Scale.MidiOf(context.Info.RootMidi + (12 * style.PadOctaveOffset), degree);
				while (midi >= style.MelodyLowMidi)
				{
					midi -= 12;
				}
				mixer.Tone(VoiceKind.Pad, midi, at, duration, new Timbre(style.PadLevel, 2, context.Brightness * 0.25, 0, 0.3, style.PadAttackSeconds));
			}
		}
		if (IsEnabled(style, section, VoiceKind.Bass))
		{
			mixer.Tone(VoiceKind.Bass, context.Info.RootMidi, at, duration, new Timbre(0.22, 3, 0.5, 0, 6, 1.0 / 120));
		}
		if (section.MelodyDensity > 0 && IsEnabled(style, section, VoiceKind.Melody))
		{
			var tonic = style.MelodyLowMidi + ((((context.Info.RootMidi - style.MelodyLowMidi) % 12) + 12) % 12);
			mixer.Tone(VoiceKind.Melody, MelodyPitch(tonic, section, style), at, duration, new Timbre(0.20, 6, context.Brightness, context.VibratoHz, 5, 1.0 / 120));
		}
		if (IsEnabled(style, section, VoiceKind.Kick))
		{
			mixer.Kick(at, context.KickBase);
		}
	}
}
