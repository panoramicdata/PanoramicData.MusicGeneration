namespace PanoramicData.MusicGeneration.SoundFonts;

internal static class LayeredScoreValidator
{
	internal static void Validate(ArrangedScore score, int rate)
	{
		ArgumentNullException.ThrowIfNull(score);
		ArrangementComposer.Validate(score.Settings);
		ArgumentNullException.ThrowIfNull(score.Tracks);
		ArgumentNullException.ThrowIfNull(score.Chords);
		ArgumentNullException.ThrowIfNull(score.Sections);
		ArgumentOutOfRangeException.ThrowIfLessThan(rate, 16000);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(rate, 96000);
		if (score.Version != 1 || score.Tracks.Count != score.Settings.Layers.Count)
		{
			throw new ArgumentException("Unsupported score version or mismatched layers.", nameof(score));
		}
		if (score.Tracks.Select(t => t?.Instrument?.Id).Distinct().Count() != score.Tracks.Count)
		{
			throw new ArgumentException("A score must contain each layer exactly once.", nameof(score));
		}
		foreach (var track in score.Tracks)
		{
			ValidateTrack(score, track);
		}
		ValidateTimeline(score);
		ArrangementEndingRenderer.Validate(score);
	}

	private static void ValidateTimeline(ArrangedScore score)
	{
		if (score.Chords.Count > 2000 || score.Sections.Count > 2000)
		{
			throw new ArgumentException("Too many timeline markers.", nameof(score));
		}
		foreach (var chord in score.Chords)
		{
			ValidateChord(chord);
			ValidateMarker(chord.StartSeconds, chord.EndSeconds, ArrangementEndingRenderer.Duration(score));
		}
		foreach (var section in score.Sections)
		{
			ArgumentNullException.ThrowIfNull(section);
			ValidateMarker(section.StartSeconds, section.EndSeconds, ArrangementEndingRenderer.Duration(score));
		}
	}

	private static void ValidateChord(ChordEvent chord)
	{
		ArgumentNullException.ThrowIfNull(chord);
		ArgumentException.ThrowIfNullOrWhiteSpace(chord.Symbol);
		if (chord.Root is < 0 or > 11 || chord.Symbol.Length > 50 || chord.Intervals is not { Count: > 0 and <= 12 })
		{
			throw new ArgumentException("Invalid chord identity or intervals.", nameof(chord));
		}
		if (chord.Intervals.Any(i => i is < 0 or > 11)) { throw new ArgumentException("Chord intervals must lie within an octave.", nameof(chord)); }
		ValidateChordDescription(chord);
	}

	private static void ValidateChordDescription(ChordEvent chord)
	{
		if (chord.Function?.Length > 100 || chord.ResolvesTo?.Length > 50)
		{
			throw new ArgumentException("Chord function and resolution labels must be short.", nameof(chord));
		}
	}

	internal static void ValidateMarker(double start, double end, double seconds)
	{
		if (!double.IsFinite(start) || !double.IsFinite(end) || start < 0 || end <= start || end > seconds)
		{
			throw new ArgumentException("Timeline markers must lie inside the score duration.");
		}
	}

	private static void ValidateTrack(ArrangedScore score, LayerScore track)
	{
		ArgumentNullException.ThrowIfNull(track);
		ArgumentNullException.ThrowIfNull(track.Notes);
		if (!score.Settings.Layers.Contains(track.Instrument) || track.Notes.Count > 100000)
		{
			throw new ArgumentException("The score contains an unknown layer or too many notes.", nameof(score));
		}
		foreach (var note in track.Notes)
		{
			SoundFontRenderer.ValidateNote(note);
			ValidateLayerNote(score, track.Instrument, note);
		}
	}

	private static void ValidateLayerNote(ArrangedScore score, InstrumentLayer layer, NoteEvent note)
	{
		if (note.Voice != layer.Voice || note.LayerId != layer.Id || note.Velocity is < 1 or > 127)
		{
			throw new ArgumentException("A note does not match its layer or velocity range.", nameof(score));
		}
		if (note.StartSeconds + note.DurationSeconds > ArrangementEndingRenderer.Duration(score) + 1e-8)
		{
			throw new ArgumentException("A note does not match its layer or score duration.", nameof(score));
		}
	}
}
