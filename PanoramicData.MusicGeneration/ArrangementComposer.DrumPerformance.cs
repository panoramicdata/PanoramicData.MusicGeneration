namespace PanoramicData.MusicGeneration;

public static partial class ArrangementComposer
{
	private static void DrumHit(BarContext bar, List<NoteEvent> notes, int midi, double at, double duration, int accent)
	{
		var count = notes.Count;
		Add(bar, notes, midi, at, duration, accent + DrumAccent(bar, at));
		if (notes.Count == count) { return; }
		var note = notes[^1];
		var start = Math.Clamp(note.StartSeconds + DrumTiming(bar, at), bar.Section.StartSeconds, bar.Section.EndSeconds);
		notes[^1] = note with { StartSeconds = start, DurationSeconds = Math.Min(note.DurationSeconds, bar.Section.EndSeconds - start) };
	}

	private static int DrumAccent(BarContext bar, double at)
	{
		var accent = bar.Layer.Pattern switch
		{
			LayerPattern.Kick => at == 0 ? 5 : -3,
			LayerPattern.Snare => IsBeat(at) ? 8 : -4,
			_ => IsBeat(at) ? 3 : -5
		};
		var phrase = 6.0 * (bar.LocalBar % 4) / 3;
		var touch = DrumRandom(bar, 2300 + (int)Math.Round(at * 48)).Next(-4, 5);
		return (int)Math.Round(bar.Settings.DrumHumanity * (accent + phrase + touch));
	}

	private static double DrumTiming(BarContext bar, double at)
	{
		if (at == 0 || bar.Settings.DrumHumanity == 0) { return 0; }
		if (bar.Layer.Pattern == LayerPattern.Kick && IsBeat(at)) { return 0; }
		var role = bar.Layer.Pattern == LayerPattern.Snare ? 0.012 : -0.006;
		var shape = 0.003 * Math.Sin(Math.PI * ((bar.LocalBar % 2) * bar.BeatsPerBar + at) / (bar.BeatsPerBar * 2));
		return bar.Settings.DrumHumanity * (role + shape);
	}
}
