using System.IO.Compression;
using System.Text;
using System.Text.Json;
using PanoramicData.MusicGeneration.Demo.Pages;
using PanoramicData.MusicGeneration.SoundFonts;

namespace PanoramicData.MusicGeneration.Test;

public class StudioSongLinkTests
{
	[Theory]
	[InlineData(null)]
	[InlineData(19876)]
	public async Task SharedSettings_ReproduceTheWrittenScoreAndAudio(int? melodySeed)
	{
		var preset = ArrangementPresets.ForGenre(MusicGenre.Ragtime);
		var layer = preset.Layers[0] with
		{
			Id = "right & left / ♯",
			Name = "Piano & café",
			Gain = 0.23,
			PianoTwoHands = false,
			Presence = new() { Verse = 0.25, Overrides = [new(SectionKind.Verse, 3, 0.75)] }
		};
		var settings = preset with
		{
			Seed = -123,
			MelodySeed = melodySeed,
			Seconds = 8,
			DrumFills = 0.85,
			Layers = [layer, .. preset.Layers.Skip(1)]
		};
		var token = StudioSongLink.Encode("Café ♯ & friends", settings);
		var restored = StudioSongLink.Decode(token);
		restored.Title.Should().Be("Café ♯ & friends");
		JsonSerializer.Serialize(restored.Settings).Should().Be(JsonSerializer.Serialize(settings));
		token.Should().Be(StudioSongLink.Encode(restored.Title, restored.Settings));
		var before = ArrangementComposer.Compose(settings);
		var after = ArrangementComposer.Compose(restored.Settings);
		JsonSerializer.Serialize(after).Should().Be(JsonSerializer.Serialize(before));
		var bank = new SoundFontRenderer(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "TestAssets", "GeneralUser-GS.sf2")), "Test bank");
		var original = await bank.RenderScoreAsync(before, 22050, null, TestContext.Current.CancellationToken);
		var replay = await bank.RenderScoreAsync(after, 22050, null, TestContext.Current.CancellationToken);
		replay.Song.ToWav().Should().Equal(original.Song.ToWav());
	}

	[Theory]
	[InlineData(24)]
	[InlineData(100)]
	public void EntireLayerConfiguration_IsPreserved(int overrides)
	{
		var settings = ArrangementPresets.ForGenre(MusicGenre.Pop) with
		{
			Layers = [.. Enumerable.Range(0, 24).Select(i => new InstrumentLayer { Id = $"layer-{i}", Program = i, Octave = -1,
				Enabled = i % 2 == 0, Velocity = i + 1, Gain = i / 24.0,
				Presence = new() { Overrides = [.. Enumerable.Range(1, overrides).Select(n => new SectionPresenceOverride(SectionKind.Chorus, n, 0.25))] } })]
		};
		var restored = StudioSongLink.Decode(StudioSongLink.Encode("Full ensemble", settings));
		JsonSerializer.Serialize(restored.Settings).Should().Be(JsonSerializer.Serialize(settings));
	}

	[Theory]
	[InlineData("")]
	[InlineData("a")]
	[InlineData("broken%link")]
	[InlineData("e30")]
	public void DamagedLinks_AreRejected(string token) => Assert.Throws<ArgumentException>(() => StudioSongLink.Decode(token));

	[Fact]
	public void UnknownVersionsAndBanks_AreRejectedRatherThanRecomposed()
	{
		var original = StudioSongLink.Decode(StudioSongLink.Encode("Test", ArrangementPresets.ForGenre(MusicGenre.Pop)));
		foreach (var changed in new[] { original with { LinkVersion = 2 }, original with { ComposerVersion = -1 },
			original with { PlaybackVersion = 99 }, original with { BankSha256 = "unknown" } })
		{
			Assert.Throws<ArgumentException>(() => StudioSongLink.Decode(Pack(JsonSerializer.Serialize(changed, SongLinkJsonContext.Default.SharedSong))));
		}
	}

	[Fact]
	public void OversizedAndCompressedExpansionLinks_AreBounded()
	{
		Assert.Throws<ArgumentException>(() => StudioSongLink.Decode(new('a', StudioSongLink.MaxTokenLength + 1)));
		var compressedBomb = Pack(new(' ', 300000));
		compressedBomb.Length.Should().BeLessThan(StudioSongLink.MaxTokenLength);
		Assert.Throws<ArgumentException>(() => StudioSongLink.Decode(compressedBomb));
	}

	private static string Pack(string json)
	{
		using var output = new MemoryStream();
		using (var zip = new GZipStream(output, CompressionLevel.SmallestSize, true)) { zip.Write(Encoding.UTF8.GetBytes(json)); }
		return Convert.ToBase64String(output.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
	}
}
