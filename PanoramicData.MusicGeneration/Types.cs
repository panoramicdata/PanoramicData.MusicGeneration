namespace PanoramicData.MusicGeneration;

/// <summary>The part of the band a note belongs to.</summary>
public enum VoiceKind
{
	/// <summary>Sustained chord tones, one chord per bar.</summary>
	Pad,

	/// <summary>Low root and fifth notes on the beat.</summary>
	Bass,

	/// <summary>The tune: a random walk through the scale.</summary>
	Melody,

	/// <summary>Bass drum: a sine whose pitch falls quickly.</summary>
	Kick,

	/// <summary>Snare: a burst of high-passed noise.</summary>
	Snare,

	/// <summary>Hi-hat: a short burst of high-passed noise.</summary>
	HiHat
}

/// <summary>One note or drum hit the generator played, so a caller can compare what was played with what an analyser hears.</summary>
/// <param name="Voice">Which part played it.</param>
/// <param name="StartSeconds">When it starts, from the start of the song.</param>
/// <param name="DurationSeconds">How long it sounds (the written length; the sound may ring a little beyond it).</param>
/// <param name="MidiNote">MIDI note number (60 is middle C, 69 is A above it); for drums, a nominal pitch only.</param>
/// <param name="FrequencyHz">The frequency actually rendered, including the song's tuning offset; for noise drums, 0.</param>
public readonly record struct NoteEvent(VoiceKind Voice, double StartSeconds, double DurationSeconds, int MidiNote, double FrequencyHz);

/// <summary>A musical scale as semitone steps above the root.</summary>
/// <param name="Name">Display name.</param>
/// <param name="Semitones">Ascending steps from the root within one octave, starting at 0.</param>
public sealed record ScaleDefinition(string Name, IReadOnlyList<int> Semitones)
{
	/// <summary>Number of notes per octave.</summary>
	public int Count => Semitones.Count;

	/// <summary>The MIDI note of scale degree <paramref name="degree"/> above <paramref name="rootMidi"/>; degrees past the top wrap into the next octave.</summary>
	/// <param name="rootMidi">MIDI note of degree 0.</param>
	/// <param name="degree">Scale degree, zero-based; may exceed <see cref="Count"/>.</param>
	/// <returns>The MIDI note number.</returns>
	public int MidiOf(int rootMidi, int degree)
	{
		var octave = Math.DivRem(degree, Count, out var index);
		return rootMidi + Semitones[index] + (12 * octave);
	}
}

/// <summary>The standard scales the generator chooses from.</summary>
public static class Scales
{
	/// <summary>Major (Ionian): bright.</summary>
	public static ScaleDefinition Major { get; } = new("Major", [0, 2, 4, 5, 7, 9, 11]);

	/// <summary>Natural minor (Aeolian): sad.</summary>
	public static ScaleDefinition NaturalMinor { get; } = new("Natural minor", [0, 2, 3, 5, 7, 8, 10]);

	/// <summary>Major pentatonic: five notes, no clashes.</summary>
	public static ScaleDefinition MajorPentatonic { get; } = new("Major pentatonic", [0, 2, 4, 7, 9]);

	/// <summary>Minor pentatonic: the blues skeleton.</summary>
	public static ScaleDefinition MinorPentatonic { get; } = new("Minor pentatonic", [0, 3, 5, 7, 10]);

	/// <summary>Dorian: minor with a raised sixth.</summary>
	public static ScaleDefinition Dorian { get; } = new("Dorian", [0, 2, 3, 5, 7, 9, 10]);

	/// <summary>All of the above, in a fixed order.</summary>
	public static IReadOnlyList<ScaleDefinition> All { get; } = [Major, NaturalMinor, MajorPentatonic, MinorPentatonic, Dorian];
}

/// <summary>Pitch arithmetic for twelve-tone equal temperament.</summary>
public static class Pitch
{
	private static readonly string[] Names = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

	/// <summary>Frequency of a MIDI note: <c>reference * 2^((midi - 69) / 12)</c>, so each semitone multiplies the frequency by 2^(1/12).</summary>
	/// <param name="midi">MIDI note number (69 is A above middle C).</param>
	/// <param name="referenceHz">Frequency of note 69; 440 by default in concert pitch.</param>
	/// <returns>Frequency in hertz.</returns>
	public static double FrequencyOf(double midi, double referenceHz) => referenceHz * Math.Pow(2, (midi - 69) / 12.0);

	/// <summary>Frequency of a MIDI note in concert pitch (A4 = 440 Hz).</summary>
	/// <param name="midi">MIDI note number.</param>
	/// <returns>Frequency in hertz.</returns>
	public static double FrequencyOf(double midi) => FrequencyOf(midi, 440);

	/// <summary>The scientific-pitch name of a MIDI note, such as C4 for 60 and A4 for 69.</summary>
	/// <param name="midi">MIDI note number.</param>
	/// <returns>The name.</returns>
	public static string NameOf(int midi) => Names[((midi % 12) + 12) % 12] + (Math.DivRem(midi, 12, out _) - 1);
}

