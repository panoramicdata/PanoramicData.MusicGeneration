namespace PanoramicData.MusicGeneration;

/// <summary>
/// A small, fixed pseudo-random generator (PCG-XSH-RR, 64-bit state, 32-bit output). It is part of this library rather than
/// <see cref="Random"/> because the sequence a seeded <see cref="Random"/> produces is not guaranteed to stay the same between .NET versions,
/// and a song generated from a seed has to be the same song for ever. Not for cryptography.
/// </summary>
internal sealed class Pcg32
{
	private const ulong Multiplier = 6364136223846793005UL;
	private ulong _state;
	private readonly ulong _increment;

	/// <summary>Creates a generator for a seed and a stream number; different streams are independent sequences.</summary>
	public Pcg32(long seed, long stream)
	{
		_state = 0;
		_increment = ((ulong)stream << 1) | 1UL;
		Next();
		_state += (ulong)seed;
		Next();
	}

	/// <summary>The next 32 random bits.</summary>
	public uint Next()
	{
		var old = _state;
		unchecked
		{
			_state = (old * Multiplier) + _increment;
			var xorShifted = (uint)(((old >> 18) ^ old) >> 27);
			var rotation = (int)(old >> 59);
			return (xorShifted >> rotation) | (xorShifted << ((-rotation) & 31));
		}
	}

	/// <summary>A uniformly distributed value in [0, 1).</summary>
	public double NextDouble() => Next() / 4294967296.0;

	/// <summary>A uniformly distributed integer in [min, max).</summary>
	public int Next(int min, int max) => min + (int)(NextDouble() * (max - min));
}
