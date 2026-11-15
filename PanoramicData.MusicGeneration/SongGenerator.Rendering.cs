namespace PanoramicData.MusicGeneration;

public static partial class SongGenerator
{
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
}
