using System.Text.Json.Serialization;

namespace PanoramicData.MusicGeneration.Demo.Pages;

internal sealed record CompositionRuleSet(string Version, string Revised, string ThesisRevision, CompositionRule[] Rules);

internal sealed class CompositionRule
{
	public string Id { get; init; } = string.Empty;
	public string Title { get; init; } = string.Empty;
	public string Principle { get; init; } = string.Empty;
	public string Rationale { get; init; } = string.Empty;
	public string Exception { get; init; } = string.Empty;
	public string Status { get; init; } = string.Empty;
	public string Implementation { get; init; } = string.Empty;
	public string CurrentLevers { get; init; } = string.Empty;
	public string PlannedLevers { get; init; } = string.Empty;
	public int Issue { get; init; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CompositionRuleSet))]
internal sealed partial class CompositionRulesJsonContext : JsonSerializerContext;
