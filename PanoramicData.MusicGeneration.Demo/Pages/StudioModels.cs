using System.Text.Json.Serialization;
using PanoramicData.MusicGeneration.SoundFonts;

namespace PanoramicData.MusicGeneration.Demo.Pages;

public sealed record StudioDocument
{
	public int SchemaVersion { get; init; } = 1;
	public string Title { get; init; } = "My song";
	public required ArrangementSettings Settings { get; init; }
	public ArrangedScore? Score { get; init; }
	public string? BankSha256 { get; init; }
}

public sealed record SavedSongHeader(string Id, string Title, string SavedUtc);

internal sealed class LayerEditor
{
	public LayerEditor(InstrumentLayer layer)
	{
		Id = layer.Id; Name = layer.Name; Pattern = layer.Pattern; Bank = layer.Bank;
		Program = layer.Program; Velocity = layer.Velocity; Gain = layer.Gain; Octave = layer.Octave; Enabled = layer.Enabled;
		Presence = new(layer.Presence);
		PianoTwoHands = layer.PianoTwoHands;
	}
	public string Id { get; set; }
	public string Name { get; set; }
	public LayerPattern Pattern { get; set; }
	public int Bank { get; set; }
	public int Program { get; set; }
	public int Velocity { get; set; }
	public double Gain { get; set; }
	public int Octave { get; set; }
	public bool Enabled { get; set; }
	public bool PianoTwoHands { get; set; }
	public PresenceEditor Presence { get; }
	public int Preset { get => Bank * 128 + Program; set { Bank = value / 128; Program = value % 128; } }
	public InstrumentLayer ToLayer() => new()
	{
		Id = Id,
		Name = Name,
		Pattern = Pattern,
		Bank = Bank,
		Program = Program,
		Velocity = Velocity,
		Gain = Gain,
		Octave = Octave,
		Enabled = Enabled,
		PianoTwoHands = PianoTwoHands,
		Presence = Presence.ToPresence()
	};
}

internal sealed class PresenceEditor(LayerPresence presence)
{
	public double Intro { get; set; } = presence.Intro;
	public double Verse { get; set; } = presence.Verse;
	public double Chorus { get; set; } = presence.Chorus;
	public double Bridge { get; set; } = presence.Bridge;
	public double Outro { get; set; } = presence.Outro;
	public List<PresenceOverrideEditor> Overrides { get; } = [.. presence.Overrides.Select(o => new PresenceOverrideEditor { Kind = o.Kind, Occurrence = o.Occurrence, Probability = o.Probability })];
	public LayerPresence ToPresence() => new()
	{
		Intro = Intro,
		Verse = Verse,
		Chorus = Chorus,
		Bridge = Bridge,
		Outro = Outro,
		Overrides = [.. Overrides.Select(o => new SectionPresenceOverride(o.Kind, o.Occurrence, o.Probability))]
	};
}

internal sealed class PresenceOverrideEditor
{
	public SectionKind Kind { get; set; } = SectionKind.Verse;
	public int Occurrence { get; set; } = 3;
	public double Probability { get; set; } = 0.25;
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(StudioDocument))]
[JsonSerializable(typeof(List<SavedSongHeader>))]
[JsonSerializable(typeof(LayeredRenderMetadata))]
internal sealed partial class StudioJsonContext : JsonSerializerContext;
