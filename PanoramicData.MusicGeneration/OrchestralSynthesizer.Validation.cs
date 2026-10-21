namespace PanoramicData.MusicGeneration;

public static partial class OrchestralSynthesizer
{
	private static void Validate(OrchestralTimbre timbre, OrchestralNote note)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(timbre.Name);
		ValidateSpectra(timbre);
		Check(note.MidiNote, 24, 108, nameof(note.MidiNote));
		Check(note.Seconds, 0.1, 10, nameof(note.Seconds));
		Check(note.Velocity, 1, 127, nameof(note.Velocity));
		Check(note.SampleRate, 16000, 96000, nameof(note.SampleRate));
		Check(timbre.AttackSeconds, 0.001, 3, nameof(timbre.AttackSeconds));
		Check(timbre.ReleaseSeconds, 0.005, 3, nameof(timbre.ReleaseSeconds));
		Check(timbre.SpectralTransitionSeconds, 0.001, 3, nameof(timbre.SpectralTransitionSeconds));
		Check(timbre.HarmonicTilt, -1, 3, nameof(timbre.HarmonicTilt));
		ValidateExpression(timbre);
	}

	private static void ValidateExpression(OrchestralTimbre timbre)
	{
		Check(timbre.NoiseLevel, 0, 0.3, nameof(timbre.NoiseLevel));
		Check(timbre.NoiseCutoffHz, 50, 8000, nameof(timbre.NoiseCutoffHz));
		Check(timbre.VibratoCents, 0, 100, nameof(timbre.VibratoCents));
		Check(timbre.VibratoHz, 0, 12, nameof(timbre.VibratoHz));
		Check(timbre.VibratoDelaySeconds, 0, 5, nameof(timbre.VibratoDelaySeconds));
		Check(timbre.PitchDriftCents, 0, 50, nameof(timbre.PitchDriftCents));
		Check(timbre.VelocityBrightness, 0, 2, nameof(timbre.VelocityBrightness));
	}

	private static void ValidateSpectra(OrchestralTimbre timbre)
	{
		ArgumentNullException.ThrowIfNull(timbre.AttackSpectrum);
		ArgumentNullException.ThrowIfNull(timbre.SustainSpectrum);
		if (timbre.AttackSpectrum.Count is < 1 or > 64 || timbre.AttackSpectrum.Count != timbre.SustainSpectrum.Count)
		{
			throw new ArgumentException("Supply matching attack/sustain spectra of one to 64 harmonics.");
		}
		foreach (var spectrum in new[] { timbre.AttackSpectrum, timbre.SustainSpectrum })
		{
			foreach (var value in spectrum) { Check(value, 0, 2, "Harmonic amplitude"); }
			if (spectrum.Sum() <= 0) { throw new ArgumentException("Each spectrum must contain a nonzero harmonic."); }
		}
	}

	private static void Check(double value, double minimum, double maximum, string name)
	{
		if (!double.IsFinite(value) || value < minimum || value > maximum)
		{
			throw new ArgumentOutOfRangeException(name, "Value must be finite and within its documented range.");
		}
	}
}
