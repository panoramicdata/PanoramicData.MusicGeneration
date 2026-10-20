namespace PanoramicData.MusicGeneration.SoundFonts;

/// <summary>Replay identity for an arbitrary instrument-layer arrangement.</summary>
public sealed record LayeredRenderMetadata(string BankName, string BankSha256, string RendererVersion,
	int SampleRate, int CompositionVersion, IReadOnlyList<InstrumentLayer> Layers);

/// <summary>An audible arranged song and the identity of its rendering setup.</summary>
public sealed record LayeredRenderResult(GeneratedSong Song, LayeredRenderMetadata Metadata);

/// <summary>Overall rendering progress for arbitrary instrumental layers.</summary>
public sealed record LayerRenderProgress(string Layer, double Fraction);

public sealed partial class SoundFontRenderer
{
	/// <summary>Renders a written arrangement at 22,050 Hz.</summary>
	public Task<LayeredRenderResult> RenderScoreAsync(ArrangedScore score) =>
		RenderScoreAsync(score, 22050, null, CancellationToken.None);

	/// <summary>Renders independent layers into one mono mix. Muting/editing layers re-renders the saved written score.</summary>
	public async Task<LayeredRenderResult> RenderScoreAsync(ArrangedScore score, int sampleRate,
		Func<LayerRenderProgress, Task>? progress, CancellationToken cancellationToken)
	{
		ValidateScore(score, sampleRate);
		var info = new SongInfo(score.Settings.Seed, score.Settings.Tempo, score.Settings.Minor ? "Minor" : "Major", 36 + score.Settings.Key, 0, 0);
		var frames = checked(score.Settings.Seconds * sampleRate);
		var mix = new double[frames];
		var template = new GeneratedSong(info, new float[frames], sampleRate, []);
		var audible = new List<NoteEvent>();
		for (var index = 0; index < score.Tracks.Count; index++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var track = score.Tracks[index];
			if (!track.Instrument.Enabled || track.Instrument.Gain == 0) { continue; }
			var part = new SoundFontPart(track.Instrument.Voice, track.Instrument.Bank, track.Instrument.Program, 100);
			ValidatePart(part);
			var position = index;
			var stem = await RenderPartAsync(template with { Notes = track.Notes }, sampleRate, frames, part,
				p => LayerProgressAsync(progress, track.Instrument.Name, position, score.Tracks.Count, p), cancellationToken).ConfigureAwait(false);
			Accumulate(mix, stem, track.Instrument.Gain);
			audible.AddRange(track.Notes);
		}
		var song = FinishLayers(template, mix, audible, score.Sections);
		if (progress is not null) { await progress(new("Complete", 1)).ConfigureAwait(false); }
		return new(song, new(_name, _hash, typeof(MeltySynth.Synthesizer).Assembly.GetName().Version!.ToString(), sampleRate,
			score.ComposerVersion, Array.AsReadOnly(score.Tracks.Select(t => t.Instrument).ToArray())));
	}

	private static Task LayerProgressAsync(Func<LayerRenderProgress, Task>? callback, string name,
		int index, int count, SoundFontRenderProgress part)
	{
		var local = Math.Clamp(part.Fraction * 6 - (int)part.Voice, 0, 1);
		return callback is null ? Task.CompletedTask : callback(new(name, (index + local) / count));
	}

	private static void Accumulate(double[] mix, double[] stem, double gain)
	{
		for (var frame = 0; frame < mix.Length; frame++) { mix[frame] += stem[frame] * gain; }
	}

	private static GeneratedSong FinishLayers(GeneratedSong template, double[] mix, List<NoteEvent> notes, IReadOnlyList<SectionEvent> sections)
	{
		var peak = mix.Select(Math.Abs).DefaultIfEmpty().Max();
		var gain = peak > 0 ? 0.9 / peak : 1;
		return template with
		{
			Samples = [.. mix.Select(s => (float)(s * gain))],
			NormalisationGain = gain,
			Notes = [.. notes.OrderBy(n => n.StartSeconds)],
			Sections = sections
		};
	}

	private static void ValidateScore(ArrangedScore score, int rate)
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
			ValidateMarker(chord.StartSeconds, chord.EndSeconds, score.Settings.Seconds);
		}
		foreach (var section in score.Sections)
		{
			ArgumentNullException.ThrowIfNull(section);
			ValidateMarker(section.StartSeconds, section.EndSeconds, score.Settings.Seconds);
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
	}

	private static void ValidateMarker(double start, double end, int seconds)
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
			ValidateNote(note);
			ValidateLayerNote(score, track.Instrument, note);
		}
	}

	private static void ValidateLayerNote(ArrangedScore score, InstrumentLayer layer, NoteEvent note)
	{
		if (note.Voice != layer.Voice || note.LayerId != layer.Id || note.Velocity is < 1 or > 127)
		{
			throw new ArgumentException("A note does not match its layer or velocity range.", nameof(score));
		}
		if (note.StartSeconds + note.DurationSeconds > score.Settings.Seconds + 1e-8)
		{
			throw new ArgumentException("A note does not match its layer or score duration.", nameof(score));
		}
	}
}
