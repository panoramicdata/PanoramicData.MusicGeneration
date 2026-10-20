namespace PanoramicData.MusicGeneration;

/// <summary>Ways to close an arrangement.</summary>
public enum EndingKind
{
	/// <summary>Repeat the chorus hook and fade its audio to silence.</summary>
	Fade,
	/// <summary>Dominant to tonic, followed by a held final chord.</summary>
	Cadence,
	/// <summary>Repeat a short closing hook, then land on tonic.</summary>
	Tag,
	/// <summary>A coordinated final hit and a release into silence.</summary>
	Stop
}

/// <summary>Nonnegative relative weights; at least one must be positive. Defaults are design choices, not measured hit frequencies.</summary>
public sealed record EndingWeights
{
	/// <summary>Repeated fade weight.</summary>
	public double Fade { get; init; } = 0.35;
	/// <summary>Held cadence weight.</summary>
	public double Cadence { get; init; } = 0.35;
	/// <summary>Closing tag weight.</summary>
	public double Tag { get; init; } = 0.2;
	/// <summary>Coordinated stop weight.</summary>
	public double Stop { get; init; } = 0.1;
}

/// <summary>A probability override for a one-based occurrence of a section type.</summary>
public sealed record SectionPresenceOverride(SectionKind Kind, int Occurrence, double Probability);

/// <summary>A layer's probability of taking part in each entire section, from zero to one.</summary>
public sealed record LayerPresence
{
	/// <summary>Intro probability.</summary>
	public double Intro { get; init; } = 1;
	/// <summary>Verse probability.</summary>
	public double Verse { get; init; } = 1;
	/// <summary>Chorus probability.</summary>
	public double Chorus { get; init; } = 1;
	/// <summary>Bridge probability.</summary>
	public double Bridge { get; init; } = 1;
	/// <summary>Outro probability.</summary>
	public double Outro { get; init; } = 1;
	/// <summary>Specific occurrence overrides, applied before the seeded decision.</summary>
	public IReadOnlyList<SectionPresenceOverride> Overrides { get; init; } = [];

	/// <summary>Compares probabilities and override contents, including after JSON round trips.</summary>
	public bool Equals(LayerPresence? other)
	{
		if (other is null) { return false; }
		return Intro.Equals(other.Intro) && Verse.Equals(other.Verse) && Chorus.Equals(other.Chorus) &&
			Bridge.Equals(other.Bridge) && Outro.Equals(other.Outro) && Overrides.SequenceEqual(other.Overrides);
	}

	/// <inheritdoc />
	public override int GetHashCode()
	{
		var hash = new HashCode();
		hash.Add(Intro); hash.Add(Verse); hash.Add(Chorus); hash.Add(Bridge); hash.Add(Outro);
		foreach (var item in Overrides) { hash.Add(item); }
		return hash.ToHashCode();
	}
}

/// <summary>Inspectable seeded section-level performance decision.</summary>
public sealed record LayerParticipation(string LayerId, int SectionIndex, int Occurrence, double Probability, bool Present);

/// <summary>Written ending boundaries; audio release is included in the score's performed duration.</summary>
public sealed record EndingPlan(EndingKind Kind, double StartSeconds, double EndSeconds, double ReleaseSeconds);
