namespace PanoramicData.MusicGeneration;

/// <summary>The role of a section within a composition.</summary>
public enum SectionKind
{
	/// <summary>Opening material.</summary>
	Intro,
	/// <summary>A verse.</summary>
	Verse,
	/// <summary>A returning chorus.</summary>
	Chorus,
	/// <summary>Contrasting bridge material.</summary>
	Bridge,
	/// <summary>Closing material.</summary>
	Outro
}

/// <summary>How a section plan uses the requested audio duration.</summary>
public enum FormDurationMode
{
	/// <summary>Keep the specified bar counts; crop excess material or leave silence after the completed form.</summary>
	Truncate,
	/// <summary>Distribute available whole bars by section weight; short requests keep a prefix of the form.</summary>
	FitWholeBars,
	/// <summary>Fit whole-bar phrases, preserving phrase counts and returning material lengths; crop a short request's prefix.</summary>
	FitWholePhrases
}

/// <summary>One section occurrence; matching material identifiers reuse the same underlying notes.</summary>
public sealed record SongSection
{
	/// <summary>Musical role of this occurrence.</summary>
	public SectionKind Kind { get; init; } = SectionKind.Verse;

	/// <summary>Stable, case-sensitive identity of the musical material.</summary>
	public string MaterialId { get; init; } = "A";

	/// <summary>Length of each reusable phrase in four-beat bars; also its fitting weight and minimum phrase length.</summary>
	public int Bars { get; init; } = 2;

	/// <summary>Number of phrase repeats. Use Truncate or FitWholePhrases to preserve this count. Default one.</summary>
	public int PhraseCount { get; init; } = 1;

	/// <summary>Melody octave shift, folded back into the style's melody range. Default zero.</summary>
	public int MelodyOctaveOffset { get; init; }

	/// <summary>Fraction of the underlying melody retained, from zero to one. Default one.</summary>
	public double MelodyDensity { get; init; } = 1;

	/// <summary>Allowed voices for this occurrence, intersected with SongStyle switches; null allows all.</summary>
	public IReadOnlyList<VoiceKind>? Voices { get; init; }

	/// <summary>Optional melody pickup before the section starts, from zero to one beat.</summary>
	public double PickupBeats { get; init; }

	/// <summary>Replace the final beat's drums with a short snare fill at a complete transition.</summary>
	public bool TransitionFill { get; init; }
}

/// <summary>A serialisable composition plan, independent of the rendered audio and mute state.</summary>
public sealed record SongForm
{
	/// <summary>Ordered section occurrences, from one to 64.</summary>
	public IReadOnlyList<SongSection> Sections { get; init; } = [];

	/// <summary>Duration policy; truncate by default.</summary>
	public FormDurationMode DurationMode { get; init; }

	/// <summary>End a completed form with a tonic chord and stop; a cropped form has no forced cadence.</summary>
	public bool EndWithCadence { get; init; } = true;

	/// <summary>Checks section lengths, identities, variations and the duration policy.</summary>
	public void Validate()
	{
		if (Sections is not { Count: > 0 and <= 64 } || !Enum.IsDefined(DurationMode))
		{
			throw new ArgumentException("A form needs 1 to 64 sections and a valid duration mode.");
		}

		var lengths = new Dictionary<string, (int Bars, int Phrases)>(StringComparer.Ordinal);
		foreach (var section in Sections)
		{
			ValidateSection(section);
			if (lengths.TryGetValue(section.MaterialId, out var length) && length != (section.Bars, section.PhraseCount))
			{
				throw new ArgumentException("Occurrences of one material identity must have the same phrase length and count.");
			}
			lengths[section.MaterialId] = (section.Bars, section.PhraseCount);
		}
	}

	private static void ValidateSection(SongSection section)
	{
		ArgumentNullException.ThrowIfNull(section);
		ValidateMaterial(section);
		ValidateUnitInterval(section.MelodyDensity, nameof(section.MelodyDensity));
		ValidateUnitInterval(section.PickupBeats, nameof(section.PickupBeats));
		if (section.MelodyOctaveOffset is < -2 or > 2 || section.Voices?.Any(v => !Enum.IsDefined(v)) == true)
		{
			throw new ArgumentException("The octave shift must be -2 to 2, and voices must be valid.");
		}
	}

