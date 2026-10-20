namespace PanoramicData.MusicGeneration;

/// <summary>Composes chord-aware, repeatable arrangements without synthesising audio or loading samples.</summary>
public static partial class ArrangementComposer
{
	/// <summary>Written-arrangement algorithm version. Legacy SongGenerator output is unaffected.</summary>
	public const int Version = 1;

	/// <summary>Validates settings and writes harmony, recurring motifs, rhythm and independent instrumental parts.</summary>
	public static ArrangedScore Compose(ArrangementSettings settings)
	{
		Validate(settings);
		settings = settings with { Layers = Array.AsReadOnly(settings.Layers.ToArray()) };
		var chords = Harmony(settings);
		var sections = Form(settings);
		var tracks = settings.Layers.Select(layer => new LayerScore(layer, WritePart(settings, layer, chords, sections))).ToArray();
		return new() { Settings = settings, Tracks = tracks, Chords = chords, Sections = sections };
	}

	/// <summary>Checks bounds before composition or rendering imported settings.</summary>
	public static void Validate(ArrangementSettings settings)
	{
		ArgumentNullException.ThrowIfNull(settings);
		Range(settings.Seconds, 6, 300, "Duration");
		Range(settings.Tempo, 40, 220, "Tempo");
		Range(settings.Key, 0, 11, "Key");
		ValidateCharacter(settings);
		if (settings.Layers is not { Count: > 0 and <= 24 })
		{
			throw new ArgumentException("Choose one to 24 layers with unique identifiers.");
		}
		foreach (var layer in settings.Layers)
		{
			ValidateLayer(layer);
		}
		if (settings.Layers.Select(l => l.Id).Distinct().Count() != settings.Layers.Count)
		{
			throw new ArgumentException("Layer identifiers must be unique.");
		}
		_ = ChordProgressions.Resolve(settings);
	}

	private static void ValidateCharacter(ArrangementSettings settings)
	{
		if (!Enum.IsDefined(settings.Genre) || !Enum.IsDefined(settings.Beat))
		{
			throw new ArgumentException("Choose a valid genre and beat.");
		}
		Range(settings.Swing, 0, 0.4, "Swing");
		Range(settings.Variation, 0, 1, "Variation");
	}

	private static void ValidateLayer(InstrumentLayer layer)
	{
		ArgumentNullException.ThrowIfNull(layer);
		if (string.IsNullOrWhiteSpace(layer.Id) || layer.Id.Length > 80 || string.IsNullOrWhiteSpace(layer.Name) || layer.Name.Length > 100)
		{
			throw new ArgumentException("Layers need a short identifier and display name.");
		}
		ValidateLayerMusic(layer);
	}

	private static void ValidateLayerMusic(InstrumentLayer layer)
	{
		if (!Enum.IsDefined(layer.Pattern))
		{
			throw new ArgumentException("Invalid layer pattern, program, velocity or octave.");
		}
		Range(layer.Program, 0, 127, "Program");
		Range(layer.Velocity, 1, 127, "Velocity");
		Range(layer.Octave, -2, 2, "Octave");
		var drum = layer.Voice is VoiceKind.Kick or VoiceKind.Snare or VoiceKind.HiHat;
		var minimum = drum ? 128 : 0;
		Range(layer.Bank, minimum, minimum + 127, "Bank");
		Range(layer.Gain, 0, 2, "Gain");
	}

	private static void Range(double value, double minimum, double maximum, string name)
	{
		if (!double.IsFinite(value) || value < minimum || value > maximum)
		{
			throw new ArgumentException($"{name} must be between {minimum} and {maximum}.", name);
		}
	}

	private static int Beats(ArrangementSettings settings) => settings.Beat == MusicBeat.Waltz ? 3 : 4;

	private static List<ChordEvent> Harmony(ArrangementSettings settings)
	{
		var progression = ChordProgressions.Resolve(settings);
		var bar = Beats(settings) * 60.0 / settings.Tempo;
		var result = new List<ChordEvent>();
		for (var index = 0; index * bar < settings.Seconds - 1e-9; index++)
		{
			var chord = progression[index % progression.Count];
			result.Add(new(chord.Symbol, chord.Root, chord.Intervals, index * bar, Math.Min((index + 1) * bar, settings.Seconds)));
		}
		return result;
	}

