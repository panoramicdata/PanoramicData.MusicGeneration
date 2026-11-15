namespace PanoramicData.MusicGeneration;

public static partial class SongGenerator
{
	private readonly record struct BeatChoices(int BassDegree, bool Bass, bool Kick, bool Snare, bool HiHat);

	private static void PlayBar(ISongRenderer mixer, Pcg32 noise, Pcg32 rng, SongStyle style, ArrangementContext context, double t)
	{
		var degree = rng.Next(0, context.Scale.Count);
		var inversion = rng.Next(0, 3);
		PlayPad(mixer, style, context, degree, inversion, t);
		var fifth = context.Scale.Count >= 7 ? 4 : 3;
		for (var q = 0; q < 4; q++)
		{
			var at = t + (q * context.Beat);
			var choices = ChooseBeat(rng, degree, fifth, q);
			if (style.Bass && choices.Bass)
			{
				mixer.Tone(VoiceKind.Bass, context.Scale.MidiOf(context.Info.RootMidi, choices.BassDegree), at, context.Beat * 1.6, new Timbre(0.22, 3, 0.5, 0, 6, 1.0 / 120));
			}
			PlayDrums(mixer, noise, style, context, choices, at);
		}
	}

	private static void PlayPad(ISongRenderer mixer, SongStyle style, ArrangementContext context, int degree, int inversion, double t)
	{
		if (!style.Pad)
		{
			return;
		}
		var step = 0;
		foreach (var chordStep in new[] { 0, 2, 4 })
		{
			var lifted = step++ < inversion ? context.Scale.Count : 0;
			var midi = context.Scale.MidiOf(context.Info.RootMidi + (12 * style.PadOctaveOffset), degree + chordStep + lifted);
			while (midi >= style.MelodyLowMidi)
			{
				midi -= 12;
			}
			mixer.Tone(VoiceKind.Pad, midi, t, context.Bar, new Timbre(style.PadLevel, 2, context.Brightness * 0.25, 0, 0.3, style.PadAttackSeconds));
		}
	}

	private static BeatChoices ChooseBeat(Pcg32 rng, int degree, int fifth, int q)
	{
		var bass = rng.NextDouble() < 0.65;
		var bassDegree = rng.NextDouble() < 0.7 ? degree : degree + fifth;
		var kick = rng.NextDouble() < (q is 0 or 2 ? 0.8 : 0.25);
		var snare = q is 1 or 3 && rng.NextDouble() < 0.85;
		var hiHat = rng.NextDouble() < 0.7;
		return new BeatChoices(bassDegree, bass, kick, snare, hiHat);
	}

	private static void PlayDrums(ISongRenderer mixer, Pcg32 noise, SongStyle style, ArrangementContext context, BeatChoices choices, double at)
	{
		if (!style.Drums)
		{
			return;
		}
		if (choices.Kick && style.Kick)
		{
			mixer.Kick(at, context.KickBase);
		}
		if (choices.Snare)
		{
			mixer.Noise(VoiceKind.Snare, 38, at, 0.12, 0.20, noise, style.Snare);
		}
		mixer.Noise(VoiceKind.HiHat, 42, at, 0.04, 0.07, noise, style.HiHat);
		if (choices.HiHat)
		{
			mixer.Noise(VoiceKind.HiHat, 42, at + (context.Beat / 2), 0.04, 0.05, noise, style.HiHat);
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
		var cursor = lead;
		while (cursor < seconds)
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
}