	private static void ValidateMaterial(SongSection section)
	{
		if (!Enum.IsDefined(section.Kind) || string.IsNullOrWhiteSpace(section.MaterialId) || section.MaterialId.Length > 80
			|| section.Bars is < 1 or > 64 || section.PhraseCount is < 1 or > 16)
		{
			throw new ArgumentException("A section needs a valid kind, an identity of at most 80 characters, 1 to 64 bars per phrase and 1 to 16 phrases.");
		}
	}

	private static void ValidateUnitInterval(double value, string name)
	{
		if (!double.IsFinite(value) || value is < 0 or > 1)
		{
			throw new ArgumentException("The value must be finite and between zero and one.", name);
		}
	}
}

/// <summary>Original, reusable form examples.</summary>
public static class SongForms
{
	/// <summary>Intro, A-B-A-B-C-B, outro, with four phrases in every verse and chorus; fitted in whole-bar phrases.</summary>
	public static SongForm VerseChorus => new()
	{
		DurationMode = FormDurationMode.FitWholePhrases,
		Sections =
		[
			new() { Kind = SectionKind.Intro, MaterialId = "intro", Bars = 1, Voices = [VoiceKind.Pad, VoiceKind.Bass] },
			new() { Kind = SectionKind.Verse, MaterialId = "A", PhraseCount = 4, MelodyDensity = 0.75, TransitionFill = true },
			new() { Kind = SectionKind.Chorus, MaterialId = "B", PhraseCount = 4, MelodyOctaveOffset = 1, PickupBeats = 0.5, TransitionFill = true },
			new() { Kind = SectionKind.Verse, MaterialId = "A", PhraseCount = 4, MelodyDensity = 0.75, TransitionFill = true },
			new() { Kind = SectionKind.Chorus, MaterialId = "B", PhraseCount = 4, MelodyOctaveOffset = 1, PickupBeats = 0.5, TransitionFill = true },
			new() { Kind = SectionKind.Bridge, MaterialId = "C", Voices = [VoiceKind.Pad, VoiceKind.Melody], TransitionFill = true },
			new() { Kind = SectionKind.Chorus, MaterialId = "B", PhraseCount = 4, MelodyOctaveOffset = 1, PickupBeats = 0.5 },
			new() { Kind = SectionKind.Outro, MaterialId = "outro", Bars = 1 }
		]
	};
}

/// <summary>One performed section boundary, including its planned length and transition metadata.</summary>
public sealed record SectionEvent
{
	/// <summary>Zero-based index into the source form.</summary>
	public int Index { get; init; }

	/// <summary>Section role.</summary>
	public SectionKind Kind { get; init; }

	/// <summary>Identity of the repeated material.</summary>
	public string MaterialId { get; init; } = string.Empty;

	/// <summary>Start of the section body, in seconds.</summary>
	public double StartSeconds { get; init; }

	/// <summary>Audible end, clipped to the requested duration.</summary>
	public double EndSeconds { get; init; }

	/// <summary>End before clipping to the requested duration.</summary>
	public double PlannedEndSeconds { get; init; }

	/// <summary>Assigned whole bars after the duration policy is applied.</summary>
	public int Bars { get; init; }

	/// <summary>Number of planned phrase repeats; cropping can make the last repeat incomplete.</summary>
	public int PhraseCount { get; init; } = 1;

	/// <summary>Whole bars in each reusable phrase.</summary>
	public int PhraseBars { get; init; } = 1;

	/// <summary>Earliest permitted pickup start, in seconds.</summary>
	public double PickupStartSeconds { get; init; }

	/// <summary>Whether the request cuts this section short.</summary>
	public bool IsTruncated => EndSeconds < PlannedEndSeconds;

	/// <summary>Whether the complete final section ends with a tonic cadence.</summary>
	public bool HasCadence { get; init; }

	/// <summary>Whether this complete section has a transition fill.</summary>
	public bool HasTransitionFill { get; init; }
}
