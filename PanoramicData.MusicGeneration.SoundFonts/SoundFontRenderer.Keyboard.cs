namespace PanoramicData.MusicGeneration.SoundFonts;

public sealed partial class SoundFontRenderer
{
	/// <summary>Starts a live bank-0 note, with caller-controlled hold and release. Render and release sequentially.</summary>
	public SoundFontKeyboardVoice CreateKeyboardVoice(int program, int midi, int velocity, int sampleRate = 22050)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(midi, 0);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(midi, 127);
		ArgumentOutOfRangeException.ThrowIfLessThan(velocity, 1);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(velocity, 127);
		ArgumentOutOfRangeException.ThrowIfLessThan(sampleRate, 16000);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(sampleRate, 96000);
		var part = new SoundFontPart(VoiceKind.Melody, 0, program, velocity);
		ValidatePart(part);
		return new(CreateSynth(sampleRate, part, 0), midi, velocity);
	}
}
