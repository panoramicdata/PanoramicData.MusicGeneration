namespace PanoramicData.MusicGeneration.SoundFonts;

/// <summary>A zero-based SoundFont bank/program and fixed MIDI velocity for a musical role.</summary>
public sealed record SoundFontPart(VoiceKind Voice, int Bank, int Program, int Velocity);

/// <summary>An available bank/program in the loaded SF2 file.</summary>
public sealed record SoundFontPreset(int Bank, int Program, string Name);

/// <summary>Settings for the optional sample-bank renderer; effects are disabled for independent stems.</summary>
public sealed record SoundFontRenderOptions
{
	/// <summary>PCM rate, 16,000 to 96,000 Hz. Default 22,050.</summary>
	public int SampleRate { get; init; } = 22050;

	/// <summary>One assignment per role. Programs are zero-based General MIDI numbers.</summary>
	public IReadOnlyList<SoundFontPart> Parts { get; init; } =
	[
		new(VoiceKind.Pad, 0, 48, 70), new(VoiceKind.Bass, 0, 32, 95), new(VoiceKind.Melody, 0, 0, 95),
		new(VoiceKind.Kick, 128, 0, 100), new(VoiceKind.Snare, 128, 0, 90), new(VoiceKind.HiHat, 128, 0, 75)
	];
}

/// <summary>Replay identity. PCM repeatability is scoped to this renderer, bank, settings and runtime.</summary>
public sealed record SoundFontRenderMetadata
{
	/// <summary>Adapter event/rendering contract version.</summary>
	public int Version { get; init; } = 1;
	/// <summary>Loaded MeltySynth assembly version.</summary>
	public required string RendererVersion { get; init; }
	/// <summary>Caller-provided asset name.</summary>
	public required string BankName { get; init; }
	/// <summary>SHA-256 of the exact SF2 bytes.</summary>
	public required string BankSha256 { get; init; }
	/// <summary>Frozen render settings, including instrument and velocity assignments.</summary>
	public required SoundFontRenderOptions Options { get; init; }
	/// <summary>Maximum concurrent sample voices per stem.</summary>
	public int MaximumPolyphony { get; init; } = 256;
	/// <summary>Synthesizer processing quantum, in samples.</summary>
	public int BlockSize { get; init; } = 64;
	/// <summary>Effects are disabled to keep stems independent.</summary>
	public bool EffectsEnabled { get; init; }
}

/// <summary>Rendered PCM/stems and the information needed to identify the rendering setup.</summary>
public sealed record SoundFontRenderResult(GeneratedSong Song, SoundFontRenderMetadata Metadata);

/// <summary>Progress through all six independently rendered stems, from zero to one.</summary>
public sealed record SoundFontRenderProgress(VoiceKind Voice, double Fraction);
