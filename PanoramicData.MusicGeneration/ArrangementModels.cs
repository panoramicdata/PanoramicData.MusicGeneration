namespace PanoramicData.MusicGeneration;

/// <summary>Starting points for instrumentation and musical character, independently overridable.</summary>
public enum MusicGenre
{
	/// <summary>Contemporary pop ensemble.</summary>
	Pop,
	/// <summary>Guitar band.</summary>
	Rock,
	/// <summary>Offbeat accompaniment and one-drop drums.</summary>
	Reggae,
	/// <summary>Syncopated guitar and bass.</summary>
	BossaNova,
	/// <summary>Stride accompaniment and syncopated piano.</summary>
	Ragtime,
	/// <summary>Acoustic chamber ensemble.</summary>
	Classical,
	/// <summary>Layered strings, woodwind and brass.</summary>
	Orchestral,
	/// <summary>Small swing ensemble.</summary>
	Jazz
}

/// <summary>Rhythmic templates; genre does not prevent choosing another beat.</summary>
public enum MusicBeat
{
	/// <summary>Even four-beat accompaniment.</summary>
	Straight,
	/// <summary>Backbeat on beats two and four.</summary>
	Rock,
	/// <summary>Offbeat chords and a beat-three one-drop.</summary>
	Reggae,
	/// <summary>Syncopated bossa-inspired accompaniment.</summary>
	BossaNova,
	/// <summary>Long-short eighth notes.</summary>
	Swing,
	/// <summary>Alternating bass and chords.</summary>
	Stride,
	/// <summary>Three beats per bar.</summary>
	Waltz,
	/// <summary>Four-beat march.</summary>
	March,
	/// <summary>Flowing broken chords without a drum groove.</summary>
	Arpeggio
}

/// <summary>What a layer plays, independent of its instrument.</summary>
public enum LayerPattern
{
	/// <summary>Voice-led chord accompaniment.</summary>
	Chords,
	/// <summary>Chord-root and fifth bass line.</summary>
	Bass,
	/// <summary>Recurring chord-aware lead motif.</summary>
	Melody,
	/// <summary>Contrasting answering motif.</summary>
	Countermelody,
	/// <summary>Broken chord notes.</summary>
	Arpeggio,
	/// <summary>General MIDI bass drum.</summary>
	Kick,
	/// <summary>General MIDI snare.</summary>
	Snare,
	/// <summary>General MIDI closed hi-hat.</summary>
	HiHat
}

/// <summary>An independently named instrument layer with zero-based SoundFont bank/program.</summary>
public sealed record InstrumentLayer
{
	/// <summary>Stable identifier, unique within a score.</summary>
	public string Id { get; init; } = "layer";
	/// <summary>Display name.</summary>
	public string Name { get; init; } = "Piano";
	/// <summary>Musical function.</summary>
	public LayerPattern Pattern { get; init; } = LayerPattern.Melody;
	/// <summary>SoundFont bank; drums use bank 128 or above.</summary>
	public int Bank { get; init; }
	/// <summary>Zero-based instrument program.</summary>
	public int Program { get; init; }
	/// <summary>Base MIDI velocity, 1–127.</summary>
	public int Velocity { get; init; } = 90;
	/// <summary>Linear mix level, 0–2.</summary>
	public double Gain { get; init; } = 0.8;
	/// <summary>Register shift, -2 to 2 octaves; percussion is unshifted.</summary>
	public int Octave { get; init; }
	/// <summary>Whether this layer is audible.</summary>
	public bool Enabled { get; init; } = true;
	/// <summary>Logical voice for existing note visualisers.</summary>
	public VoiceKind Voice => Pattern switch
	{
		LayerPattern.Bass => VoiceKind.Bass,
		LayerPattern.Melody or LayerPattern.Countermelody => VoiceKind.Melody,
		LayerPattern.Kick => VoiceKind.Kick,
		LayerPattern.Snare => VoiceKind.Snare,
		LayerPattern.HiHat => VoiceKind.HiHat,
		_ => VoiceKind.Pad
	};
}

/// <summary>Portable composition and instrument settings, without any bank or rendered audio.</summary>
public sealed record ArrangementSettings
{
	/// <summary>Deterministic composition seed.</summary>
	public int Seed { get; init; } = 42;
	/// <summary>Requested duration, 6–300 seconds.</summary>
	public int Seconds { get; init; } = 60;
	/// <summary>Quarter-note tempo, 40–220 bpm.</summary>
	public int Tempo { get; init; } = 100;
	/// <summary>Tonic pitch class, C=0 through B=11.</summary>
	public int Key { get; init; }
	/// <summary>Major or natural-minor key for melody passing notes.</summary>
	public bool Minor { get; init; }
	/// <summary>Ensemble starting point.</summary>
	public MusicGenre Genre { get; init; }
	/// <summary>Chosen rhythm.</summary>
	public MusicBeat Beat { get; init; }
	/// <summary>Named progression, or Custom.</summary>
	public string Progression { get; init; } = "Four chords";
	/// <summary>Comma-separated chord symbols when Custom is chosen.</summary>
	public string CustomChords { get; init; } = "C, G, Am, F";
	/// <summary>Offbeat delay as a fraction of a beat, 0–0.4.</summary>
	public double Swing { get; init; }
	/// <summary>Controlled motif variation, 0–1.</summary>
	public double Variation { get; init; } = 0.25;
	/// <summary>Returning sections or continuous eight-bar blocks.</summary>
	public bool Sections { get; init; } = true;
	/// <summary>One to 24 independently selectable instrument layers.</summary>
	public IReadOnlyList<InstrumentLayer> Layers { get; init; } = [];
}

/// <summary>A chord occurrence with explicit pitches and performed boundaries.</summary>
public sealed record ChordEvent(string Symbol, int Root, IReadOnlyList<int> Intervals, double StartSeconds, double EndSeconds);

/// <summary>Written notes belonging to one instrument layer.</summary>
public sealed record LayerScore(InstrumentLayer Instrument, IReadOnlyList<NoteEvent> Notes);

/// <summary>A portable written arrangement. Rendering does not regenerate these notes.</summary>
public sealed record ArrangedScore
{
	/// <summary>Written-score file schema version.</summary>
	public int Version { get; init; } = 1;
	/// <summary>Algorithm that originally wrote the notes. Stored notes can be replayed without recomposition.</summary>
	public int ComposerVersion { get; init; } = ArrangementComposer.Version;
	/// <summary>Frozen settings used to compose the score.</summary>
	public required ArrangementSettings Settings { get; init; }
	/// <summary>Individual written parts, including muted layers.</summary>
	public required IReadOnlyList<LayerScore> Tracks { get; init; }
	/// <summary>Chord names, pitches and times.</summary>
	public required IReadOnlyList<ChordEvent> Chords { get; init; }
	/// <summary>Performed section boundaries.</summary>
	public required IReadOnlyList<SectionEvent> Sections { get; init; }
}