	private static List<SectionEvent> Form(ArrangementSettings settings)
	{
		var result = new List<SectionEvent>();
		var bar = Beats(settings) * 60.0 / settings.Tempo;
		var at = 0.0;
		for (var index = 0; at < settings.Seconds - 1e-9; index++)
		{
			var kind = SectionRole(settings, index, settings.Seconds - at, bar);
			var bars = kind switch { SectionKind.Intro or SectionKind.Outro => 1, SectionKind.Bridge => 4, _ => 8 };
			var end = at + bars * bar;
			result.Add(new()
			{
				Index = index,
				Kind = kind,
				MaterialId = Material(kind),
				StartSeconds = at,
				EndSeconds = Math.Min(end, settings.Seconds),
				PlannedEndSeconds = end,
				Bars = bars,
				PhraseCount = bars == 8 ? 4 : 1,
				PhraseBars = bars == 8 ? 2 : bars
			});
			at = end;
		}
		return result;
	}

	private static SectionKind SectionRole(ArrangementSettings settings, int index, double remaining, double bar)
	{
		if (!settings.Sections) { return SectionKind.Chorus; }
		if (index == 0) { return SectionKind.Intro; }
		if (remaining <= bar + 1e-9) { return SectionKind.Outro; }
		if (index % 6 == 5) { return SectionKind.Bridge; }
		return index % 2 == 1 ? SectionKind.Verse : SectionKind.Chorus;
	}

	private static string Material(SectionKind kind) => kind switch
	{
		SectionKind.Verse => "A",
		SectionKind.Chorus => "B",
		SectionKind.Bridge => "C",
		_ => kind.ToString()
	};

	private static List<NoteEvent> WritePart(ArrangementSettings settings, InstrumentLayer layer,
		List<ChordEvent> chords, List<SectionEvent> sections)
	{
		var notes = new List<NoteEvent>();
		var previous = new[] { 48 + settings.Key, 52 + settings.Key, 55 + settings.Key };
		for (var index = 0; index < chords.Count; index++)
		{
			var chord = chords[index];
			var section = sections.First(s => chord.StartSeconds >= s.StartSeconds - 1e-9 && chord.StartSeconds < s.EndSeconds - 1e-9);
			var voicing = VoiceLead(chord, previous);
			previous = voicing;
			WriteBar(new(settings, layer, chord, section, index, voicing), notes);
		}
		return notes;
	}

	private sealed record BarContext(ArrangementSettings Settings, InstrumentLayer Layer, ChordEvent Chord,
		SectionEvent Section, int Index, int[] Voicing)
	{
		public int Beats => ArrangementComposer.Beats(Settings);
		public double BeatSeconds => 60.0 / Settings.Tempo;
	}

	private static int[] VoiceLead(ChordEvent chord, int[] previous)
	{
		var candidates = new List<int[]>();
		for (var octave = -1; octave <= 1; octave++)
		{
			for (var inversion = 0; inversion < chord.Intervals.Count; inversion++)
			{
				var pitches = chord.Intervals.Select((step, i) => 48 + chord.Root + octave * 12 + step + (i < inversion ? 12 : 0)).Order().ToArray();
				if (pitches[0] >= 40 && pitches[^1] <= 76) { candidates.Add(pitches); }
			}
		}
		return candidates.OrderBy(c => c.Select((pitch, i) => Math.Abs(pitch - previous[Math.Min(i, previous.Length - 1)])).Sum()).First();
	}

	private static void Add(BarContext bar, List<NoteEvent> notes, int midi, double at, double duration, int accent)
	{
		var delayed = Math.Abs(at % 1 - 0.5) < 0.01 ? bar.Settings.Swing : 0;
		var start = bar.Chord.StartSeconds + (at + delayed) * bar.BeatSeconds;
		var end = Math.Min(bar.Chord.EndSeconds, start + duration * bar.BeatSeconds);
		if (start >= end) { return; }
		var drum = bar.Layer.Voice is VoiceKind.Kick or VoiceKind.Snare or VoiceKind.HiHat;
		midi = Math.Clamp(midi + (drum ? 0 : bar.Layer.Octave * 12), 0, 127);
		var lift = bar.Section.Kind == SectionKind.Chorus ? 8 : 0;
		notes.Add(new(bar.Layer.Voice, start, end - start, midi, drum ? 0 : Pitch.FrequencyOf(midi))
		{
			SectionIndex = bar.Section.Index,
			LayerId = bar.Layer.Id,
			Velocity = Math.Clamp(bar.Layer.Velocity + accent + lift, 1, 127)
		});
	}
}
