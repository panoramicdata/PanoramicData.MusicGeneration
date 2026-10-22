namespace PanoramicData.MusicGeneration;

public static partial class ArrangementComposer
{
	private static readonly double[][] HarmonicPacing = [[2, 1, 0.5, 0.5], [0.5, 0.5, 1, 2], [1.5, 0.5, 1.5, 0.5]];

	private static List<ChordEvent> ShapeHarmony(ArrangementSettings settings, List<ChordEvent> chords, FormPlan form)
	{
		var result = chords.ToList();
		var bar = Beats(settings) * 60.0 / settings.Tempo;
		foreach (var section in form.Sections.Where(s => s.Kind != SectionKind.Outro))
		{
			var first = chords.FindIndex(c => c.StartSeconds >= section.StartSeconds - 1e-9);
			for (var offset = 0; offset + 4 <= section.Bars; offset += 4)
			{
				var rng = new Pcg32(settings.Seed, 1200 + section.Index * 31L + offset);
				if (rng.NextDouble() >= settings.HarmonicMovement) { continue; }
				var lengths = HarmonicPacing[rng.Next(0, HarmonicPacing.Length)];
				var start = section.StartSeconds + offset * bar;
				for (var i = 0; i < 4; i++)
				{
					var end = start + lengths[i] * bar;
					result[first + offset + i] = chords[first + offset + i] with { StartSeconds = start, EndSeconds = end };
					start = end;
				}
			}
		}
		return result;
	}

	private static List<ChordEvent> ColourHarmony(ArrangementSettings settings, List<ChordEvent> chords, FormPlan form)
	{
		var result = new List<ChordEvent>();
		for (var i = 0; i < chords.Count; i++)
		{
			var chord = chords[i];
			var section = form.Sections.First(s => chord.StartSeconds < s.EndSeconds - 1e-9);
			var next = chords[Math.Min(i + 1, chords.Count - 1)];
			var colour = ColourApproach(settings, chord, next, section, i);
			if (colour is null) { result.Add(chord); continue; }
			var split = chord.EndSeconds - Math.Min(chord.EndSeconds - chord.StartSeconds, Beats(settings) * 60.0 / settings.Tempo) * 0.5;
			result.Add(chord with { EndSeconds = split });
			result.Add(colour with { StartSeconds = split, EndSeconds = chord.EndSeconds });
		}
		return result;
	}

	private static ChordEvent? ColourApproach(ArrangementSettings settings, ChordEvent chord,
		ChordEvent next, SectionEvent section, int index)
	{
		if (section.Kind == SectionKind.Outro || next.StartSeconds >= section.EndSeconds - 1e-9) { return null; }
		var borrowed = !settings.Minor && chord.Root == (settings.Key + 5) % 12 && next.Root == settings.Key;
		if (!borrowed && (index & 3) != 1) { return null; }
		if (new Pcg32(settings.Seed, 1300 + index).NextDouble() >= settings.ChromaticColour) { return null; }
		return MakeApproach(settings, chord, next, borrowed);
	}

	private static ChordEvent? MakeApproach(ArrangementSettings settings, ChordEvent chord, ChordEvent next, bool borrowed)
	{
		if (!next.Intervals.Contains(7) || chord.Root == next.Root) { return null; }
		var root = borrowed ? chord.Root : (next.Root + 7) % 12;
		var definition = ChordProgressions.Parse(Pitch.NameOf(60 + root)[..^1] + (borrowed ? "m" : "7"));
		var scale = settings.Minor ? MinorMelodicScale : MajorMelodicScale;
		if (definition.Intervals.All(i => scale.Contains((root + i - settings.Key + 12) % 12))) { return null; }
		return new(definition.Symbol, definition.Root, definition.Intervals, chord.StartSeconds, chord.EndSeconds)
		{
			Function = borrowed ? "Borrowed minor subdominant" : "Dominant approach",
			ResolvesTo = next.Symbol
		};
	}
}
