namespace PanoramicData.MusicGeneration;

public static partial class ArrangementComposer
{
	private static int DrumArticulation(BarContext bar, int key, double at)
	{
		if (bar.Layer.Pattern != LayerPattern.HiHat) { return key; }
		if (LandingCrash(bar, at)) { return 49; }
		if (RideCymbal(bar)) { return 51; }
		return OpenHat(bar, at) ? 46 : key;
	}

	private static bool LandingCrash(BarContext bar, double at) => NearBeat(at, 0) && bar.LocalBar == 0
		&& bar.Section.Kind == SectionKind.Chorus && DrumRandom(bar, 2101).NextDouble() < bar.Settings.DrumFills;

	private static bool RideCymbal(BarContext bar) => bar.Settings.Beat == MusicBeat.Swing
		&& bar.Section.Kind == SectionKind.Chorus && DrumRandom(bar, 2102).NextDouble() < bar.Settings.DrumVariation;

	private static bool OpenHat(BarContext bar, double at) => NearBeat(at, 3.5) && (bar.LocalBar & 1) != 0
		&& DrumRandom(bar, 2103).NextDouble() < bar.Settings.DrumVariation;

	private static void DrumDecorations(BarContext bar, List<NoteEvent> notes, int key, DrumFill fill)
	{
		if (fill.Length > 0 || bar.Section.Kind == SectionKind.Outro) { return; }
		if (DrumRandom(bar, 2110).NextDouble() >= bar.Settings.DrumVariation) { return; }
		var second = (bar.LocalBar & 1) != 0;
		switch (bar.Layer.Pattern)
		{
			case LayerPattern.Snare:
				DrumHit(bar, notes, key, second ? 2.75 : 0.75, 0.1, -36);
				break;
			case LayerPattern.Kick:
				KickDecoration(bar, notes, key, second);
				break;
			default:
				HatDecoration(bar, notes, key, second);
				break;
		}
	}

	private static void KickDecoration(BarContext bar, List<NoteEvent> notes, int key, bool second)
	{
		if (bar.Settings.Beat == MusicBeat.Reggae) { return; }
		DrumHit(bar, notes, key, second ? 3.5 : 1.75, 0.14, -15);
	}

	private static void HatDecoration(BarContext bar, List<NoteEvent> notes, int key, bool second)
	{
		if (!second) { return; }
		DrumHit(bar, notes, key, 2.75, 0.1, -25);
		DrumHit(bar, notes, key, 3.25, 0.1, -20);
	}
}
