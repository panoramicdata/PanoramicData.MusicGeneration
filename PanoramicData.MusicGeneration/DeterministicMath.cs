namespace PanoramicData.MusicGeneration;

/// <summary>
/// Sine, built only from IEEE 754 basic operations (add, multiply, round), which every machine computes identically.
/// <see cref="Math.Sin(double)"/> defers to the platform's C runtime and may differ in the last bit between operating systems and CPUs.
/// </summary>
internal static class DeterministicMath
{
	private const double TwoOverPi = 0.63661977236758134308;
	private const double PiOver2High = 1.57079632673412561417;
	private const double PiOver2Low = 6.07710050650619224932e-11;

	/// <summary>Sine of an angle in radians.</summary>
	public static double Sin(double x)
	{
		var k = Math.Round(x * TwoOverPi);
		var r = x - (k * PiOver2High) - (k * PiOver2Low);
		var quadrant = (((long)k % 4) + 4) % 4;
		if (quadrant == 0)
		{
			return SinPoly(r);
		}

		if (quadrant == 1)
		{
			return CosPoly(r);
		}

		return quadrant == 2 ? -SinPoly(r) : -CosPoly(r);
	}

	private static double SinPoly(double r)
	{
		var r2 = r * r;
		var s = 1.0 / 355687428096000.0;
		s = (-s * r2) + (1.0 / 1307674368000.0);
		s = (-s * r2) + (1.0 / 6227020800.0);
		s = (-s * r2) + (1.0 / 39916800.0);
		s = (-s * r2) + (1.0 / 362880.0);
		s = (-s * r2) + (1.0 / 5040.0);
		s = (-s * r2) + (1.0 / 120.0);
		s = (-s * r2) + (1.0 / 6.0);
		s = (-s * r2) + 1.0;
		return r * s;
	}

	private static double CosPoly(double r)
	{
		var r2 = r * r;
		var c = 1.0 / 20922789888000.0;
		c = (-c * r2) + (1.0 / 87178291200.0);
		c = (-c * r2) + (1.0 / 479001600.0);
		c = (-c * r2) + (1.0 / 3628800.0);
		c = (-c * r2) + (1.0 / 40320.0);
		c = (-c * r2) + (1.0 / 720.0);
		c = (-c * r2) + (1.0 / 24.0);
		c = (-c * r2) + 0.5;
		return 1.0 - (c * r2);
	}
}
