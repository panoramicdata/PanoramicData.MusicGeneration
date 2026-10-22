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
	/// <summary>Add coordinated hands and treble voicings to piano melody/arpeggio parts; ignored for other instruments and roles.</summary>
	public bool PianoTwoHands { get; init; } = true;
	/// <summary>Whole-section participation probabilities and occurrence overrides.</summary>
	public LayerPresence Presence { get; init; } = new();
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
	/// <summary>Optional independent seed for melodic ideas and their development; null follows Seed.</summary>
	public int? MelodySeed { get; init; }
	/// <summary>Maximum performed duration, 6–300 seconds; the song may finish earlier.</summary>
	public int Seconds { get; init; } = 210;
	/// <summary>Quarter-note tempo, 40–220 bpm.</summary>
	public int Tempo { get; init; } = 160;
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
	public double Swing { get; init; } = 0.1;
	/// <summary>Probability of a functional chord substitution in a four-bar phrase, 0–1.</summary>
	public double ProgressionVariation { get; init; } = 0.35;
	/// <summary>Chance of deriving contrasting sections from the chosen cycle, 0–1; zero repeats the cycle unchanged.</summary>
	public double SectionContrast { get; init; } = 1;
	/// <summary>Hook teaser and accompaniment build variation within the four-bar intro, 0–1.</summary>
	public double IntroVariation { get; init; } = 0.65;
	/// <summary>Chance of shaping a four-bar harmonic phrase with held and faster-moving chords, 0–1.</summary>
	public double HarmonicMovement { get; init; } = 0.65;
	/// <summary>Probability of a prepared dominant/borrowed chord or melodic embellishment at eligible positions, 0–1.</summary>
	public double ChromaticColour { get; init; } = 0.3;
	/// <summary>Strength of melodic tension, connected articulation, phrase dynamics and shaped timing, 0–1.</summary>
	public double PhraseExpression { get; init; } = 0.65;
	/// <summary>Probability of accompaniment rests, altered gestures and bass approaches at eligible positions, 0–1.</summary>
	public double AccompanimentVariation { get; init; } = 0.5;
	/// <summary>Probability of small two-bar drum variations, ghost notes and cymbal articulations, 0–1.</summary>
	public double DrumVariation { get; init; } = 0.55;
	/// <summary>Chance and scope of phrase/section drum fills, 0–1; zero retains the earlier two-hit verse pickup.</summary>
	public double DrumFills { get; init; } = 0.6;
	/// <summary>Strength of drum accents, phrase dynamics and bounded role-specific timing, 0–1.</summary>
	public double DrumHumanity { get; init; } = 0.6;
	/// <summary>Relative weights for the four implemented ending types.</summary>
	public EndingWeights Endings { get; init; } = new();
	/// <summary>Chance of developing later two-bar phrases through changed answers, fragmentation/inversion and closure, 0–1.</summary>
	public double Variation { get; init; } = 0.6;
	/// <summary>Returning sections or continuous eight-bar blocks.</summary>
	public bool Sections { get; init; } = true;
	/// <summary>One to 24 independently selectable instrument layers.</summary>
	public IReadOnlyList<InstrumentLayer> Layers { get; init; } = [];
}

/// <summary>A chord occurrence with explicit pitches and performed boundaries.</summary>
public sealed record ChordEvent(string Symbol, int Root, IReadOnlyList<int> Intervals, double StartSeconds, double EndSeconds)
{
	/// <summary>Optional explanatory harmonic function; does not control rendering saved notes.</summary>
	public string? Function { get; init; }
	/// <summary>Optional chord symbol toward which an approach chord resolves.</summary>
	public string? ResolvesTo { get; init; }
}

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
	/// <summary>Actual audio duration including release; zero denotes an older score using the requested duration.</summary>
	public double PerformedSeconds { get; init; }
	/// <summary>Planned ending, or null for scores written by composer version 1.</summary>
	public EndingPlan? Ending { get; init; }
	/// <summary>Saved whole-section decisions, including absent layers.</summary>
	public IReadOnlyList<LayerParticipation> Participation { get; init; } = [];
	/// <summary>Saved two-bar melodic ideas in key-relative scale degrees; older written scores may omit these.</summary>
	public IReadOnlyList<MelodyMotif> Motifs { get; init; } = [];
}
