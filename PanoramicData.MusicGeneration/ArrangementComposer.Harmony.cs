namespace PanoramicData.MusicGeneration;

public static partial class ArrangementComposer
{
	private static List<ChordEvent> Harmony(ArrangementSettings settings, FormPlan form)
	{
		var result = new List<ChordEvent>();
		var bar = Beats(settings) * 60.0 / settings.Tempo;
		foreach (var section in form.Sections)
		{
			var plan = SectionChords(settings, section.Kind);
			for (var index = 0; index < section.Bars; index++)
			{
				var start = section.StartSeconds + index * bar;
				var chord = PlannedChord(settings, section, plan, index, form.Ending.Kind);
				result.Add(new(chord.Symbol, chord.Root, chord.Intervals, start, Math.Min(start + bar, section.EndSeconds)));
			}
		}
		return result;
	}

	private static IReadOnlyList<ChordDefinition> SectionChords(ArrangementSettings settings, SectionKind kind)
	{
		var baseChords = ChordProgressions.Resolve(settings);
		if (settings.SectionContrast <= 0) { return baseChords; }
		var rng = new Pcg32(settings.Seed, 601 + (int)kind);
		if (rng.NextDouble() >= settings.SectionContrast) { return baseChords; }
		return ContrastingChords(kind, baseChords);
	}

	private static IReadOnlyList<ChordDefinition> ContrastingChords(SectionKind kind,
		IReadOnlyList<ChordDefinition> baseChords) => kind switch
		{
			SectionKind.Verse => RotateChords(baseChords, baseChords.Count / 2),
			SectionKind.Bridge => [.. baseChords.Reverse()],
			SectionKind.Intro => [baseChords[0], baseChords[^1]],
			_ => baseChords
		};

	private static IReadOnlyList<ChordDefinition> RotateChords(IReadOnlyList<ChordDefinition> chords, int offset) =>
		[.. Enumerable.Range(0, chords.Count).Select(i => chords[(i + offset) % chords.Count])];

	private static ChordDefinition DiatonicChord(ArrangementSettings settings, int degree)
	{
		var roots = settings.Minor ? new[] { 0, 2, 3, 5, 7, 8, 10 } : [0, 2, 4, 5, 7, 9, 11];
		var qualities = settings.Minor ? new[] { "m", "dim", "", "m", "7", "", "" } : ["", "m", "m", "", "7", "m", "dim"];
		var root = (settings.Key + roots[degree]) % 12;
		var name = Pitch.NameOf(60 + root)[..^1];
		return ChordProgressions.Parse(name + qualities[degree]);
	}

	private static ChordDefinition PlannedChord(ArrangementSettings settings, SectionEvent section,
		IReadOnlyList<ChordDefinition> plan, int index, EndingKind ending)
	{
		var chord = plan[index % plan.Count];
		if (section.Kind == SectionKind.Outro) { return OutroChord(settings, plan, index, section.Bars, ending); }
		if (UseSubstitution(settings, section, index)) { chord = PhraseVariant(settings, chord, section.Index, index); }
		if (settings.Progression != "Custom" && settings.SectionContrast > 0 && index == section.Bars - 1)
		{
			return ClosingChord(settings, section.Kind);
		}
		return chord;
	}

	private static bool UseSubstitution(ArrangementSettings settings, SectionEvent section, int index)
	{
		if ((index + 2) % 4 != 0) { return false; }
		var draw = new Pcg32(settings.Seed, 700 + section.Index * 31L + index).NextDouble();
		return draw < settings.ProgressionVariation;
	}

	private static ChordDefinition Substitute(ArrangementSettings settings, ChordDefinition chord)
	{
		var tonic = DiatonicChord(settings, 0);
		var relative = DiatonicChord(settings, 5);
		if (chord.Root == tonic.Root) { return relative; }
		if (chord.Root == relative.Root) { return tonic; }
		var subdominant = DiatonicChord(settings, 3);
		return chord.Root == subdominant.Root ? DiatonicChord(settings, 1) : subdominant;
	}

	private static ChordDefinition PhraseVariant(ArrangementSettings settings, ChordDefinition chord, int section, int bar)
	{
		var substitute = Substitute(settings, chord);
		if (new Pcg32(settings.Seed, 701 + section * 31L + bar).NextDouble() < 0.5) { return substitute; }
		var root = Pitch.NameOf(60 + chord.Root)[..^1];
		if (chord.Intervals.SequenceEqual([0, 4, 7])) { return ChordProgressions.Parse(root + "maj7"); }
		return chord.Intervals.SequenceEqual([0, 3, 7]) ? ChordProgressions.Parse(root + "m7") : substitute;
	}

	private static ChordDefinition ClosingChord(ArrangementSettings settings, SectionKind kind) =>
		DiatonicChord(settings, kind == SectionKind.Chorus ? 0 : 4);

	private static ChordDefinition OutroChord(ArrangementSettings settings, IReadOnlyList<ChordDefinition> plan,
		int index, int bars, EndingKind ending)
	{
		if (ending == EndingKind.Fade) { return plan[index % plan.Count]; }
		if (index == bars - 1) { return DiatonicChord(settings, 0); }
		if (index == bars - 2) { return DiatonicChord(settings, 4); }
		var cycle = ending == EndingKind.Tag ? Math.Min(2, plan.Count) : plan.Count;
		return plan[index % cycle];
	}
}
