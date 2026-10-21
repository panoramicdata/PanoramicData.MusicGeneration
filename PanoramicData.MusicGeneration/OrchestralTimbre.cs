namespace PanoramicData.MusicGeneration;

/// <summary>Parameters for additive/noise synthesis; contains no recorded PCM or sample loops.</summary>
public sealed record OrchestralTimbre
{
	/// <summary>Instrument/profile identity.</summary>
	public required string Name { get; init; }
	/// <summary>Relative amplitudes of harmonics 1–64 during the attack.</summary>
	public required IReadOnlyList<double> AttackSpectrum { get; init; }
	/// <summary>Relative amplitudes of harmonics 1–64 during the held tone.</summary>
	public required IReadOnlyList<double> SustainSpectrum { get; init; }
	/// <summary>Amplitude rise duration, seconds.</summary>
	public double AttackSeconds { get; init; } = 0.1;
	/// <summary>Release after note-off, seconds.</summary>
	public double ReleaseSeconds { get; init; } = 0.15;
	/// <summary>Time constant of the transition from attack to sustain spectrum.</summary>
	public double SpectralTransitionSeconds { get; init; } = 0.15;
	/// <summary>Additional harmonic roll-off exponent; positive values darken the tone.</summary>
	public double HarmonicTilt { get; init; }
	/// <summary>Coloured noise amplitude relative to the harmonic amplitude sum, 0–0.3.</summary>
	public double NoiseLevel { get; init; } = 0.025;
	/// <summary>High-pass noise corner, Hz.</summary>
	public double NoiseCutoffHz { get; init; } = 1000;
	/// <summary>Pitch modulation depth in cents.</summary>
	public double VibratoCents { get; init; } = 8;
	/// <summary>Pitch modulation rate in Hz.</summary>
	public double VibratoHz { get; init; } = 5;
	/// <summary>Delay before vibrato begins its 250 ms rise.</summary>
	public double VibratoDelaySeconds { get; init; } = 0.3;
	/// <summary>Amplitude of slow deterministic pitch drift, cents.</summary>
	public double PitchDriftCents { get; init; } = 2;
	/// <summary>Additional darkening at soft velocities, 0–2.</summary>
	public double VelocityBrightness { get; init; } = 0.6;
}

/// <summary>A single generated note. Profile and note settings are independent.</summary>
public sealed record OrchestralNote
{
	/// <summary>MIDI pitch, 24–108.</summary>
	public int MidiNote { get; init; } = 72;
	/// <summary>Time to note-off, 0.1–10 seconds; release is added.</summary>
	public double Seconds { get; init; } = 2;
	/// <summary>Playing strength, 1–127.</summary>
	public int Velocity { get; init; } = 90;
	/// <summary>PCM sample rate, 16,000–96,000 Hz.</summary>
	public int SampleRate { get; init; } = 22050;
	/// <summary>Noise and drift identity.</summary>
	public int Seed { get; init; } = 42;
}
