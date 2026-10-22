namespace PanoramicData.MusicGeneration;

public static partial class ArrangementComposer
{
	private sealed record DrumFill(double Length, int Shape);
	private static readonly double[][] FillPositions =
	[
		[0, 0.25, 0.5, 0.75],
		[0, 0.125, 0.25, 0.375, 0.5, 0.625, 0.75, 0.875],
		[0, 1.0 / 6, 2.0 / 6, 3.0 / 6, 4.0 / 6, 5.0 / 6],
		[0, 0.25, 0.625, 0.75, 0.875],
		[0, 0.5],
		[0, 1.0 / 3, 2.0 / 3]
	];

	private static DrumFill DrumFillPlan(BarContext bar)
	{
		if (!bar.HasFillPlayer || bar.Section.Kind == SectionKind.Outro || bar.Settings.DrumFills <= 0) { return new(0, 0); }
		var ending = bar.LocalBar == bar.Section.Bars - 1;
		var boundary = (bar.LocalBar + 1) % 4 == 0;
		if (!ending && !boundary) { return new(0, 0); }
		var random = DrumRandom(bar, 2200);
		var probability = bar.Settings.DrumFills * (ending ? 1 : 0.35);
		if (random.NextDouble() >= probability) { return new(0, 0); }
		return SelectDrumFill(bar.Settings.DrumFills, random);
	}

	private static DrumFill SelectDrumFill(double intensity, Pcg32 random)
	{
		if (intensity < 0.33) { return new(0.5, 4); }
		if (intensity < 0.55) { return new(1, random.Next(0, 2) == 0 ? 0 : 5); }
		return new(2, random.Next(0, 4));
	}

	private static void WriteDrumFill(BarContext bar, List<NoteEvent> notes, DrumFill fill)
	{
		if (fill.Length <= 0 || bar.Layer.Pattern != LayerPattern.Snare) { return; }
		var positions = FillPositions[fill.Shape];
		for (var i = 0; i < positions.Length; i++)
		{
			var at = bar.BeatsPerBar - fill.Length + positions[i] * fill.Length;
			var key = DrumFillKey(bar, fill.Shape, i, positions.Length);
			var lift = -20 + (int)Math.Round(22.0 * i / (positions.Length - 1));
			DrumHit(bar, notes, key, at, 0.1, lift, straight: true);
		}
	}

	private static int DrumFillKey(BarContext bar, int shape, int index, int count)
	{
		if (bar.Settings.Beat == MusicBeat.BossaNova) { return 37; }
		if (shape is 0 or 4 || index < count / 3) { return 38; }
		int[] toms = [50, 47, 45, 43];
		return toms[Math.Min(toms.Length - 1, index * toms.Length / count)];
	}
}
