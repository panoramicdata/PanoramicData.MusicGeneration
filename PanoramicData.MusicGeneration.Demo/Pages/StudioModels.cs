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
		Enabled = Enabled
	};
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(StudioDocument))]
[JsonSerializable(typeof(List<SavedSongHeader>))]
[JsonSerializable(typeof(LayeredRenderMetadata))]
internal sealed partial class StudioJsonContext : JsonSerializerContext;
