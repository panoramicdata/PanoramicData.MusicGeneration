namespace PanoramicData.MusicGeneration;

/// <summary>Generates reproducible seeded songs, with optional recurring sections and an inspectable note list.</summary>
public static partial class SongGenerator
{
	/// <summary>
	/// The version of the generation algorithm. The same seed gives the same song only within one version: any change that alters the notes or the samples
	/// for an existing seed is a breaking change and increments this number.
	/// </summary>
	public static int Version => 3;

	private const double Peak = 0.9;

	/// <summary>Generates a song with the default style.</summary>
	/// <param name="seed">Chooses tempo, key, scale, harmony, melody, drum patterns, timbre and tuning.</param>
	/// <param name="seconds">Length of the song.</param>
	/// <param name="sampleRate">Output sample rate in hertz.</param>
	/// <returns>The song.</returns>
	public static GeneratedSong Generate(int seed, double seconds, int sampleRate) =>
		Generate(seed, seconds, sampleRate, new SongStyle());

	/// <summary>Generates a song.</summary>
	/// <param name="seed">Chooses tempo, key, scale, harmony, melody, drum patterns, timbre and tuning.</param>
	/// <param name="seconds">Length of the song, up to an hour.</param>
	/// <param name="sampleRate">Output sample rate in hertz, 8,000 to 96,000.</param>
	/// <param name="style">What may vary from song to song.</param>
	/// <returns>The song.</returns>
	/// <exception cref="ArgumentException">The style is inconsistent.</exception>
	/// <exception cref="ArgumentOutOfRangeException">The length or sample rate is out of range.</exception>
	public static GeneratedSong Generate(int seed, double seconds, int sampleRate, SongStyle style) => GenerateCore(seed, seconds, sampleRate, style, false);

	/// <summary>Generates the song and unnormalised per-voice buffers without changing the mix.</summary>
	public static GeneratedSong GenerateStems(int seed, double seconds, int sampleRate, SongStyle style) => GenerateCore(seed, seconds, sampleRate, style, true);

	/// <summary>Generates the song and stems with the default style.</summary>
	public static GeneratedSong GenerateStems(int seed, double seconds, int sampleRate) => GenerateStems(seed, seconds, sampleRate, new SongStyle());

	private static GeneratedSong GenerateCore(int seed, double seconds, int sampleRate, SongStyle style, bool captureStems)
	{
		ArgumentNullException.ThrowIfNull(style);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(seconds, 0);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(seconds, 3600);
		ArgumentOutOfRangeException.ThrowIfLessThan(sampleRate, 8000);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(sampleRate, 96000);
		style.Validate();

		var choice = new Pcg32(seed, 0);
		var tempo = choice.Next(style.MinTempoBpm, style.MaxTempoBpm + 1);
		var scale = style.Scales[choice.Next(0, style.Scales.Count)];
		var root = choice.Next(style.RootMidiLow, style.RootMidiHigh + 1);
		var brightness = 0.35 + (choice.NextDouble() * 0.5);
		var vibratoHz = style.VibratoMinHz + (choice.NextDouble() * (style.VibratoMaxHz - style.VibratoMinHz));
		var detune = choice.Next(-style.MaxDetuneCents, style.MaxDetuneCents + 1);
		var kickBase = 42 + (choice.NextDouble() * 24);
		var beat = 60.0 / tempo;
		var bar = 4 * beat;
		var lead = choice.NextDouble() * bar;
		var info = new SongInfo(seed, tempo, scale.Name, root, detune, lead);

		var mixer = new Mixer(sampleRate, (int)(seconds * sampleRate), info.ReferenceHz, style, captureStems);
		var context = new ArrangementContext(info, scale, brightness, vibratoHz, kickBase, style);
		if (style.Form is { } form)
		{
			var sections = SectionPlanner.Place(form, info, seconds);
			PlayForm(mixer, context, form, sections);
			return mixer.Finish(info) with { Sections = sections };
		}
		var noise = new Pcg32(seed, 2);

		var bars = (int)Math.Ceiling((seconds - lead) / bar);
		for (var b = 0; b < bars; b++)
		{
			PlayBar(mixer, noise, new Pcg32(seed, 1000L + b), style, context, lead + (b * bar));
		}

		if (style.Melody)
		{
			PlayMelody(mixer, new Pcg32(seed, 1), style, context, lead, seconds);
		}

		return mixer.Finish(info);
	}

	internal static double VibratoFraction(double time, double duration, double rate, SongStyle style, bool platformSine)
	{
		if (duration < 1.0 / 3 || rate <= 0 || style.VibratoDepth == 0)
		{
			return 0;
		}

		var phase = 2 * Math.PI * rate * time;
		return style.VibratoDepth * Math.Clamp((time - style.VibratoDelaySeconds) / style.VibratoRampSeconds, 0, 1)
			* (platformSine ? Math.Sin(phase) : DeterministicMath.Sin(phase));
	}

	private readonly record struct Timbre(double Level, int Harmonics, double Rolloff, double VibratoHz, double Decay, double AttackSeconds);
}
