using MeltySynth;

namespace PanoramicData.MusicGeneration.SoundFonts;

/// <summary>A single live SF2 note. Render blocks sequentially; release uses the bank's own note-off envelope.</summary>
public sealed class SoundFontKeyboardVoice
{
	private readonly Synthesizer _synth;
	private readonly int _midi;
	private bool _released;

	internal SoundFontKeyboardVoice(Synthesizer synth, int midi, int velocity)
	{
		_synth = synth;
		_midi = midi;
		_synth.NoteOn(0, midi, velocity);
	}

	/// <summary>Lifts the key and applies the SF2 release envelope; repeated releases have no further effect.</summary>
	public void Release()
	{
		if (_released) { return; }
		_released = true;
		_synth.NoteOff(0, _midi);
	}

	/// <summary>Renders 1–4096 mono samples without peak normalisation, preserving velocity and natural decay.</summary>
	public float[] Render(int sampleCount)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(sampleCount, 1);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(sampleCount, 4096);
		var left = new float[sampleCount];
		var right = new float[sampleCount];
		_synth.Render(left, right);
		for (var i = 0; i < sampleCount; i++) { left[i] = (left[i] + right[i]) * 0.5f; }
		return left;
	}
}
