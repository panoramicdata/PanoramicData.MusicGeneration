namespace PanoramicData.MusicGeneration;

public static partial class ArrangementComposer
{
	private static void WriteLeadTone(BarContext bar, List<NoteEvent> notes, MotifTone tone, int pitch, bool last)
	{
		var at = tone.Beat - (SecondMotifBar(bar) ? bar.BeatsPerBar : 0);
		var duration = ConnectedDuration(bar, tone, last);
		if (bar.Section.Kind != SectionKind.Outro)
		{
			if (WriteSuspension(bar, notes, at, duration, tone.Accent)) { return; }
			if (WriteAppoggiatura(bar, notes, tone, pitch, at)) { return; }
			ChromaticPickup(bar, notes, pitch, at);
		}
		Add(bar, notes, pitch, at, duration, tone.Accent);
	}

	private static double ConnectedDuration(BarContext bar, MotifTone tone, bool last)
	{
		if (!last || SecondMotifBar(bar) || !FlowChoice(bar, 1600, bar.Settings.PhraseExpression)) { return tone.DurationBeats; }
		var next = MotifForBar(bar).Notes.First(n => n.Beat >= bar.BeatsPerBar);
		return Math.Max(tone.DurationBeats, next.Beat - tone.Beat + 0.035);
	}

	private static bool WriteSuspension(BarContext bar, List<NoteEvent> notes, double at, double duration, int accent)
	{
		if (!SuspensionEligible(bar, notes, at, duration)) { return false; }
		var previous = notes[^1];
		if (previous.SectionIndex != bar.Section.Index || previous.StartSeconds + previous.DurationSeconds < bar.StartSeconds - bar.BeatSeconds * 0.5) { return false; }
		var resolution = SuspensionPitch(bar, previous.MidiNote - bar.Layer.Octave * 12);
		if (resolution is null) { return false; }
		const double delay = 0.4;
		notes[^1] = previous with { DurationSeconds = PerformedTime(bar, delay) - previous.StartSeconds };
		Add(bar, notes, resolution.Value, delay, duration - delay, accent - 2);
		return true;
	}

	private static bool SuspensionEligible(BarContext bar, List<NoteEvent> notes, double at, double duration)
	{
		if (at > 0.001 || duration < 0.6 || notes.Count == 0) { return false; }
		return bar.Chord.StartSeconds >= bar.StartSeconds - 1e-9 && FlowChoice(bar, 1601, bar.Settings.PhraseExpression);
	}

	private static int? SuspensionPitch(BarContext bar, int pitch)
	{
		if (IsChordPitch(bar.Chord, pitch)) { return null; }
		var previous = bar.Harmony.LastOrDefault(c => c.EndSeconds <= bar.Chord.StartSeconds + 1e-9);
		if (previous is null || !IsChordPitch(previous, pitch)) { return null; }
		return Enumerable.Range(pitch - 2, 2).Where(p => p >= 48 && p <= 72 && IsChordPitch(bar.Chord, p))
			.OrderDescending().Select(p => (int?)p).FirstOrDefault();
	}

	private static bool WriteAppoggiatura(BarContext bar, List<NoteEvent> notes, MotifTone tone, int pitch, double at)
	{
		if (!SecondMotifBar(bar) || at > 0.001 || tone.DurationBeats < 0.7) { return false; }
		if (!FlowChoice(bar, 1602, bar.Settings.PhraseExpression * 0.5)) { return false; }
		var neighbour = UpperNeighbour(bar, pitch);
		if (neighbour is null) { return false; }
		var delay = Math.Min(0.4, tone.DurationBeats * 0.4);
		Add(bar, notes, neighbour.Value, at, delay, tone.Accent + 3);
		Add(bar, notes, pitch, at + delay, tone.DurationBeats - delay, tone.Accent - 3);
		return true;
	}

	private static int? UpperNeighbour(BarContext bar, int pitch)
	{
		var scale = bar.Settings.Minor ? MinorMelodicScale : MajorMelodicScale;
		var chromatic = FlowChoice(bar, 1603, bar.Settings.ChromaticColour);
		return Enumerable.Range(pitch + 1, 2).Where(p => p <= 72 && !IsChordPitch(bar.Chord, p))
			.Where(p => chromatic || scale.Contains((p - bar.Settings.Key + 120) % 12)).Select(p => (int?)p).FirstOrDefault();
	}

	private static void ChromaticPickup(BarContext bar, List<NoteEvent> notes, int pitch, double at)
	{
		if (!IsBeat(at) || at < 0.5 || !FlowChoice(bar, 1610 + (int)at, bar.Settings.ChromaticColour)) { return; }
		var start = PerformedTime(bar, at - 0.25);
		if (notes.Any(n => n.StartSeconds + n.DurationSeconds > start)) { return; }
		var scale = bar.Settings.Minor ? MinorMelodicScale : MajorMelodicScale;
		var neighbours = new[] { pitch - 1, pitch + 1 }.Where(p => p >= 48 && p <= 72 && !scale.Contains((p - bar.Settings.Key + 120) % 12));
		var approach = neighbours.Select(p => (int?)p).FirstOrDefault();
		if (approach.HasValue) { Add(bar, notes, approach.Value, at - 0.25, 0.2, -14); }
	}

	private static bool IsChordPitch(ChordEvent chord, int pitch) => chord.Intervals.Contains((pitch - chord.Root + 120) % 12);
}
