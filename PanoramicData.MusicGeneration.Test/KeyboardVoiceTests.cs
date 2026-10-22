using PanoramicData.MusicGeneration.SoundFonts;

namespace PanoramicData.MusicGeneration.Test;

public class KeyboardVoiceTests
{
	private static readonly Lazy<SoundFontRenderer> Bank = new(() => new(
		File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "TestAssets", "GeneralUser-GS.sf2")), "GeneralUser GS"));

	[Theory]
	[InlineData(0)]
	[InlineData(16)]
	public void KeyRelease_DampsTheSoundWhileAHeldKeyContinues(int program)
	{
		var held = Bank.Value.CreateKeyboardVoice(program, 60, 100);
		var released = Bank.Value.CreateKeyboardVoice(program, 60, 100);
		for (var i = 0; i < 5; i++) { released.Render(882).Should().Equal(held.Render(882)); }
		released.Release();
		released.Release();
		for (var i = 0; i < 30; i++) { _ = released.Render(882); _ = held.Render(882); }
		var heldLevel = Rms(held.Render(4096));
		var dampedLevel = Rms(released.Render(4096));
		heldLevel.Should().BeGreaterThan(0.0001);
		dampedLevel.Should().BeLessThan(heldLevel * 0.1);
	}

	[Fact]
	public void ReleasingOneVoice_DoesNotDampAnotherVoiceOfTheSamePitch()
	{
		var first = Bank.Value.CreateKeyboardVoice(0, 60, 100);
		var second = Bank.Value.CreateKeyboardVoice(0, 60, 100);
		var control = Bank.Value.CreateKeyboardVoice(0, 60, 100);
		first.Release();
		for (var i = 0; i < 20; i++)
		{
			_ = first.Render(882);
			second.Render(882).Should().Equal(control.Render(882));
		}
	}

	[Fact]
	public void LiveRendering_PreservesVelocityDynamicsAndRejectsInvalidSizes()
	{
		var soft = Bank.Value.CreateKeyboardVoice(0, 60, 32);
		var hard = Bank.Value.CreateKeyboardVoice(0, 60, 127);
		Rms(soft.Render(4096)).Should().BeLessThan(Rms(hard.Render(4096)) * 0.5);
		Assert.Throws<ArgumentOutOfRangeException>(() => soft.Render(0));
		Assert.Throws<ArgumentOutOfRangeException>(() => soft.Render(4097));
		Assert.Throws<ArgumentOutOfRangeException>(() => Bank.Value.CreateKeyboardVoice(0, -1, 90));
		Assert.Throws<ArgumentOutOfRangeException>(() => Bank.Value.CreateKeyboardVoice(0, 60, 0));
	}

	private static double Rms(float[] samples) => Math.Sqrt(samples.Average(s => (double)s * s));
}
