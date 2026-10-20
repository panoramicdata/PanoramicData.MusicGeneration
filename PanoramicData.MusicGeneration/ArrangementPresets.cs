namespace PanoramicData.MusicGeneration;

/// <summary>Editable ensemble starting points; instruments use General MIDI zero-based programs.</summary>
public static class ArrangementPresets
{
	/// <summary>Creates an ensemble with distinct musical parts for the selected genre.</summary>
	public static ArrangementSettings ForGenre(MusicGenre genre)
	{
		if (!Factories.TryGetValue(genre, out var build)) { throw new ArgumentOutOfRangeException(nameof(genre)); }
		return build(new() { Genre = genre });
	}

	private static readonly Dictionary<MusicGenre, Func<ArrangementSettings, ArrangementSettings>> Factories = new()
	{
		[MusicGenre.Pop] = s => s with { Layers = Pop(), Beat = MusicBeat.Straight },
		[MusicGenre.Rock] = s => s with { Layers = Band(29, 27, 33), Beat = MusicBeat.Rock, Tempo = 112 },
		[MusicGenre.Reggae] = s => s with { Layers = Band(16, 27, 34), Beat = MusicBeat.Reggae, Tempo = 82 },
		[MusicGenre.BossaNova] = s => s with { Layers = Band(73, 24, 32), Beat = MusicBeat.BossaNova, Tempo = 124, Progression = "Jazz turnaround" },
		[MusicGenre.Jazz] = s => s with { Layers = Band(65, 4, 32), Beat = MusicBeat.Swing, Tempo = 116, Swing = 0.16, Progression = "Jazz turnaround" },
		[MusicGenre.Ragtime] = s => s with { Layers = Ragtime(), Beat = MusicBeat.Stride, Tempo = 116, Progression = "Ragtime circle", Variation = 0.35 },
		[MusicGenre.Classical] = s => s with { Layers = Chamber(), Beat = MusicBeat.Waltz, Tempo = 112, Progression = "Canon" },
		[MusicGenre.Orchestral] = s => s with { Layers = Orchestra(), Beat = MusicBeat.Arpeggio, Tempo = 96, Progression = "Canon" }
	};

	private static InstrumentLayer Layer(string id, string name, LayerPattern pattern, int program, double gain) => new()
	{
		Id = id,
		Name = name,
		Pattern = pattern,
		Program = program,
		Gain = gain,
		Presence = DefaultPresence(pattern),
		Bank = pattern is LayerPattern.Kick or LayerPattern.Snare or LayerPattern.HiHat ? 128 : 0
	};

	private static LayerPresence DefaultPresence(LayerPattern pattern) => pattern switch
	{
		LayerPattern.Countermelody => new() { Intro = 0.1, Verse = 0.25, Chorus = 0.75, Bridge = 0.6, Outro = 0.5 },
		LayerPattern.Snare => new() { Intro = 0.35, Verse = 0.85, Chorus = 0.9, Bridge = 0.55 },
		LayerPattern.Arpeggio => new() { Intro = 0.75, Verse = 0.65, Bridge = 0.8 },
		LayerPattern.Melody => new() { Intro = 0.65 },
		_ => new()
	};

	private static InstrumentLayer[] Band(int lead, int chords, int bass) =>
	[
		Layer("chords", "Chords", LayerPattern.Chords, chords, 0.65), Layer("bass", "Bass", LayerPattern.Bass, bass, 0.85),
		Layer("lead", "Lead", LayerPattern.Melody, lead, 0.8), Layer("kick", "Kick", LayerPattern.Kick, 0, 0.8),
		Layer("snare", "Snare / cross-stick", LayerPattern.Snare, 0, 0.65), Layer("hat", "Hi-hat", LayerPattern.HiHat, 0, 0.4)
	];

	private static InstrumentLayer[] Pop() =>
	[
		.. Band(0, 48, 32), Layer("answer", "Guitar answer", LayerPattern.Countermelody, 24, 0.35)
	];

	private static InstrumentLayer[] Ragtime() =>
	[
		Layer("stride", "Stride piano chords", LayerPattern.Chords, 0, 0.7), Layer("bass", "Left-hand bass", LayerPattern.Bass, 0, 0.7),
		Layer("lead", "Syncopated piano", LayerPattern.Melody, 0, 0.9), Layer("answer", "Piano answer", LayerPattern.Countermelody, 0, 0.3),
		Layer("clarinet", "Clarinet doubling", LayerPattern.Melody, 71, 0.25), Layer("banjo", "Banjo comping", LayerPattern.Chords, 105, 0.25),
		Layer("tuba", "Tuba bass", LayerPattern.Bass, 58, 0.2)
	];

	private static InstrumentLayer[] Chamber() =>
	[
		Layer("violin", "First violin", LayerPattern.Melody, 40, 0.8), Layer("violin2", "Second violin", LayerPattern.Countermelody, 40, 0.5),
		Layer("viola", "Viola harmony", LayerPattern.Chords, 41, 0.45), Layer("cello", "Cello", LayerPattern.Bass, 42, 0.7),
		Layer("piano", "Piano arpeggios", LayerPattern.Arpeggio, 0, 0.4), Layer("flute", "Flute", LayerPattern.Melody, 73, 0.3),
		Layer("oboe", "Oboe answer", LayerPattern.Countermelody, 68, 0.25)
	];

	private static InstrumentLayer[] Orchestra() =>
	[
		.. Chamber(), Layer("strings", "String section", LayerPattern.Chords, 48, 0.4),
		Layer("horn", "French horns", LayerPattern.Chords, 60, 0.25), Layer("bassoon", "Bassoon", LayerPattern.Bass, 70, 0.25),
		Layer("harp", "Harp", LayerPattern.Arpeggio, 46, 0.3), Layer("basses", "Double basses", LayerPattern.Bass, 43, 0.35)
	];
}
