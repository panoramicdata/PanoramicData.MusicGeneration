using PanoramicData.MusicGeneration.SoundFonts;

namespace PanoramicData.MusicGeneration.Test;

public class LayeredRenderTests
{
	private static readonly Lazy<SoundFontRenderer> Bank = new(() => new(
		File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "TestAssets", "GeneralUser-GS.sf2")), "GeneralUser GS 2.0.3"));

	[Fact]
	public async Task ArbitraryLayers_RenderTheSavedScoreRepeatablyWithLayerIdentity()
	{
		var score = ArrangementComposer.Compose(ArrangementPresets.ForGenre(MusicGenre.Classical) with { Seconds = 6 });
		var first = await RenderAsync(score);
		var second = await RenderAsync(score);
		first.Song.Samples.Should().Equal(second.Song.Samples);
		first.Song.Notes.Should().BeEquivalentTo(score.Tracks.SelectMany(t => t.Notes));
		first.Metadata.Layers.Should().Equal(score.Settings.Layers);
		first.Song.Duration.TotalSeconds.Should().Be(6);
		first.Song.Samples.Should().OnlyContain(s => float.IsFinite(s));
		first.Song.Samples.Should().Contain(s => s != 0);
		first.Song.Stems.Should().BeEmpty("layers are mixed sequentially rather than retaining every raw buffer");
	}

	[Fact]
	public async Task MutedLayers_KeepTheirWrittenNotesButAreAbsentFromTheAudibleScore()
	{
		var settings = ArrangementPresets.ForGenre(MusicGenre.Pop) with { Seconds = 6 };
		settings = settings with { Layers = [.. settings.Layers.Select(l => l.Pattern == LayerPattern.Melody ? l with { Enabled = false } : l)] };
		var score = ArrangementComposer.Compose(settings);
		score.Tracks.Single(t => t.Instrument.Pattern == LayerPattern.Melody).Notes.Should().NotBeEmpty();
		var result = await RenderAsync(score);
		result.Song.Notes.Should().NotContain(n => n.Voice == VoiceKind.Melody);
	}

	[Fact]
	public async Task ImportedScore_RejectsDuplicateTracksAndMismatchedNoteIdentity()
	{
		var score = ArrangementComposer.Compose(ArrangementPresets.ForGenre(MusicGenre.Pop) with { Seconds = 6 });
		var duplicated = score with { Tracks = [.. score.Tracks.SkipLast(1), score.Tracks[0]] };
		await Assert.ThrowsAsync<ArgumentException>(() => RenderAsync(duplicated));
		var first = score.Tracks[0];
		var wrong = first with { Notes = [first.Notes[0] with { LayerId = "unknown" }] };
		await Assert.ThrowsAsync<ArgumentException>(() => RenderAsync(score with { Tracks = [wrong, .. score.Tracks.Skip(1)] }));
	}

	[Fact]
	public async Task WrittenScore_ReplaysAnEarlierComposerButRejectsAnUnknownFileSchema()
	{
		var score = ArrangementComposer.Compose(ArrangementPresets.ForGenre(MusicGenre.Pop) with { Seconds = 6 });
		var earlier = await RenderAsync(score with { ComposerVersion = 0 });
		earlier.Metadata.CompositionVersion.Should().Be(0);
		earlier.Song.Notes.Should().BeEquivalentTo(score.Tracks.SelectMany(t => t.Notes));
		await Assert.ThrowsAsync<ArgumentException>(() => RenderAsync(score with { Version = 999 }));
	}

	[Fact]
	public async Task EventVelocity_ChangesTheRawSampledPerformance()
	{
		var score = new GeneratedSong(new(0, 120, "Major", 36, 0, 0), new float[16000], 16000,
			[new(VoiceKind.Melody, 0, 0.8, 60, Pitch.FrequencyOf(60)) { Velocity = 30 }]);
		var quiet = await Bank.Value.RenderAsync(score, null, null, TestContext.Current.CancellationToken);
		var loud = await Bank.Value.RenderAsync(score with { Notes = [score.Notes[0] with { Velocity = 120 }] }, null, null, TestContext.Current.CancellationToken);
		loud.Song.Stems[VoiceKind.Melody].Select(Math.Abs).Max().Should().BeGreaterThan(quiet.Song.Stems[VoiceKind.Melody].Select(Math.Abs).Max());
	}

	private static Task<LayeredRenderResult> RenderAsync(ArrangedScore score) =>
		Bank.Value.RenderScoreAsync(score, 22050, null, TestContext.Current.CancellationToken);
}
