using System.Text.Json;
using PanoramicData.MusicGeneration.SoundFonts;

namespace PanoramicData.MusicGeneration.Test;

public class SoundFontTests
{
	private static readonly Lazy<SoundFontRenderer> Bank = new(() => new(
		File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "TestAssets", "GeneralUser-GS.sf2")), "GeneralUser GS 2.0.3"));

	[Fact]
	public async Task Rendering_IsRepeatableAndPreservesTheScoreAndRawStemMix()
	{
		var score = Score();
		var first = await RenderAsync(score);
		var second = await RenderAsync(score);
		first.Song.Notes.Should().Equal(score.Notes);
		first.Song.Sections.Should().Equal(score.Sections);
		first.Song.Info.Should().Be(score.Info);
		first.Song.Samples.Should().Equal(second.Song.Samples);
		first.Song.SampleRate.Should().Be(22050);
		first.Song.Samples.Length.Should().Be(2 * 22050);
		first.Song.Samples.Should().OnlyContain(s => float.IsFinite(s));
		first.Song.Samples.Select(Math.Abs).Max().Should().BeApproximately(0.9f, 1e-6f);
		foreach (var stem in first.Song.Stems.Values)
		{
			stem.Should().Contain(s => s != 0);
		}
		for (var i = 0; i < first.Song.Samples.Length; i += 97)
		{
			var mixed = (float)(first.Song.Stems.Values.Sum(s => s[i]) * first.Song.NormalisationGain);
			first.Song.Samples[i].Should().Be(mixed);
		}
	}

	[Fact]
	public async Task ChangingTheMelodyInstrument_LeavesOtherStemsAndAllEventsUnchanged()
	{
		var score = Score();
		var original = await RenderAsync(score);
		var options = new SoundFontRenderOptions();
		options = options with { Parts = [.. options.Parts.Select(p => p.Voice == VoiceKind.Melody ? p with { Program = 73 } : p)] };
		var changed = await RenderAsync(score, options);
		changed.Song.Notes.Should().Equal(original.Song.Notes);
		changed.Song.Stems[VoiceKind.Melody].Should().NotEqual(original.Song.Stems[VoiceKind.Melody]);
		foreach (var voice in Enum.GetValues<VoiceKind>().Where(v => v != VoiceKind.Melody))
		{
			changed.Song.Stems[voice].Should().Equal(original.Song.Stems[voice]);
		}
	}

	[Fact]
	public async Task Metadata_IdentifiesTheAssetRendererAndFrozenAssignments()
	{
		var parts = new SoundFontRenderOptions().Parts.ToArray();
		var result = await RenderAsync(Score(), new() { Parts = parts });
		parts[0] = parts[0] with { Program = 0 };
		result.Metadata.Options.Parts[0].Program.Should().Be(48);
		result.Metadata.BankSha256.Should().Be("9575028C7A1F589F5770FCCC8CFF2734566AF40CD26ED836944E9A5152688CFE");
		result.Metadata.RendererVersion.Should().StartWith("2.4.1");
		result.Metadata.EffectsEnabled.Should().BeFalse();
		var restored = JsonSerializer.Deserialize<SoundFontRenderMetadata>(JsonSerializer.Serialize(result.Metadata));
		restored!.Options.Parts.Should().Equal(result.Metadata.Options.Parts);
		restored.BankSha256.Should().Be(result.Metadata.BankSha256);
	}

	[Fact]
	public async Task MissingPresetsAndDuplicateRoles_FailBeforeRendering()
	{
		var options = new SoundFontRenderOptions();
		var missing = options with { Parts = [.. options.Parts.Select(p => p with { Bank = 127 })] };
		await Assert.ThrowsAsync<ArgumentException>(() => RenderAsync(Score(), missing));
		var duplicate = options with { Parts = [.. Enumerable.Repeat(options.Parts[0], 6)] };
		await Assert.ThrowsAsync<ArgumentException>(() => RenderAsync(Score(), duplicate));
		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => RenderAsync(Score(), options with { SampleRate = 8000 }));
	}

	[Fact]
	public void MissingOrMalformedAssets_FailClearly()
	{
		Assert.Throws<ArgumentNullException>(() => new SoundFontRenderer(null!, "missing"));
		Assert.Throws<ArgumentException>(() => new SoundFontRenderer([], "empty"));
		Assert.Throws<InvalidDataException>(() => new SoundFontRenderer(new byte[32], "invalid.sf2"));
	}

	[Fact]
	public async Task ProgressCompletesAndCancellationIsObserved()
	{
		var fractions = new List<double>();
		await RenderAsync(Score(), progress: p => { fractions.Add(p.Fraction); return Task.CompletedTask; });
		fractions.Should().BeInAscendingOrder();
		fractions[^1].Should().Be(1);
		using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Bank.Value.RenderAsync(Score(), options: null, progress: _ =>
		{
			cancellation.Cancel();
			return Task.CompletedTask;
		}, cancellationToken: cancellation.Token));
	}

	[Fact]
	public async Task EventsRespectStartTimeAndDuration_WithIndependentOverlappingKeys()
	{
		var score = Score() with
		{
			Notes = [new(VoiceKind.Melody, 0.5, 0.8, 60, Pitch.FrequencyOf(60)), new(VoiceKind.Melody, 0.6, 0.8, 60, Pitch.FrequencyOf(60))]
		};
		var result = await RenderAsync(score);
		result.Song.Stems[VoiceKind.Melody].Take(11025).Should().OnlyContain(s => s == 0);
		result.Song.Stems[VoiceKind.Melody].Skip(11025).Should().Contain(s => s != 0);
		result.Song.Stems.Where(s => s.Key != VoiceKind.Melody).SelectMany(s => s.Value).Should().OnlyContain(s => s == 0);
		result.Song.Duration.Should().Be(score.Duration);
	}

	private static Task<SoundFontRenderResult> RenderAsync(GeneratedSong score, SoundFontRenderOptions? options = null, Func<SoundFontRenderProgress, Task>? progress = null) =>
		Bank.Value.RenderAsync(score, options, progress, TestContext.Current.CancellationToken);

	private static GeneratedSong Score()
	{
		var notes = Enum.GetValues<VoiceKind>().Select(v => new NoteEvent(v, 0.1, 0.6,
			v switch { VoiceKind.Kick => 36, VoiceKind.Snare => 38, VoiceKind.HiHat => 42, _ => 60 }, 261.625565)).ToArray();
		return new(new SongInfo(42, 120, "Major", 36, 0, 0), new float[16000], 8000, notes)
		{
			Sections = [new() { Kind = SectionKind.Verse, EndSeconds = 2, PlannedEndSeconds = 2 }]
		};
	}
}
