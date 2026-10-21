namespace PanoramicData.MusicGeneration.Test;

public class OrchestralSynthesisTests
{
	private static readonly OrchestralNote Note = new() { MidiNote = 69, Seconds = 1, Velocity = 127 };
	private static readonly OrchestralTimbre Pure = new()
	{
		Name = "Test",
		AttackSpectrum = [1, 0, 0],
		SustainSpectrum = [1, 0, 0],
		NoiseLevel = 0,
		VibratoCents = 0,
		PitchDriftCents = 0
	};

	[Fact]
	public void MeasuredProfiles_AreRepeatableFiniteAndIncludeARelease()
	{
		foreach (var profile in new[] { OrchestralTimbres.Flute, OrchestralTimbres.Violin, OrchestralTimbres.Trumpet })
		{
			var samples = OrchestralSynthesizer.Render(profile, Note);
			samples.Should().Equal(OrchestralSynthesizer.Render(profile, Note));
			samples.Length.Should().Be((int)Math.Ceiling((Note.Seconds + profile.ReleaseSeconds) * Note.SampleRate));
			samples.Should().OnlyContain(x => float.IsFinite(x) && Math.Abs(x) <= 1);
			samples[0].Should().Be(0);
			Math.Abs(samples[^1]).Should().BeLessThan(0.0001f);
		}
	}

	[Fact]
	public void AttackAndSustainSpectra_CreateDifferentHarmonicEvolution()
	{
		var evolving = Pure with { AttackSeconds = 0.001, AttackSpectrum = [1, 1, 0], SpectralTransitionSeconds = 0.15 };
		var samples = OrchestralSynthesizer.Render(evolving, Note);
		var early = Harmonic(samples, 880, 0.01) / Harmonic(samples, 440, 0.01);
		var late = Harmonic(samples, 880, 0.7) / Harmonic(samples, 440, 0.7);
		early.Should().BeGreaterThan(late * 20);
	}

	[Fact]
	public void SoftVelocity_ChangesBothLevelAndSpectralBalance()
	{
		var profile = Pure with { AttackSpectrum = [1, 1, 0], SustainSpectrum = [1, 1, 0], VelocityBrightness = 2 };
		var hard = OrchestralSynthesizer.Render(profile, Note);
		var soft = OrchestralSynthesizer.Render(profile, Note with { Velocity = 40 });
		Harmonic(soft, 440, 0.5).Should().BeLessThan(Harmonic(hard, 440, 0.5));
		(Harmonic(soft, 880, 0.5) / Harmonic(soft, 440, 0.5)).Should().BeLessThan(0.5);
	}

	[Fact]
	public void HarmonicsAboveNyquist_AreOmittedRatherThanFoldedBack()
	{
		var profile = Pure with { AttackSpectrum = [0, 0, 1], SustainSpectrum = [0, 0, 1] };
		OrchestralSynthesizer.Render(profile, Note with { MidiNote = 108 }).Should().OnlyContain(s => s == 0);
	}

	[Fact]
	public void RegisterAnchors_InterpolateColourWithoutUsingRecordedAudio()
	{
		var profile = Pure with
		{
			RegisterSpectra = [new(60, [1, 0, 0], [1, 0, 0]), new(72, [0, 1, 0], [0, 1, 0])]
		};
		var note = Note with { MidiNote = 66 };
		var actual = OrchestralSynthesizer.Render(profile, note);
		var expected = OrchestralSynthesizer.Render(Pure with { AttackSpectrum = [0.5, 0.5, 0], SustainSpectrum = [0.5, 0.5, 0] }, note);
		actual.Should().Equal(expected);
	}

	[Fact]
	public void InvalidImportedParameters_AreRejectedBeforeAllocation()
	{
		var render = () => OrchestralSynthesizer.Render(Pure with { NoiseLevel = double.NaN }, Note);
		render.Should().Throw<ArgumentOutOfRangeException>();
		render = () => OrchestralSynthesizer.Render(Pure with { SustainSpectrum = [1] }, Note);
		render.Should().Throw<ArgumentException>();
		render = () => OrchestralSynthesizer.Render(Pure, Note with { Seconds = double.PositiveInfinity });
		render.Should().Throw<ArgumentOutOfRangeException>();
	}

	private static double Harmonic(float[] samples, double frequency, double start)
	{
		var real = 0.0;
		var imaginary = 0.0;
		var first = (int)(start * Note.SampleRate);
		for (var i = 0; i < Note.SampleRate / 10; i++)
		{
			var phase = 2 * Math.PI * frequency * i / Note.SampleRate;
			real += samples[first + i] * Math.Cos(phase);
			imaginary += samples[first + i] * Math.Sin(phase);
		}
		return Math.Sqrt(real * real + imaginary * imaginary);
	}
}
