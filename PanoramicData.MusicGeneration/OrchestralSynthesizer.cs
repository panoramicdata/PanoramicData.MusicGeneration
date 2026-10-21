namespace PanoramicData.MusicGeneration;

/// <summary>Experimental sample-free harmonic/noise synthesis. Not a claim of perceptual equivalence.</summary>
public static partial class OrchestralSynthesizer
{
	/// <summary>Generates a note and release. Repeatable on the same runtime; does not affect legacy synthesis.</summary>
	public static float[] Render(OrchestralTimbre timbre, OrchestralNote note)
	{
		ArgumentNullException.ThrowIfNull(timbre);
		ArgumentNullException.ThrowIfNull(note);
		Validate(timbre, note);
		var attack = timbre.AttackSpectrum.ToArray();
		var sustain = timbre.SustainSpectrum.ToArray();
		var frames = checked((int)Math.Ceiling((note.Seconds + timbre.ReleaseSeconds) * note.SampleRate));
		var samples = new float[frames];
		var rng = new Pcg32(note.Seed, 710);
		var phase = 0.0;
		var lowNoise = 0.0;
		var corner = 1 - Math.Exp(-2 * Math.PI * timbre.NoiseCutoffHz / note.SampleRate);
		var level = 0.7 * Math.Pow(note.Velocity / 127.0, 1.5) / Math.Max(1, Math.Max(attack.Sum(), sustain.Sum()));
		for (var i = 0; i < frames; i++)
		{
			var time = (double)i / note.SampleRate;
			var frequency = Pitch.FrequencyOf(note.MidiNote) * Math.Pow(2, PitchCents(timbre, time, note.Seed) / 1200);
			phase += 2 * Math.PI * frequency / note.SampleRate;
			phase -= Math.Floor(phase / (2 * Math.PI)) * (2 * Math.PI);
			var tone = Harmonics(timbre, note, attack, sustain, time, phase, frequency);
			var white = rng.NextDouble() * 2 - 1;
			lowNoise += corner * (white - lowNoise);
			var noise = (white - lowNoise) * timbre.NoiseLevel * (0.5 + 0.5 * Math.Exp(-time / 0.1));
			samples[i] = (float)Math.Clamp(level * Envelope(timbre, note, time) * (tone + noise), -1, 1);
		}
		return samples;
	}

	private static double Harmonics(OrchestralTimbre timbre, OrchestralNote note, double[] attack,
		double[] sustain, double time, double phase, double frequency)
	{
		var transition = Math.Exp(-time / timbre.SpectralTransitionSeconds);
		var tilt = timbre.HarmonicTilt + (1 - note.Velocity / 127.0) * timbre.VelocityBrightness;
		var result = 0.0;
		for (var h = 1; h <= sustain.Length && h * frequency < note.SampleRate * 0.48; h++)
		{
			var amplitude = sustain[h - 1] + transition * (attack[h - 1] - sustain[h - 1]);
			result += amplitude * Math.Pow(h, -tilt) * Math.Sin(h * phase);
		}
		return result;
	}

	private static double PitchCents(OrchestralTimbre timbre, double time, int seed)
	{
		var rise = Math.Clamp((time - timbre.VibratoDelaySeconds) / 0.25, 0, 1);
		var vibrato = timbre.VibratoCents * rise * Math.Sin(2 * Math.PI * timbre.VibratoHz * time);
		var drift = 0.65 * Math.Sin(2 * Math.PI * 0.37 * time + seed) + 0.35 * Math.Sin(2 * Math.PI * 0.71 * time + seed * 0.3);
		return vibrato + timbre.PitchDriftCents * drift;
	}

	private static double Envelope(OrchestralTimbre timbre, OrchestralNote note, double time)
	{
		var attack = 0.5 - 0.5 * Math.Cos(Math.PI * Math.Clamp(time / timbre.AttackSeconds, 0, 1));
		var release = 0.5 + 0.5 * Math.Cos(Math.PI * Math.Clamp((time - note.Seconds) / timbre.ReleaseSeconds, 0, 1));
		return attack * release;
	}
}
