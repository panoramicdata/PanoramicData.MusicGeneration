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
		LayeredScoreValidator.Validate(score, sampleRate);
		var info = new SongInfo(score.Settings.Seed, score.Settings.Tempo, score.Settings.Minor ? "Minor" : "Major", 36 + score.Settings.Key, 0, 0);
		var frames = checked((int)Math.Round(ArrangementEndingRenderer.Duration(score) * sampleRate));
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
		ArrangementEndingRenderer.Shape(mix, sampleRate, score.Ending);
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
}
