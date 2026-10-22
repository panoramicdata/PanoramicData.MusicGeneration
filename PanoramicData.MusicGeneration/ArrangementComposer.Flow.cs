namespace PanoramicData.MusicGeneration;

public static partial class ArrangementComposer
{
	private static ChordEvent ChordAt(BarContext bar, double at) =>
		bar.Harmony.Last(c => c.StartSeconds <= bar.StartSeconds + at * bar.BeatSeconds + 1e-9);

	private static BarContext AtHarmony(BarContext bar, double at)
	{
		var chord = ChordAt(bar, at);
		return bar with { Chord = chord, Voicing = VoiceLead(chord, bar.Voicing) };
	}

	private static bool FlowChoice(BarContext bar, int stream, double probability) =>
		new Pcg32(bar.Settings.Seed, stream + bar.Section.Index * 101L + bar.LocalBar).NextDouble() < probability;

	private static double PerformedTime(BarContext bar, double at)
	{
		var time = bar.StartSeconds + at * bar.BeatSeconds;
		if (bar.Layer.Pattern is not (LayerPattern.Melody or LayerPattern.Countermelody)) { return time; }
		var length = bar.BeatsPerBar * 2;
		var position = ((bar.LocalBar & 1) * bar.BeatsPerBar + at) % length;
		var shape = -0.12 * bar.Settings.PhraseExpression * Math.Sin(Math.PI * position / length);
		return Math.Max(bar.Section.StartSeconds, time + shape * bar.BeatSeconds);
	}

	private static int PhraseAccent(BarContext bar, double at)
	{
		if (bar.Layer.Pattern is not (LayerPattern.Melody or LayerPattern.Countermelody)) { return 0; }
		var position = ((bar.LocalBar & 1) * bar.BeatsPerBar + at) / (bar.BeatsPerBar * 2);
		return (int)Math.Round(bar.Settings.PhraseExpression * (12 * Math.Sin(Math.PI * position) - 5 * position));
	}

	private static LayerScore[] CoordinateAnswers(ArrangementSettings settings, LayerScore[] tracks, List<SectionEvent> sections)
	{
		if (settings.PhraseExpression <= 0) { return tracks; }
		var lead = tracks.Where(t => t.Instrument.Enabled && t.Instrument.Gain > 0 && t.Instrument.Pattern == LayerPattern.Melody)
			.SelectMany(t => t.Notes).OrderBy(n => n.StartSeconds).ToArray();
		if (lead.Length == 0) { return tracks; }
		return [.. tracks.Select(t => t.Instrument.Pattern != LayerPattern.Countermelody ? t :
			t with { Notes = AnswerGaps(t.Notes, lead, sections, settings.Tempo) })];
	}

	private static NoteEvent[] AnswerGaps(IReadOnlyList<NoteEvent> answers, NoteEvent[] lead, List<SectionEvent> sections, int tempo)
	{
		var result = new List<NoteEvent>();
		foreach (var note in answers)
		{
			var section = sections.Single(s => s.Index == note.SectionIndex);
			var limit = Math.Min(section.EndSeconds, note.StartSeconds + 60.0 / tempo);
			var candidates = lead.Select(n => n.StartSeconds + n.DurationSeconds + 0.025)
				.Append(note.StartSeconds).Where(t => t >= note.StartSeconds && t < limit).Order();
			var fitted = FitAnswer(note, candidates, [.. lead, .. result], limit);
			if (fitted.HasValue) { result.Add(fitted.Value); }
		}
		return [.. result.OrderBy(n => n.StartSeconds)];
	}

	private static NoteEvent? FitAnswer(NoteEvent note, IEnumerable<double> candidates, NoteEvent[] lead, double limit)
	{
		foreach (var start in candidates)
		{
			if (lead.Any(n => n.StartSeconds < start + 0.001 && n.StartSeconds + n.DurationSeconds > start)) { continue; }
			var next = lead.Where(n => n.StartSeconds >= start).Select(n => n.StartSeconds).DefaultIfEmpty(limit).Min();
			var length = Math.Min(note.DurationSeconds, Math.Min(next - 0.015, limit) - start);
			if (length >= Math.Min(0.09, note.DurationSeconds)) { return note with { StartSeconds = start, DurationSeconds = length }; }
		}
		return null;
	}

	private static List<NoteEvent> SeparateRepeatedNotes(List<NoteEvent> notes)
	{
		var ordered = notes.OrderBy(n => n.StartSeconds).ToList();
		var latest = new Dictionary<int, int>();
		for (var i = 0; i < ordered.Count; i++)
		{
			var note = ordered[i];
			if (latest.TryGetValue(note.MidiNote, out var index))
			{
				var previous = ordered[index];
				if (previous.StartSeconds + previous.DurationSeconds > note.StartSeconds)
				{
					ordered[index] = previous with { DurationSeconds = note.StartSeconds - previous.StartSeconds };
				}
			}
			latest[note.MidiNote] = i;
		}
		return [.. ordered.Where(n => n.DurationSeconds > 1e-8)];
	}
}
