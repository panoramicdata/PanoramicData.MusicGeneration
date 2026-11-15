namespace PanoramicData.MusicGeneration;

public static partial class ArrangementComposer
{
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
		var approach = ChromaticApproachPitch(bar.Settings, pitch);
		if (approach.HasValue) { Add(bar, notes, approach.Value, at - 0.25, 0.2, -14); }
	}

	private static int? ChromaticApproachPitch(ArrangementSettings settings, int pitch)
	{
		var scale = settings.Minor ? MinorMelodicScale : MajorMelodicScale;
		return new[] { pitch - 1, pitch + 1 }.Where(p => p >= 48 && p <= 72 && !scale.Contains((p - settings.Key + 120) % 12))
			.Select(p => (int?)p).FirstOrDefault();
	}

	private static bool IsChordPitch(ChordEvent chord, int pitch) => chord.Intervals.Contains((pitch - chord.Root + 120) % 12);
}
