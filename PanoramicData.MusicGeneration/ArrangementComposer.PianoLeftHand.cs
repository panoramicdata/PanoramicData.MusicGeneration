namespace PanoramicData.MusicGeneration;

public static partial class ArrangementComposer
{
	private static void PianoLeftHand(ArrangementSettings settings, LayerScore track, List<ChordEvent> harmony,
		SectionEvent section, NoteEvent[] lead, List<NoteEvent> notes)
	{
		var beat = 60.0 / settings.Tempo;
		var beats = Beats(settings);
		for (var bar = 0; bar < section.Bars; bar++)
		{
			var start = section.StartSeconds + bar * beats * beat;
			var written = lead.Where(n => n.StartSeconds >= start && n.StartSeconds < start + beats * beat).ToArray();
			if (written.Length == 0) { continue; }
			var ceiling = Math.Min(59 + track.Instrument.Octave * 12, written.Min(n => n.MidiNote) - 3);
			PianoLeftBar(settings, harmony, written[0], start, ceiling, notes);
		}
	}

	private static void PianoLeftBar(ArrangementSettings settings, List<ChordEvent> harmony, NoteEvent source,
		double start, int ceiling, List<NoteEvent> notes)
	{
		var beat = 60.0 / settings.Tempo;
		var starts = PianoLeftStarts(settings.Beat);
		for (var index = 0; index < starts.Length; index++)
		{
			var at = starts[index];
			var time = start + (at + (NearBeat(at % 1, 0.5) ? settings.Swing : 0)) * beat;
			var chord = harmony.Last(c => c.StartSeconds <= time + 1e-9);
			var duration = Math.Min(beat * 0.7, chord.EndSeconds - time);
			if (duration <= 1e-8) { continue; }
			var pitches = PianoLeftPitches(settings, chord, ceiling, index);
			foreach (var pitch in pitches.Where(p => PianoToneFree(notes, p, time, duration)))
			{
				notes.Add(PianoNote(source, pitch, time, duration, -28));
			}
		}
	}

	private static double[] PianoLeftStarts(MusicBeat beat)
	{
		if (beat == MusicBeat.Waltz) { return [0, 1, 2]; }
		if (beat == MusicBeat.Arpeggio) { return [0, 0.5, 1, 1.5, 2, 2.5, 3, 3.5]; }
		if (beat is MusicBeat.Stride or MusicBeat.Swing or MusicBeat.March) { return [0, 1, 2, 3]; }
		return beat == MusicBeat.Straight ? [0, 1.5, 2.5] : ChordStarts(beat);
	}

	private static int[] PianoLeftPitches(ArrangementSettings settings, ChordEvent chord, int ceiling, int at)
	{
		var pitches = Enumerable.Range(Math.Max(21, ceiling - 12), 13).Where(p => p <= ceiling && IsChordPitch(chord, p)).ToArray();
		var bassBeat = settings.Beat == MusicBeat.Waltz ? at == 0 : at % 2 == 0;
		if (!bassBeat) { return [.. pitches.OrderDescending().Take(2)]; }
		var target = (at == 0 ? chord.Root : chord.Root + chord.Intervals[2]) % 12;
		return [.. pitches.Where(p => p % 12 == target).Take(1)];
	}
}
