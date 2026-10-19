namespace PanoramicData.MusicGeneration;

internal static class SectionPlanner
{
	internal static IReadOnlyList<SectionEvent> Place(SongForm form, SongInfo info, double seconds)
	{
		var beat = 60.0 / info.TempoBpm;
		var bar = 4 * beat;
		var lengths = form.Sections.Select(s => s.Bars * s.PhraseCount).ToArray();
		var available = Math.Max(0, (int)Math.Floor((seconds - info.LeadInSeconds) / bar + 1e-10));
		if (form.DurationMode == FormDurationMode.FitWholeBars)
		{
			lengths = Fit(lengths, available);
		}
		else if (form.DurationMode == FormDurationMode.FitWholePhrases)
		{
			lengths = FitPhrases(form.Sections, available);
		}

		var result = new List<SectionEvent>();
		var elapsedBars = 0;
		for (var index = 0; index < lengths.Length; index++)
		{
			var start = info.LeadInSeconds + (elapsedBars * bar);
			if (start >= seconds)
			{
				break;
			}
			if (lengths[index] == 0)
			{
				continue;
			}
			elapsedBars += lengths[index];
			var end = SnapEnd(info.LeadInSeconds + (elapsedBars * bar), seconds);
			result.Add(CreateSection(form, index, lengths[index], start, end, seconds, beat));
		}
		return result;
	}

	private static double SnapEnd(double end, double seconds) => Math.Abs(end - seconds) < 1e-9 ? seconds : end;

	private static SectionEvent CreateSection(SongForm form, int index, int bars, double start, double end, double seconds, double beat)
	{
		var section = form.Sections[index];
		var complete = end <= seconds;
		return new SectionEvent
		{
			Index = index,
			Kind = section.Kind,
			MaterialId = section.MaterialId,
			StartSeconds = start,
			EndSeconds = Math.Min(end, seconds),
			PlannedEndSeconds = end,
			Bars = bars,
			PhraseCount = section.PhraseCount,
			PhraseBars = form.DurationMode == FormDurationMode.FitWholePhrases ? bars / section.PhraseCount : section.Bars,
			PickupStartSeconds = Math.Max(0, start - (section.PickupBeats * beat)),
			HasCadence = complete && index == form.Sections.Count - 1 && form.EndWithCadence,
			HasTransitionFill = complete && index < form.Sections.Count - 1 && section.TransitionFill
				&& section.Voices?.Contains(VoiceKind.Snare) != false
		};
	}

	private static int[] FitPhrases(IReadOnlyList<SongSection> sections, int available)
	{
		var total = sections.Sum(s => s.Bars * s.PhraseCount);
		if (available < total)
		{
			return [.. sections.Select(s => s.Bars * s.PhraseCount)];
		}
		var groups = PhraseGroups(sections);
		var phraseBars = groups.ToDictionary(g => g, g => (int)((long)available * g[0].Bars / total));
		var used = groups.Sum(g => phraseBars[g] * PhraseGroupCost(g));
		while (used < available)
		{
			var group = groups.Where(g => PhraseGroupCost(g) <= available - used)
				.OrderByDescending(g => (double)available * g[0].Bars / total - phraseBars[g])
				.ThenByDescending(PhraseGroupCost).FirstOrDefault();
			if (group is null)
			{
				break;
			}
			phraseBars[group]++;
			used += PhraseGroupCost(group);
		}
		return [.. sections.Select(s => phraseBars[groups.First(g => g.Contains(s))] * s.PhraseCount)];
	}

	private static int PhraseGroupCost(List<SongSection> group) => group[0].PhraseCount * group.Count;

	private static bool SamePhraseFamily(SongSection left, SongSection right) =>
		left.Bars == right.Bars && left.PhraseCount == right.PhraseCount
		&& (left.Kind == right.Kind || left.MaterialId == right.MaterialId);

	private static List<List<SongSection>> PhraseGroups(IReadOnlyList<SongSection> sections)
	{
		var groups = new List<List<SongSection>>();
		foreach (var section in sections)
		{
			var matches = groups.Where(g => g.Any(s => SamePhraseFamily(s, section))).ToArray();
			if (matches.Length == 0)
			{
				groups.Add([section]);
				continue;
			}
			var target = matches[0];
			target.Add(section);
			foreach (var other in matches.Skip(1))
			{
				target.AddRange(other);
				groups.Remove(other);
			}
		}
		return groups;
	}

	private static int[] Fit(int[] weights, int available)
	{
		var lengths = new int[weights.Length];
		var count = Math.Min(weights.Length, Math.Max(1, available));
		Array.Fill(lengths, 1, 0, count);
		var remaining = Math.Max(0, available - count);
		if (remaining == 0)
		{
			return lengths;
		}

		var total = weights.Sum();
		for (var i = 0; i < lengths.Length; i++)
		{
			lengths[i] += remaining * weights[i] / total;
		}
		var order = Enumerable.Range(0, weights.Length).OrderByDescending(i => remaining * weights[i] % total).ThenBy(i => i);
		foreach (var i in order.Take(available - lengths.Sum()))
		{
			lengths[i]++;
		}
		return lengths;
	}
}
