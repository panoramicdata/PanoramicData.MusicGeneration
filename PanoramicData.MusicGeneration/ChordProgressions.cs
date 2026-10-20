using System.Text.RegularExpressions;

namespace PanoramicData.MusicGeneration;

/// <summary>A chord symbol resolved to a root pitch class and semitone intervals.</summary>
public sealed record ChordDefinition(string Symbol, int Root, IReadOnlyList<int> Intervals);

/// <summary>Common harmonic patterns and explicit chord-symbol parsing.</summary>
public static partial class ChordProgressions
{
	/// <summary>Selectable patterns. Four chords is the common I–V–vi–IV schema.</summary>
	public static IReadOnlyList<string> Names { get; } = ["Four chords", "Doo-wop", "Jazz turnaround", "Blues", "Canon", "Minor descent", "Ragtime circle", "Custom"];
	private static readonly Dictionary<char, int> NoteRoots = new() { ['C'] = 0, ['D'] = 2, ['E'] = 4, ['F'] = 5, ['G'] = 7, ['A'] = 9, ['B'] = 11 };
	private static readonly string[] RootNames = ["C", "C#", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"];
	private static readonly Dictionary<string, int[]> Qualities = new(StringComparer.Ordinal)
	{
		[""] = [0, 4, 7],
		["M"] = [0, 4, 7],
		["m"] = [0, 3, 7],
		["7"] = [0, 4, 7, 10],
		["maj7"] = [0, 4, 7, 11],
		["m7"] = [0, 3, 7, 10],
		["m7b5"] = [0, 3, 6, 10],
		["dim"] = [0, 3, 6],
		["dim7"] = [0, 3, 6, 9],
		["aug"] = [0, 4, 8],
		["sus2"] = [0, 2, 7],
		["sus4"] = [0, 5, 7]
	};

	/// <summary>Resolves a named pattern in a key, or parses one to 16 explicit comma-separated chord symbols.</summary>
	public static IReadOnlyList<ChordDefinition> Resolve(ArrangementSettings settings)
	{
		ArgumentNullException.ThrowIfNull(settings);
		ArgumentException.ThrowIfNullOrWhiteSpace(settings.Progression);
		if (settings.Progression == "Custom")
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(settings.CustomChords);
			var symbols = settings.CustomChords.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
			if (symbols.Length is < 1 or > 16)
			{
				throw new ArgumentException("Choose between one and 16 chord symbols, separated by commas.");
			}
			return [.. symbols.Select(Parse)];
		}
		return [.. Pattern(settings.Progression, settings.Minor).Select(c => Make(settings.Key + c.Root, c.Quality))];
	}

	/// <summary>Parses a named chord such as C, F#m, Bbmaj7, G7 or Dsus4.</summary>
	public static ChordDefinition Parse(string symbol)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
		var match = ChordSymbol().Match(symbol.Trim().Replace('♯', '#').Replace('♭', 'b'));
		if (!match.Success)
		{
			throw new ArgumentException($"Unknown chord '{symbol}'. Try C, Am, Fmaj7, G7, Bdim, Dsus2 or Dsus4.", nameof(symbol));
		}
		var root = NoteRoots[char.ToUpperInvariant(match.Groups[1].Value[0])];
		root += match.Groups[2].Value switch { "#" => 1, "b" => -1, _ => 0 };
		return Make(root, match.Groups[3].Value);
	}

	private static ChordDefinition Make(int root, string quality)
	{
		root = ((root % 12) + 12) % 12;
		return new(RootNames[root] + quality, root, Array.AsReadOnly(Qualities[quality]));
	}

	private static readonly Dictionary<string, Func<bool, (int Root, string Quality)[]>> Patterns = new(StringComparer.Ordinal)
	{
		["Four chords"] = FourChords,
		["Doo-wop"] = DooWop,
		["Jazz turnaround"] = m => m ? [(2, "m7b5"), (7, "7"), (0, "m7"), (8, "7")] : [(2, "m7"), (7, "7"), (0, "maj7"), (9, "7")],
		["Blues"] = _ => [(0, "7"), (0, "7"), (0, "7"), (0, "7"), (5, "7"), (5, "7"), (0, "7"), (0, "7"), (7, "7"), (5, "7"), (0, "7"), (7, "7")],
		["Canon"] = m => m ? [(0, "m"), (7, ""), (8, ""), (3, ""), (5, "m"), (0, "m"), (5, "m"), (7, "")] : [(0, ""), (7, ""), (9, "m"), (4, "m"), (5, ""), (0, ""), (5, ""), (7, "")],
		["Minor descent"] = _ => [(0, "m"), (10, ""), (8, ""), (7, "7")],
		["Ragtime circle"] = _ => [(0, ""), (9, "7"), (2, "7"), (7, "7")]
	};

	private static (int, string)[] FourChords(bool minor) =>
		[(0, minor ? "m" : ""), (7, ""), (minor ? 8 : 9, minor ? "" : "m"), (5, minor ? "m" : "")];

	private static (int, string)[] DooWop(bool minor) =>
		[(0, minor ? "m" : ""), (minor ? 8 : 9, minor ? "" : "m"), (5, minor ? "m" : ""), (7, "7")];

	private static (int Root, string Quality)[] Pattern(string name, bool minor)
	{
		if (!Patterns.TryGetValue(name, out var build)) { throw new ArgumentException($"Unknown progression '{name}'.", nameof(name)); }
		return build(minor);
	}

	[GeneratedRegex("^([A-Ga-g])([#b]?)(maj7|m7b5|m7|dim7|dim|aug|sus2|sus4|m|M|7)?$", RegexOptions.CultureInvariant)]
	private static partial Regex ChordSymbol();
}
