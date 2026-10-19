namespace PanoramicData.MusicGeneration.Test;

public class VoiceTests
{
	[Fact]
	public void DefaultPad_IsBelowMelodyAndQuieterInTheRawMix()
	{
		for (var seed = 0; seed < 12; seed++)
		{
			var song = SongGenerator.GenerateStems(seed, 12, 8000);
			song.Notes.Where(n => n.Voice == VoiceKind.Pad).Should().NotBeEmpty()
				.And.OnlyContain(n => n.MidiNote < 60);
			var padEnergy = song.Stems[VoiceKind.Pad].Sum(v => v * v);
			var melodyEnergy = song.Stems[VoiceKind.Melody].Sum(v => v * v);
			padEnergy.Should().BeLessThan(melodyEnergy);
		}
	}

	[Theory]
	[InlineData(VoiceKind.Pad)]
	[InlineData(VoiceKind.Bass)]
	[InlineData(VoiceKind.Melody)]
	[InlineData(VoiceKind.Kick)]
	[InlineData(VoiceKind.Snare)]
	[InlineData(VoiceKind.HiHat)]
	public void MutingOneVoice_PreservesOtherNotesAndRawSamples(VoiceKind muted)
	{
		var style = new SongStyle();
		var full = SongGenerator.GenerateStems(42, 8, 8000, style);
		style = muted switch
		{
			VoiceKind.Pad => style with { Pad = false },
			VoiceKind.Bass => style with { Bass = false },
			VoiceKind.Melody => style with { Melody = false },
			VoiceKind.Kick => style with { Kick = false },
			VoiceKind.Snare => style with { Snare = false },
			_ => style with { HiHat = false }
		};
		var filtered = SongGenerator.GenerateStems(42, 8, 8000, style);
		filtered.Notes.Should().Equal(full.Notes.Where(n => n.Voice != muted));
		filtered.Stems[muted].Should().OnlyContain(v => v == 0);
		foreach (var voice in Enum.GetValues<VoiceKind>().Where(v => v != muted))
		{
			filtered.Stems[voice].Should().Equal(full.Stems[voice]);
		}
	}

	[Fact]
	public void Stems_SumToTheMixWithTheReportedGain()
	{
		var song = SongGenerator.GenerateStems(7, 8, 8000);
		var ordinary = SongGenerator.Generate(7, 8, 8000);
		song.Samples.Should().Equal(ordinary.Samples);
		song.Notes.Should().Equal(ordinary.Notes);
		ordinary.Stems.Should().BeEmpty();
		for (var i = 0; i < song.Samples.Length; i++)
		{
			var sum = song.Stems.Values.Sum(stem => stem[i]);
			((float)(sum * song.NormalisationGain)).Should().BeApproximately(song.Samples[i], 1e-7f);
		}
	}

	[Fact]
	public void Vibrato_IsBoundedAndDelayedWithALinearRamp()
	{
		var style = new SongStyle();
		SongGenerator.VibratoFraction(0.1, 1, 5, style, false).Should().Be(0);
		SongGenerator.VibratoFraction(0.15, 1, 5, style, false).Should().Be(0);
		SongGenerator.VibratoFraction(0.25, 1, 5, style, false).Should().BeApproximately(style.VibratoDepth / 2, 1e-12);
		SongGenerator.VibratoFraction(0.45, 1, 5, style, false).Should().BeApproximately(style.VibratoDepth, 1e-12);
		for (var i = 0; i < 1000; i++)
		{
			Math.Abs(SongGenerator.VibratoFraction(i / 1000.0, 1, 5, style, false)).Should().BeLessThanOrEqualTo(style.VibratoDepth);
			SongGenerator.VibratoFraction(i / 1000.0, 0.3, 5, style, false).Should().Be(0);
		}
	}

	[Fact]
	public void ZeroDepth_RendersTheSameStraightToneAsZeroRate()
	{
		var style = new SongStyle { Pad = false, Bass = false, Drums = false };
		var zeroDepth = SongGenerator.GenerateStems(11, 8, 8000, style with { VibratoDepth = 0 });
		var zeroRate = SongGenerator.GenerateStems(11, 8, 8000, style with { VibratoMinHz = 0, VibratoMaxHz = 0 });
		zeroDepth.Stems[VoiceKind.Melody].Should().Equal(zeroRate.Stems[VoiceKind.Melody]);
		var vibrato = SongGenerator.GenerateStems(11, 8, 8000, style);
		var first = vibrato.Notes[0];
		var endOfDelay = (int)((first.StartSeconds + Math.Min(first.DurationSeconds, style.VibratoDelaySeconds)) * 8000);
		vibrato.Stems[VoiceKind.Melody].Take(endOfDelay).Should().Equal(zeroDepth.Stems[VoiceKind.Melody].Take(endOfDelay));
		vibrato.Stems[VoiceKind.Melody].Should().NotEqual(zeroDepth.Stems[VoiceKind.Melody]);
	}

	[Fact]
	public void InvalidSoundOptions_AreRejected()
	{
		SongStyle[] invalid =
		[
			new() { PadOctaveOffset = 4 }, new() { PadLevel = double.NaN }, new() { PadAttackSeconds = 0 },
			new() { VibratoDepth = -1 }, new() { VibratoDepth = double.PositiveInfinity },
			new() { VibratoMinHz = 8, VibratoMaxHz = 2 }, new() { VibratoMaxHz = double.NaN },
			new() { VibratoDelaySeconds = -1 }, new() { VibratoRampSeconds = 0 }
		];
		foreach (var style in invalid)
		{
			var validate = style.Validate;
			validate.Should().Throw<ArgumentException>();
		}
	}
}
