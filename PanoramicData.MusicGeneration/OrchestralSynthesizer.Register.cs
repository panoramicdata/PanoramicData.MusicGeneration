namespace PanoramicData.MusicGeneration;

public static partial class OrchestralSynthesizer
{
	private static (double[] Attack, double[] Sustain) ResolveSpectra(OrchestralTimbre timbre, int midi)
	{
		var anchors = timbre.RegisterSpectra.OrderBy(a => a.MidiNote).ToArray();
		if (anchors.Length == 0) { return (timbre.AttackSpectrum.ToArray(), timbre.SustainSpectrum.ToArray()); }
		var lower = anchors.LastOrDefault(a => a.MidiNote <= midi) ?? anchors[0];
		var upper = anchors.FirstOrDefault(a => a.MidiNote >= midi) ?? anchors[^1];
		var amount = lower.MidiNote == upper.MidiNote ? 0 : (double)(midi - lower.MidiNote) / (upper.MidiNote - lower.MidiNote);
		return (Interpolate(lower.AttackSpectrum, upper.AttackSpectrum, amount),
			Interpolate(lower.SustainSpectrum, upper.SustainSpectrum, amount));
	}

	private static double[] Interpolate(IReadOnlyList<double> lower, IReadOnlyList<double> upper, double amount) =>
		[.. Enumerable.Range(0, lower.Count).Select(i => lower[i] + amount * (upper[i] - lower[i]))];
}
