namespace PanoramicData.MusicGeneration;

/// <summary>Writes 16-bit mono PCM WAV files.</summary>
public static class WavEncoder
{
	/// <summary>Encodes samples (clamped to -1 to 1) as a WAV file.</summary>
	/// <param name="samples">Mono samples.</param>
	/// <param name="sampleRate">Sample rate in hertz.</param>
	/// <returns>The file's bytes.</returns>
	public static byte[] Encode(ReadOnlySpan<float> samples, int sampleRate)
	{
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sampleRate, 0);
		using var stream = new MemoryStream(44 + (samples.Length * 2));
		using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
		{
			var dataBytes = samples.Length * 2;
			writer.Write("RIFF"u8);
			writer.Write(36 + dataBytes);
			writer.Write("WAVE"u8);
			writer.Write("fmt "u8);
			writer.Write(16);
			writer.Write((short)1);
			writer.Write((short)1);
			writer.Write(sampleRate);
			writer.Write(sampleRate * 2);
			writer.Write((short)2);
			writer.Write((short)16);
			writer.Write("data"u8);
			writer.Write(dataBytes);
			foreach (var sample in samples)
			{
				writer.Write((short)Math.Round(Math.Clamp(sample, -1f, 1f) * 32767));
			}
		}

		return stream.ToArray();
	}
}
