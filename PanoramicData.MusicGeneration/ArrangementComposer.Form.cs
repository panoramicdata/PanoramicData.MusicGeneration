namespace PanoramicData.MusicGeneration;

public static partial class ArrangementComposer
{
	private sealed record FormPlan(List<SectionEvent> Sections, EndingPlan Ending, double Duration);

	private static FormPlan PlanForm(ArrangementSettings settings)
	{
		var bar = Beats(settings) * 60.0 / settings.Tempo;
		var available = Math.Max(1, (int)Math.Floor((settings.Seconds - 0.35) / bar));
		var ending = ChooseEnding(settings);
		var outroBars = Math.Min(available, EndingBars(ending));
		var sections = new List<SectionEvent>();
		var intro = Math.Min(available - outroBars, IntroBars(settings));
		if (intro > 0) { AppendSection(sections, SectionKind.Intro, intro, bar); }
		FillBody(settings, sections, available - outroBars - intro, bar);
		AppendSection(sections, SectionKind.Outro, outroBars, bar);
		var last = sections[^1];
		var end = Math.Min(last.EndSeconds, settings.Seconds - 0.35);
		sections[^1] = last with
		{
			EndSeconds = end,
			PlannedEndSeconds = end,
			HasCadence = ending is EndingKind.Cadence or EndingKind.Tag
		};
		return new(sections, new(ending, last.StartSeconds, end, 0.35), end + 0.35);
	}

	private static void FillBody(ArrangementSettings settings, List<SectionEvent> sections, int remaining, double bar)
	{
		var pairs = 0;
		while (remaining >= 16)
		{
			var kind = settings.Sections ? SectionKind.Verse : SectionKind.Chorus;
			AppendSection(sections, kind, 8, bar);
			AppendSection(sections, SectionKind.Chorus, 8, bar);
			remaining -= 16;
			pairs++;
			if (settings.Sections && pairs == 2 && remaining >= 12)
			{
				AppendSection(sections, SectionKind.Bridge, 4, bar);
				AppendSection(sections, SectionKind.Chorus, 8, bar);
				remaining -= 12;
			}
		}
		if (remaining >= 8) { AppendSection(sections, SectionKind.Chorus, 8, bar); }
	}

	private static void AppendSection(List<SectionEvent> sections, SectionKind kind, int bars, double bar)
	{
		var start = sections.LastOrDefault()?.EndSeconds ?? 0;
		var phrases = kind is SectionKind.Verse or SectionKind.Chorus ? 4 : 1;
		sections.Add(new()
		{
			Index = sections.Count,
			Kind = kind,
			MaterialId = SectionMaterial(kind),
			StartSeconds = start,
			EndSeconds = start + bars * bar,
			PlannedEndSeconds = start + bars * bar,
			Bars = bars,
			PhraseCount = phrases,
			PhraseBars = bars / phrases,
			HasTransitionFill = kind == SectionKind.Verse
		});
	}

	private static string SectionMaterial(SectionKind kind) => kind switch
	{
		SectionKind.Verse => "A",
		SectionKind.Chorus => "B",
		SectionKind.Bridge => "C",
		_ => kind.ToString().ToLowerInvariant()
	};

	private static int IntroBars(ArrangementSettings settings)
	{
		var rng = new Pcg32(settings.Seed, 501);
		if (rng.NextDouble() >= settings.IntroVariation) { return 2; }
		return rng.NextDouble() < 0.7 ? 4 : 8;
	}

	private static int EndingBars(EndingKind kind) => kind switch
	{
		EndingKind.Fade => 8,
		EndingKind.Tag => 6,
		_ => 2
	};

	private static EndingKind ChooseEnding(ArrangementSettings settings)
	{
		var weights = settings.Endings;
		var values = new[] { weights.Fade, weights.Cadence, weights.Tag, weights.Stop };
		var draw = new Pcg32(settings.Seed, 502).NextDouble() * values.Sum();
		for (var i = 0; i < values.Length; i++)
		{
			draw -= values[i];
			if (draw < 0) { return (EndingKind)i; }
		}
		return EndingKind.Stop;
	}
}
