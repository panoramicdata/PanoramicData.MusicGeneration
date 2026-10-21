namespace PanoramicData.MusicGeneration;

/// <summary>Experimental profiles measured from Iowa MIS C5 recordings. No recorded PCM is included.</summary>
public static class OrchestralTimbres
{
	/// <summary>Flute: C5 spectral fit; other pitches extrapolate the same harmonic ratios.</summary>
	public static OrchestralTimbre Flute { get; } = new()
	{
		Name = "Experimental flute v1",
		AttackSpectrum = Array.AsReadOnly<double>([0.59651, 0.23636, 0.04957, 0.09907, 0.01128, 0.00193, 0.00039, 0.00034, 0.00021, 0.00035, 0.00014, 0.00053, 0.00057, 0.00045, 0.00046, 0.00045, 0.00016, 0.00016, 0.00023, 0.00012, 0.00036, 0.00012, 9e-05, 0.00013]),
		SustainSpectrum = Array.AsReadOnly<double>([0.40857, 0.25832, 0.1542, 0.10634, 0.02779, 0.00558, 0.00875, 0.01034, 0.00357, 0.00261, 0.00286, 0.00164, 0.00154, 0.00159, 0.0006, 0.0005, 0.00082, 0.00061, 0.00054, 0.00065, 0.00109, 0.00053, 0.0004, 0.00057]),
		AttackSeconds = 0.18,
		NoiseLevel = 0.025,
		VibratoCents = 5
	};

	/// <summary>Violin: C5 spectral fit; other pitches extrapolate the same harmonic ratios.</summary>
	public static OrchestralTimbre Violin { get; } = new()
	{
		Name = "Experimental violin v1",
		AttackSpectrum = Array.AsReadOnly<double>([0.03632, 0.41389, 0.23084, 0.02438, 0.23973, 0.01263, 0.00795, 0.01801, 0.00087, 0.00058, 0.0021, 0.00388, 0.00097, 0.00346, 0.0002, 0.00027, 0.00062, 0.0013, 0.00036, 0.00023, 0.00057, 0.00025, 0.0003, 0.00029]),
		SustainSpectrum = Array.AsReadOnly<double>([0.0428, 0.19112, 0.16042, 0.10888, 0.27222, 0.04869, 0.02276, 0.04727, 0.02152, 0.00618, 0.01266, 0.01634, 0.00762, 0.0139, 0.0034, 0.00233, 0.00166, 0.00437, 0.00157, 0.00298, 0.00419, 0.00302, 0.00202, 0.00208]),
		AttackSeconds = 0.37,
		NoiseLevel = 0.04,
		VibratoCents = 12
	};

	/// <summary>Trumpet: C5 spectral fit; other pitches extrapolate the same harmonic ratios.</summary>
	public static OrchestralTimbre Trumpet { get; } = new()
	{
		Name = "Experimental trumpet v1",
		AttackSpectrum = Array.AsReadOnly<double>([0.18186, 0.37438, 0.30523, 0.06193, 0.04205, 0.01979, 0.0071, 0.0036, 0.00221, 0.00082, 0.00044, 0.00026, 0.00015, 6e-05, 5e-05, 2e-05, 1e-05, 1e-05, 0.0, 0.0, 1e-05, 1e-05, 1e-05, 1e-05]),
		SustainSpectrum = Array.AsReadOnly<double>([0.0985, 0.25746, 0.30167, 0.11594, 0.07735, 0.06334, 0.02543, 0.01759, 0.01441, 0.01011, 0.00558, 0.0038, 0.00275, 0.00173, 0.00134, 0.00101, 0.00042, 8e-05, 0.00011, 0.00026, 0.00029, 0.00026, 0.00029, 0.00028]),
		AttackSeconds = 0.28,
		NoiseLevel = 0.015,
		VibratoCents = 5
	};

}
