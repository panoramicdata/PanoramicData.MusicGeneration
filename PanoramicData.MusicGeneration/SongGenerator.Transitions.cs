namespace PanoramicData.MusicGeneration;

public static partial class SongGenerator
{
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