/// <summary>Everything that varies from song to song, with defaults that make varied, pleasant-sounding music.</summary>
public sealed record SongStyle
{
	/// <summary>Slowest tempo that may be chosen, beats per minute. Default 78.</summary>
	public int MinTempoBpm { get; init; } = 78;

	/// <summary>Fastest tempo that may be chosen, beats per minute. Default 147.</summary>
	public int MaxTempoBpm { get; init; } = 147;

	/// <summary>The scales a song may use. Default: all of <see cref="Scales.All"/>.</summary>
	public IReadOnlyList<ScaleDefinition> Scales { get; init; } = PanoramicData.MusicGeneration.Scales.All;

	/// <summary>Lowest MIDI note that may be a song's root (the bass sits here). Default 36 (C2).</summary>
	public int RootMidiLow { get; init; } = 36;

	/// <summary>Highest MIDI note that may be a song's root. Default 47 (B2).</summary>
	public int RootMidiHigh { get; init; } = 47;

	/// <summary>Lowest note of the melody. Default 60 (middle C), where most songs' tunes sit.</summary>
	public int MelodyLowMidi { get; init; } = 60;

	/// <summary>Highest note of the melody. Default 84 (C6, about 1 kHz).</summary>
	public int MelodyHighMidi { get; init; } = 84;

	/// <summary>The largest random detuning of the whole song from concert pitch, in cents either way (100 cents is a semitone). Default 45.</summary>
	public int MaxDetuneCents { get; init; } = 45;

	/// <summary>Whether to play the chord pad. Default true.</summary>
	public bool Pad { get; init; } = true;

	/// <summary>Whether to play the bass line. Default true.</summary>
	public bool Bass { get; init; } = true;

	/// <summary>Whether to play the melody. Default true.</summary>
	public bool Melody { get; init; } = true;

	/// <summary>Whether to play the drums. Default true.</summary>
	public bool Drums { get; init; } = true;

	/// <summary>
	/// Use <see cref="Math.Sin(double)"/> instead of the library's own bit-exact sine. It is much faster where arithmetic is slow (for example in an interpreted WebAssembly runtime), but the last bit of each sample may then differ between
	/// operating systems and CPUs, so the audio is no longer guaranteed identical everywhere. The notes and choices are unaffected. Default false.
	/// </summary>
	public bool UsePlatformSine { get; init; }

	/// <summary>Checks the style for consistency.</summary>
	/// <exception cref="ArgumentException">A value is out of range.</exception>
	public void Validate()
	{
		Require(MinTempoBpm is >= 30 and <= 300 && MaxTempoBpm >= MinTempoBpm && MaxTempoBpm <= 300, nameof(MaxTempoBpm), "tempo range must lie between 30 and 300 bpm and not be inverted");
		Require(Scales is { Count: > 0 }, nameof(Scales), "needs at least one scale");
		Require(RootMidiLow is >= 12 and <= 72 && RootMidiHigh >= RootMidiLow && RootMidiHigh <= 72, nameof(RootMidiHigh), "root range must lie between MIDI 12 and 72 and not be inverted");
		Require(MelodyLowMidi is >= 24 and <= 108 && MelodyHighMidi >= MelodyLowMidi + 12 && MelodyHighMidi <= 108, nameof(MelodyHighMidi), "melody range must lie between MIDI 24 and 108 and span at least an octave");
		Require(MaxDetuneCents is >= 0 and <= 50, nameof(MaxDetuneCents), "detune must be 0 to 50 cents");
	}

	private static void Require(bool condition, string name, string message)
	{
		if (!condition)
		{
			throw new ArgumentException($"{name}: {message}.", name);
		}
	}
}

/// <summary>The choices a seed made for a song.</summary>
/// <param name="Seed">The seed.</param>
/// <param name="TempoBpm">Beats per minute.</param>
/// <param name="ScaleName">The scale used.</param>
/// <param name="RootMidi">MIDI note of the key's root.</param>
/// <param name="DetuneCents">Offset of the whole song from concert pitch, in cents.</param>
/// <param name="LeadInSeconds">Silence before the first bar, which moves the whole song off the beat grid of any other song.</param>
public sealed record SongInfo(int Seed, int TempoBpm, string ScaleName, int RootMidi, double DetuneCents, double LeadInSeconds)
{
	/// <summary>The frequency of A4 in this song, after detuning.</summary>
	public double ReferenceHz => 440 * Math.Pow(2, DetuneCents / 1200.0);
}

/// <summary>A generated song: audio, the notes that made it, and the choices behind it.</summary>
/// <param name="Info">The choices the seed made.</param>
/// <param name="Samples">Mono samples, peaking at 0.9.</param>
/// <param name="SampleRate">Sample rate in hertz.</param>
/// <param name="Notes">Every note and drum hit, in start order.</param>
public sealed record GeneratedSong(SongInfo Info, float[] Samples, int SampleRate, IReadOnlyList<NoteEvent> Notes)
{
	/// <summary>Duration of the audio.</summary>
	public TimeSpan Duration => TimeSpan.FromSeconds((double)Samples.Length / SampleRate);

	/// <summary>The song as a 16-bit mono WAV file.</summary>
	/// <returns>The file's bytes.</returns>
	public byte[] ToWav() => WavEncoder.Encode(Samples, SampleRate);
}
