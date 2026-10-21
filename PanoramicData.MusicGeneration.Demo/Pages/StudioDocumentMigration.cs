using System.Text.Json;
using System.Text.Json.Nodes;

namespace PanoramicData.MusicGeneration.Demo.Pages;

// Source-generated init-only record construction does not retain every initializer for absent JSON members.
// Expand optional fields explicitly before deserializing older exports; explicit null values still fail validation.
internal static class StudioDocumentMigration
{
	public static string ExpandDefaults(string json)
	{
		var document = JsonNode.Parse(json, new() { PropertyNameCaseInsensitive = true }) as JsonObject
			?? throw new JsonException("Expected a song/settings object.");
		SettingsDefaults(document["Settings"]);
		if (document["Score"] is JsonObject score)
		{
			SettingsDefaults(score["Settings"]);
			Add(score, "ComposerVersion", JsonValue.Create(1)!);
			Add(score, "Participation", new JsonArray());
			Add(score, "Motifs", new JsonArray());
			if (score["Tracks"] is JsonArray tracks)
			{
				foreach (var track in tracks) { InstrumentDefaults((track as JsonObject)?["Instrument"]); }
			}
		}
		return document.ToJsonString();
	}

	private static void SettingsDefaults(JsonNode? node)
	{
		if (node is not JsonObject settings) { return; }
		Add(settings, "ProgressionVariation", JsonValue.Create(0.35)!);
		Add(settings, "SectionContrast", JsonValue.Create(1.0)!);
		Add(settings, "IntroVariation", JsonValue.Create(0.65)!);
		Add(settings, "Endings", new JsonObject { ["Fade"] = 0.35, ["Cadence"] = 0.35, ["Tag"] = 0.2, ["Stop"] = 0.1 });
		if (settings["Endings"] is JsonObject ending)
		{
			Add(ending, "Fade", JsonValue.Create(0.35)!);
			Add(ending, "Cadence", JsonValue.Create(0.35)!);
			Add(ending, "Tag", JsonValue.Create(0.2)!);
			Add(ending, "Stop", JsonValue.Create(0.1)!);
		}
		if (settings["Layers"] is JsonArray layers)
		{
			foreach (var layer in layers) { InstrumentDefaults(layer); }
		}
	}

	private static void InstrumentDefaults(JsonNode? node)
	{
		if (node is not JsonObject instrument) { return; }
		Add(instrument, "Presence", new JsonObject
		{
			["Intro"] = 1,
			["Verse"] = 1,
			["Chorus"] = 1,
			["Bridge"] = 1,
			["Outro"] = 1,
			["Overrides"] = new JsonArray()
		});
		if (instrument["Presence"] is JsonObject presence)
		{
			foreach (var kind in Enum.GetNames<SectionKind>()) { Add(presence, kind, JsonValue.Create(1.0)!); }
			Add(presence, "Overrides", new JsonArray());
		}
	}

	private static void Add(JsonObject node, string key, JsonNode value)
	{
		if (!node.ContainsKey(key)) { node.Add(key, value); }
	}
}
