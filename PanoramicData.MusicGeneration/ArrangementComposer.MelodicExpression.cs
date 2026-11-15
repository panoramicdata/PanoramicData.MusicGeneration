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
}
