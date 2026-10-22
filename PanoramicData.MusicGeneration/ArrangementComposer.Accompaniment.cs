namespace PanoramicData.MusicGeneration;

public static partial class ArrangementComposer
{
	private static void HeldChords(BarContext bar, List<NoteEvent> notes)
	{
		var end = bar.StartSeconds + bar.BeatsPerBar * bar.BeatSeconds;
		foreach (var chord in bar.Harmony.Where(c => c.StartSeconds >= bar.StartSeconds - 1e-9 && c.StartSeconds < end - 1e-9))
		{
			var at = (chord.StartSeconds - bar.StartSeconds) / bar.BeatSeconds;
			var current = AtHarmony(bar, at);
			var duration = (chord.EndSeconds - chord.StartSeconds) / bar.BeatSeconds;
			foreach (var midi in current.Voicing)
			{
				if (TieCommonTone(current, notes, midi, chord.EndSeconds)) { continue; }
				Add(current, notes, midi, at, duration, -12);
			}
		}
	}

	private static bool TieCommonTone(BarContext bar, List<NoteEvent> notes, int midi, double end)
	{
		if (bar.Settings.PhraseExpression <= 0) { return false; }
		var actual = Math.Clamp(midi + bar.Layer.Octave * 12, 0, 127);
		var index = notes.FindLastIndex(n => n.MidiNote == actual && n.SectionIndex == bar.Section.Index);
		if (index < 0) { return false; }
		var previous = notes[index];
		if (Math.Abs(previous.StartSeconds + previous.DurationSeconds - bar.Chord.StartSeconds) > 0.001) { return false; }
		notes[index] = previous with { DurationSeconds = end - previous.StartSeconds };
		return true;
	}

	private static bool AccompanimentRest(BarContext bar, double at)
	{
		if (at < 0.01 || bar.Section.Kind == SectionKind.Outro) { return false; }
		var answerWindow = SecondMotifBar(bar) && at >= bar.BeatsPerBar - 1;
		return answerWindow && FlowChoice(bar, 1500 + (int)(at * 8), bar.Settings.AccompanimentVariation);
	}

	private static void BassApproach(BarContext bar, List<NoteEvent> notes)
	{
		var end = bar.StartSeconds + bar.BeatsPerBar * bar.BeatSeconds;
		if (end >= bar.Section.EndSeconds - 1e-9 || !FlowChoice(bar, 1510, bar.Settings.AccompanimentVariation)) { return; }
		var next = ChordAt(bar, bar.BeatsPerBar);
		if (next.Root == ChordAt(bar, bar.BeatsPerBar - 0.5).Root) { return; }
		var target = 36 + next.Root;
		var pitch = FlowChoice(bar, 1511, bar.Settings.ChromaticColour) ? target - 1 : LowerScaleNeighbour(bar.Settings, target);
		var onset = end - 0.5 * bar.BeatSeconds;
		TrimBassAt(notes, onset);
		Add(bar, notes, pitch, bar.BeatsPerBar - 0.5, 0.4, -10);
	}

	private static void TrimBassAt(List<NoteEvent> notes, double onset)
	{
		for (var i = 0; i < notes.Count; i++)
		{
			var note = notes[i];
			if (note.StartSeconds < onset && note.StartSeconds + note.DurationSeconds > onset)
			{
				notes[i] = note with { DurationSeconds = onset - note.StartSeconds };
			}
		}
	}

	private static int LowerScaleNeighbour(ArrangementSettings settings, int target)
	{
		var scale = settings.Minor ? MinorMelodicScale : MajorMelodicScale;
		return Enumerable.Range(target - 2, 2).Where(p => scale.Contains((p - settings.Key + 120) % 12)).Max();
	}
}
