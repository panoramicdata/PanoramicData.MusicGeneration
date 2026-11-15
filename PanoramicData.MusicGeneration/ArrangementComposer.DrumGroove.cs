namespace PanoramicData.MusicGeneration;

public static partial class ArrangementComposer
{
	private static bool FillPlayerAvailable(ArrangementSettings settings, List<LayerParticipation> participation, int section) =>
		settings.Layers.Where(l => l.Pattern == LayerPattern.Snare && l.Enabled && l.Gain > 0)
			.Any(l => participation.Any(p => p.LayerId == l.Id && p.SectionIndex == section && p.Present));

	private static Pcg32 DrumRandom(BarContext bar, int stream) =>
		new(bar.Settings.Seed, stream + bar.Section.Index * 1009L + bar.LocalBar * 37L);

	private static void ExpressiveDrums(BarContext bar, List<NoteEvent> notes)
	{
		var fill = DrumFillPlan(bar);
		var key = DrumKey(bar);
		foreach (var at in DrumStarts(bar.Settings.Beat, bar.Layer.Pattern))
		{
			if (DrumRest(bar, at, fill)) { continue; }
			DrumHit(bar, notes, DrumArticulation(bar, key, at), at, 0.18, IsBeat(at) ? 0 : -16);
		}
		DrumDecorations(bar, notes, key, fill);
		WriteDrumFill(bar, notes, fill);
		if (bar.Settings.DrumFills <= 0) { TransitionFill(bar, notes, key); }
	}

	private static int DrumKey(BarContext bar) => bar.Layer.Pattern switch
	{
		LayerPattern.Kick => 36,
		LayerPattern.Snare => bar.Settings.Beat == MusicBeat.BossaNova ? 37 : 38,
		_ => 42
	};

	private static bool DrumRest(BarContext bar, double at, DrumFill fill)
	{
		if (fill.Length > 0 && at >= bar.BeatsPerBar - fill.Length)
		{
			return bar.Layer.Pattern != LayerPattern.Kick || at > bar.BeatsPerBar - fill.Length;
		}
		if (bar.Layer.Pattern != LayerPattern.HiHat || !NearBeat(at, 3.5)) { return false; }
		return (bar.LocalBar & 1) != 0 && DrumRandom(bar, 2100).NextDouble() < bar.Settings.DrumVariation * 0.2;
	}
}
