namespace PanoramicData.MusicGeneration;

public static partial class ArrangementComposer
{
	private static bool UsesHook(BarContext bar) => bar.Section.Kind == SectionKind.Chorus ||
		bar.Section.Kind == SectionKind.Outro && bar.Ending.Kind is EndingKind.Fade or EndingKind.Tag;

	private static bool SparseIntro(BarContext bar) => bar.Section.Kind == SectionKind.Intro &&
		new Pcg32(bar.Settings.Seed, 503).NextDouble() < bar.Settings.IntroVariation;

	private static bool IntroRest(BarContext bar)
	{
		if (!SparseIntro(bar)) { return false; }
		var early = bar.LocalBar < bar.Section.Bars / 2;
		return early && bar.Layer.Pattern is LayerPattern.Snare or LayerPattern.Countermelody;
	}

	private static int DynamicLift(BarContext bar)
	{
		var role = bar.Section.Kind switch
		{
			SectionKind.Chorus => 8,
			SectionKind.Bridge => -8,
			SectionKind.Intro => -18 + (int)(16.0 * (bar.LocalBar + 1) / bar.Section.Bars),
			_ => 0
		};
		var phrase = bar.LocalBar % 2 == 0 ? -3 : 1;
		return role + phrase;
	}

	private static bool WriteEndingHit(BarContext bar, List<NoteEvent> notes)
	{
		if (bar.Section.Kind != SectionKind.Outro || bar.Ending.Kind == EndingKind.Fade) { return false; }
		if (bar.LocalBar != bar.Section.Bars - 1) { return false; }
		var duration = bar.Ending.Kind == EndingKind.Stop ? 0.2 : bar.BeatsPerBar * 0.7;
		if (bar.Layer.Pattern is LayerPattern.Chords or LayerPattern.Arpeggio)
		{
			foreach (var midi in bar.Voicing) { Add(bar, notes, midi, 0, duration, -5); }
		}
		else { Add(bar, notes, FinalPitch(bar), 0, duration, -3); }
		return true;
	}

	private static int FinalPitch(BarContext bar) => bar.Layer.Pattern switch
	{
		LayerPattern.Bass => 36 + bar.Settings.Key,
		LayerPattern.Kick => 36,
		LayerPattern.Snare => 38,
		LayerPattern.HiHat => 42,
		_ => 60 + bar.Settings.Key
	};

	private static void TransitionFill(BarContext bar, List<NoteEvent> notes, int key)
	{
		if (bar.Layer.Pattern != LayerPattern.Snare || !bar.Section.HasTransitionFill) { return; }
		if (bar.LocalBar != bar.Section.Bars - 1) { return; }
		Add(bar, notes, key, bar.BeatsPerBar - 0.5, 0.15, -6);
		Add(bar, notes, key, bar.BeatsPerBar - 0.25, 0.15, 0);
	}
}
