using System.Text.Json;
using System.Text.Json.Nodes;
using PanoramicData.MusicGeneration.Demo.Pages;
using PanoramicData.MusicGeneration.SoundFonts;

namespace PanoramicData.MusicGeneration.Test;

public class StudioContractTests
{
	[Theory]
	[InlineData(false, 1)]
	[InlineData(true, 1)]
	[InlineData(false, 2)]
	[InlineData(true, 2)]
	public async Task OlderWrittenScore_ReplaysThroughTheActualDemoJsonContract(bool lowerCase, int composerVersion)
	{
		var score = ArrangementComposer.Compose(ArrangementPresets.ForGenre(MusicGenre.Pop) with { Seconds = 8, MelodySeed = 1234 });
		// Simulate earlier export shapes; written notes must survive without the newly introduced motif/seed metadata.
		var node = JsonSerializer.SerializeToNode(new StudioDocument { Settings = score.Settings, Score = score })!.AsObject();
		var oldScore = node["Score"]!.AsObject();
		oldScore["ComposerVersion"] = composerVersion;
		oldScore.Remove("Motifs");
		if (composerVersion == 1)
		{
			foreach (var key in new[] { "PerformedSeconds", "Ending", "Participation" }) { oldScore.Remove(key); }
			RemoveNewSettings(node["Settings"]!);
			RemoveNewSettings(oldScore["Settings"]!);
			foreach (var track in oldScore["Tracks"]!.AsArray()) { track!["Instrument"]!.AsObject().Remove("Presence"); }
		}
		else
		{
			node["Settings"]!.AsObject().Remove("MelodySeed");
			oldScore["Settings"]!.AsObject().Remove("MelodySeed");
		}
		var json = node.ToJsonString();
		if (lowerCase) { json = json.Replace("\"Settings\":", "\"settings\":", StringComparison.Ordinal).Replace("\"Tracks\":", "\"tracks\":", StringComparison.Ordinal); }
		var restored = JsonSerializer.Deserialize(StudioDocumentMigration.ExpandDefaults(json), StudioJsonContext.Default.StudioDocument)!;
		ArrangementComposer.Validate(restored.Settings);
		restored.Score!.ComposerVersion.Should().Be(composerVersion);
		restored.Score.Motifs.Should().BeEmpty();
		restored.Settings.MelodySeed.Should().BeNull();
		foreach (var track in score.Tracks) { restored.Score.Tracks.Single(t => t.Instrument.Id == track.Instrument.Id).Notes.Should().Equal(track.Notes); }
		restored.Score.Tracks.Select(t => t.Instrument).Should().Equal(restored.Score.Settings.Layers);
		var bank = new SoundFontRenderer(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "TestAssets", "GeneralUser-GS.sf2")), "Test bank");
		var result = await bank.RenderScoreAsync(restored.Score, 22050, null, TestContext.Current.CancellationToken);
		result.Song.Duration.TotalSeconds.Should().BeApproximately(composerVersion == 1 ? 8 : score.PerformedSeconds, 1.0 / 22050);
	}

	[Fact]
	public void MelodySeedAndMotifs_RoundTripThroughTheActualDemoContract()
	{
		var score = ArrangementComposer.Compose(ArrangementPresets.ForGenre(MusicGenre.Pop) with { Seconds = 30, MelodySeed = 1234 });
		var json = JsonSerializer.Serialize(new StudioDocument { Settings = score.Settings, Score = score }, StudioJsonContext.Default.StudioDocument);
		var restored = JsonSerializer.Deserialize(StudioDocumentMigration.ExpandDefaults(json), StudioJsonContext.Default.StudioDocument)!;
		restored.Settings.MelodySeed.Should().Be(1234);
		restored.Score!.ComposerVersion.Should().Be(3);
		foreach (var motif in score.Motifs) { restored.Score.Motifs.Single(m => m.Id == motif.Id).Notes.Should().Equal(motif.Notes); }
		foreach (var track in score.Tracks) { restored.Score.Tracks.Single(t => t.Instrument.Id == track.Instrument.Id).Notes.Should().Equal(track.Notes); }
	}

	[Fact]
	public void PartialPresence_RetainsExplicitZeroAndDefaultsMissingFieldsToAlwaysPresent()
	{
		var settings = ArrangementPresets.ForGenre(MusicGenre.Pop);
		var node = JsonSerializer.SerializeToNode(new StudioDocument { Settings = settings })!;
		var presence = node["Settings"]!["Layers"]![0]!["Presence"]!.AsObject();
		presence["Verse"] = 0;
		presence.Remove("Intro");
		presence.Remove("Overrides");
		var restored = JsonSerializer.Deserialize(StudioDocumentMigration.ExpandDefaults(node.ToJsonString()), StudioJsonContext.Default.StudioDocument)!;
		ArrangementComposer.Validate(restored.Settings);
		restored.Settings.Layers[0].Presence.Intro.Should().Be(1);
		restored.Settings.Layers[0].Presence.Verse.Should().Be(0);
		restored.Settings.Layers[0].Presence.Overrides.Should().BeEmpty();
	}

	[Fact]
	public void ExplicitNullAndInvalidNewValues_AreNotSilentlyReplaced()
	{
		var settings = ArrangementPresets.ForGenre(MusicGenre.Pop) with { Endings = null! };
		var json = JsonSerializer.Serialize(new StudioDocument { Settings = settings });
		var restored = JsonSerializer.Deserialize(StudioDocumentMigration.ExpandDefaults(json), StudioJsonContext.Default.StudioDocument)!;
		Assert.Throws<ArgumentNullException>(() => ArrangementComposer.Validate(restored.Settings));
	}

	private static void RemoveNewSettings(JsonNode node)
	{
		var settings = node.AsObject();
		foreach (var key in new[] { "Endings", "ProgressionVariation", "SectionContrast", "IntroVariation", "MelodySeed" }) { settings.Remove(key); }
		foreach (var layer in settings["Layers"]!.AsArray()) { layer!.AsObject().Remove("Presence"); }
	}
}
