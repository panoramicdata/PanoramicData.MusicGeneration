namespace PanoramicData.MusicGeneration;

/// <summary>Composes chord-aware, repeatable arrangements without synthesising audio or loading samples.</summary>
public static partial class ArrangementComposer
{
	/// <summary>Written-arrangement algorithm version. Legacy SongGenerator output is unaffected.</summary>
	public static int Version => 4;

	/// <summary>Validates settings and writes harmony, recurring motifs, rhythm and independent instrumental parts.</summary>
	public static ArrangedScore Compose(ArrangementSettings settings)
	{
		Validate(settings);
		settings = Freeze(settings);
		var form = PlanForm(settings);
		var chords = Harmony(settings, form);
		var participation = PlanParticipation(settings, form.Sections);
		var motifs = CreateMotifs(settings);
		var tracks = settings.Layers.Select(layer => new LayerScore(layer, WritePart(settings, layer, chords, form, participation, motifs))).ToArray();
		return new()
		{
			Settings = settings,
			Tracks = tracks,
			Chords = chords,
			Sections = form.Sections,
			PerformedSeconds = form.Duration,
			Ending = form.Ending,
			Participation = participation,
			Motifs = motifs
		};
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
		ValidateExpression(settings);
	}

	private static void ValidateLayer(InstrumentLayer layer)
	{
		ArgumentNullException.ThrowIfNull(layer);
		if (string.IsNullOrWhiteSpace(layer.Id) || layer.Id.Length > 80 || string.IsNullOrWhiteSpace(layer.Name) || layer.Name.Length > 100)
		{
			throw new ArgumentException("Layers need a short identifier and display name.");
		}
		ValidateLayerMusic(layer);
		ValidatePresence(layer.Presence);
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

	private static List<NoteEvent> WritePart(ArrangementSettings settings, InstrumentLayer layer,
		List<ChordEvent> chords, FormPlan form, List<LayerParticipation> participation, IReadOnlyList<MelodyMotif> motifs)
	{
		var notes = new List<NoteEvent>();
		var previous = new[] { 48 + settings.Key, 52 + settings.Key, 55 + settings.Key };
		for (var index = 0; index < chords.Count; index++)
		{
			var chord = chords[index];
			var section = form.Sections.First(s => chord.StartSeconds >= s.StartSeconds - 1e-9 && chord.StartSeconds < s.EndSeconds - 1e-9);
			if (!participation.Single(p => p.LayerId == layer.Id && p.SectionIndex == section.Index).Present) { continue; }
			var voicing = VoiceLead(chord, previous);
			previous = voicing;
			WriteBar(new(settings, layer, chord, section, index, voicing, form.Ending, motifs), notes);
		}
		return notes;
	}

	private sealed record BarContext(ArrangementSettings Settings, InstrumentLayer Layer, ChordEvent Chord,
		SectionEvent Section, int Index, int[] Voicing, EndingPlan Ending, IReadOnlyList<MelodyMotif> Motifs)
	{
		public int BeatsPerBar => ArrangementComposer.Beats(Settings);
		public double BeatSeconds => 60.0 / Settings.Tempo;
		public int LocalBar => (int)Math.Round((Chord.StartSeconds - Section.StartSeconds) / (BeatsPerBar * BeatSeconds));
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
		var lift = DynamicLift(bar);
		notes.Add(new(bar.Layer.Voice, start, end - start, midi, drum ? 0 : Pitch.FrequencyOf(midi))
		{
			SectionIndex = bar.Section.Index,
			LayerId = bar.Layer.Id,
			Velocity = Math.Clamp(bar.Layer.Velocity + accent + lift, 1, 127)
		});
	}
}
