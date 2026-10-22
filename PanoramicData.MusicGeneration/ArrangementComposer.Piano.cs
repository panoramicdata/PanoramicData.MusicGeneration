namespace PanoramicData.MusicGeneration;

public static partial class ArrangementComposer
{
	private static bool IsPiano(InstrumentLayer layer) => layer.Bank == 0 && layer.Program is >= 0 and <= 7;

	private static LayerScore[] WritePianoHands(ArrangementSettings settings, LayerScore[] tracks,
		List<ChordEvent> harmony, List<SectionEvent> sections) =>
		[.. tracks.Select(track => PianoHandsEnabled(track.Instrument)
			? track with { Notes = PianoHands(settings, track, tracks, harmony, sections) } : track)];

	private static bool PianoHandsEnabled(InstrumentLayer layer) => IsPiano(layer) && layer.PianoTwoHands
		&& layer.Pattern is LayerPattern.Melody or LayerPattern.Arpeggio;

	private static List<NoteEvent> PianoHands(ArrangementSettings settings, LayerScore track, LayerScore[] tracks,
		List<ChordEvent> harmony, List<SectionEvent> sections)
	{
		var notes = track.Notes.ToList();
		PianoTreble(settings, track, harmony, notes);
		foreach (var section in sections)
		{
			var lead = track.Notes.Where(n => n.SectionIndex == section.Index).ToArray();
			if (lead.Length == 0) { continue; }
			var supporting = PianoSupport(track, tracks, section.Index);
			if (supporting) { continue; }
			if (section.Kind == SectionKind.Outro) { PianoClose(track, harmony, lead, notes); continue; }
			PianoLeftHand(settings, track, harmony, section, lead, notes);
		}
		return SeparateRepeatedNotes(notes);
	}

	private static bool PianoSupport(LayerScore track, LayerScore[] tracks, int section) =>
		tracks.Any(t => t.Instrument.Id != track.Instrument.Id && t.Instrument.Enabled && t.Instrument.Gain > 0
			&& PianoAccompanist(t.Instrument, track.Instrument)
			&& t.Notes.Any(n => n.SectionIndex == section));

	private static bool PianoAccompanist(InstrumentLayer candidate, InstrumentLayer lead) =>
		IsPiano(candidate) && candidate.Program == lead.Program && candidate.Pattern is LayerPattern.Bass or LayerPattern.Chords;

	private static void PianoClose(LayerScore track, List<ChordEvent> harmony, NoteEvent[] lead, List<NoteEvent> notes)
	{
		foreach (var note in lead)
		{
			var chord = harmony.Last(c => c.StartSeconds <= note.StartSeconds + 1e-9);
			var ceiling = Math.Min(55 + track.Instrument.Octave * 12, note.MidiNote - 7);
			var root = PianoRoot(chord, ceiling);
			if (root >= 21) { notes.Add(PianoNote(note, root, note.StartSeconds, note.DurationSeconds, -24)); }
		}
	}

	private static int PianoRoot(ChordEvent chord, int ceiling)
	{
		var lowest = Math.Max(21, ceiling - 11);
		return Enumerable.Range(lowest, Math.Max(0, ceiling - lowest + 1))
			.Where(p => p % 12 == chord.Root).DefaultIfEmpty(-1).Max();
	}

	private static void PianoTreble(ArrangementSettings settings, LayerScore track, List<ChordEvent> harmony, List<NoteEvent> notes)
	{
		var beat = 60.0 / settings.Tempo;
		foreach (var melody in track.Notes)
		{
			var position = melody.StartSeconds / beat;
			if (Math.Abs(position - Math.Round(position)) > 0.22 || melody.DurationSeconds < beat * 0.25) { continue; }
			var chord = harmony.Last(c => c.StartSeconds <= melody.StartSeconds + 1e-9);
			if (!IsChordPitch(chord, melody.MidiNote)) { continue; }
			var duration = Math.Min(melody.DurationSeconds, Math.Min(beat * 0.8, chord.EndSeconds - melody.StartSeconds));
			var count = settings.Genre == MusicGenre.Ragtime ? 2 : 1;
			var pitches = Enumerable.Range(Math.Max(48, melody.MidiNote - 12), Math.Max(0, melody.MidiNote - Math.Max(48, melody.MidiNote - 12) - 2))
				.Where(p => IsChordPitch(chord, p)).OrderDescending();
			foreach (var pitch in pitches.Where(p => PianoToneFree(track.Notes, p, melody.StartSeconds, duration)).Take(count))
			{
				notes.Add(PianoNote(melody, pitch, melody.StartSeconds, duration, -18));
			}
		}
	}

	private static bool PianoToneFree(IReadOnlyList<NoteEvent> notes, int pitch, double start, double duration) =>
		!notes.Any(n => n.MidiNote == pitch && n.StartSeconds < start + duration && n.StartSeconds + n.DurationSeconds > start);

	private static NoteEvent PianoNote(NoteEvent source, int pitch, double start, double duration, int accent) => source with
	{
		MidiNote = pitch,
		FrequencyHz = Pitch.FrequencyOf(pitch),
		StartSeconds = start,
		DurationSeconds = duration,
		Velocity = Math.Clamp(source.Velocity + accent, 1, 127)
	};

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
