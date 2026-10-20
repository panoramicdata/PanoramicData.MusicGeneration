using System.Text.Json;
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
		var restored = JsonSerializer.Deserialize<ArrangedScore>(JsonSerializer.Serialize(score))!;
		var second = await RenderAsync(restored);
		first.Song.Samples.Should().Equal(second.Song.Samples);
		first.Song.Notes.Should().BeEquivalentTo(score.Tracks.SelectMany(t => t.Notes));
		first.Metadata.Layers.Should().Equal(score.Settings.Layers);
		first.Song.Duration.TotalSeconds.Should().BeApproximately(score.PerformedSeconds, 1.0 / 22050);
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
		var muted = settings.Layers.Single(l => l.Pattern == LayerPattern.Melody).Id;
		result.Song.Notes.Should().NotContain(n => n.LayerId == muted);
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
	public async Task FadeEnding_ChangesTheAudioEnvelopeAndReleasesToExactSilence()
	{
		var layer = new InstrumentLayer { Id = "held", Pattern = LayerPattern.Chords, Program = 48 };
		var settings = new ArrangementSettings { Seconds = 12, Layers = [layer] };
		var score = new ArrangedScore
		{
			Settings = settings,
			Tracks = [new(layer, [new(VoiceKind.Pad, 0, 11, 60, Pitch.FrequencyOf(60)) { LayerId = layer.Id }])],
			Chords = [new("C", 0, [0, 4, 7], 0, 11.5)],
			Sections = [new() { Kind = SectionKind.Outro, StartSeconds = 0, EndSeconds = 11.5 }],
			PerformedSeconds = 11.85,
			Ending = new(EndingKind.Fade, 0, 11.5, 0.35)
		};
		var fade = await RenderAsync(score);
		var held = await RenderAsync(score with { Ending = score.Ending with { Kind = EndingKind.Cadence } });
		var start = 8 * 22050;
		var fadingEnergy = fade.Song.Samples.Skip(start).Take(22050).Average(s => (double)s * s);
		var heldEnergy = held.Song.Samples.Skip(start).Take(22050).Average(s => (double)s * s);
		fadingEnergy.Should().BeLessThan(heldEnergy * 0.02);
		fade.Song.Samples[^1].Should().Be(0);
		held.Song.Samples[^1].Should().Be(0);
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
