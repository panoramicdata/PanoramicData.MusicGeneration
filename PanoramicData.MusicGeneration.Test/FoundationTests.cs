namespace PanoramicData.MusicGeneration.Test;

public class FoundationTests
{
	[Fact]
	public void Pcg32_IsDeterministicAndStreamsAreIndependent()
	{
		var a = new Pcg32(1, 0);
		var b = new Pcg32(1, 0);
		var c = new Pcg32(1, 1);
		var first = Enumerable.Range(0, 8).Select(_ => a.Next()).ToArray();
		first.Should().Equal(Enumerable.Range(0, 8).Select(_ => b.Next()));
		first.Should().NotEqual(Enumerable.Range(0, 8).Select(_ => c.Next()));
	}

	[Fact]
	public void Pcg32_GoldenValues_NeverChange()
	{
		var rng = new Pcg32(42, 54);
		var values = Enumerable.Range(0, 4).Select(_ => rng.Next()).ToArray();
		values.Should().Equal(GoldenPcg);
	}

	// These are the reference outputs of the published PCG32 demo for seed 42, stream 54 (0xA15C02B7, 0x7B47F409, 0xBA1D3330, 0x83D2F293).
	private static readonly uint[] GoldenPcg = [0xA15C02B7, 0x7B47F409, 0xBA1D3330, 0x83D2F293];

	[Fact]
	public void Pcg32_DoublesAndRangesStayInBounds()
	{
		var rng = new Pcg32(5, 5);
		for (var i = 0; i < 10000; i++)
		{
			rng.NextDouble().Should().BeInRange(0, 0.9999999999);
			rng.Next(-3, 4).Should().BeInRange(-3, 3);
		}
	}

	[Fact]
	public void Pcg32_IsRoughlyUniform()
	{
		var rng = new Pcg32(8, 1);
		var buckets = new int[10];
		for (var i = 0; i < 100000; i++)
		{
			buckets[(int)(rng.NextDouble() * 10)]++;
		}

		buckets.Should().OnlyContain(count => count > 9500 && count < 10500);
	}

	[Fact]
	public void DeterministicSin_AgreesWithMathSin()
	{
		for (var x = -300.0; x <= 300.0; x += 0.0173)
		{
			DeterministicMath.Sin(x).Should().BeApproximately(Math.Sin(x), 1e-12);
		}

		DeterministicMath.Sin(0).Should().Be(0);
	}

	[Fact]
	public void Pitch_FollowsEqualTemperament()
	{
		Pitch.FrequencyOf(69).Should().BeApproximately(440, 1e-9);
		Pitch.FrequencyOf(60).Should().BeApproximately(261.6256, 1e-3);
		Pitch.FrequencyOf(81).Should().BeApproximately(880, 1e-9);
		Pitch.FrequencyOf(70).Should().BeApproximately(440 * Math.Pow(2, 1 / 12.0), 1e-9);
		Pitch.FrequencyOf(69, 442).Should().BeApproximately(442, 1e-9);
	}

	[Theory]
	[InlineData(60, "C4")]
	[InlineData(69, "A4")]
	[InlineData(61, "C#4")]
	[InlineData(36, "C2")]
	[InlineData(11, "B-1")]
	public void Pitch_NamesNotesInScientificNotation(int midi, string name) => Pitch.NameOf(midi).Should().Be(name);

	[Fact]
	public void Scale_MapsDegreesAcrossOctaves()
	{
		Scales.Major.MidiOf(60, 0).Should().Be(60);
		Scales.Major.MidiOf(60, 2).Should().Be(64);
		Scales.Major.MidiOf(60, 7).Should().Be(72);
		Scales.Major.MidiOf(60, 9).Should().Be(76);
		Scales.MinorPentatonic.MidiOf(60, 5).Should().Be(72);
		Scales.Major.Count.Should().Be(7);
		Scales.All.Should().HaveCount(5);
		Scales.All.Should().OnlyContain(s => s.Semitones[0] == 0 && s.Semitones.SequenceEqual(s.Semitones.Order()));
		_ = Scales.NaturalMinor;
		_ = Scales.MajorPentatonic;
		_ = Scales.Dorian;
	}

	[Fact]
	public void SongInfo_ReferencePitchFollowsTheDetune()
	{
		new SongInfo(1, 100, "Major", 40, 0, 0).ReferenceHz.Should().BeApproximately(440, 1e-9);
		new SongInfo(1, 100, "Major", 40, 100, 0).ReferenceHz.Should().BeApproximately(440 * Math.Pow(2, 1 / 12.0), 1e-9);
	}

	public static TheoryData<SongStyle> InvalidStyles => new()
	{
		new SongStyle { MinTempoBpm = 10 },
		new SongStyle { MinTempoBpm = 120, MaxTempoBpm = 100 },
		new SongStyle { Scales = [] },
		new SongStyle { Scales = null! },
		new SongStyle { RootMidiLow = 5 },
		new SongStyle { RootMidiLow = 50, RootMidiHigh = 40 },
		new SongStyle { MelodyLowMidi = 80, MelodyHighMidi = 85 },
		new SongStyle { MelodyHighMidi = 120 },
		new SongStyle { MaxDetuneCents = 80 },
		new SongStyle { MaxDetuneCents = -5 },
		new SongStyle { MaxTempoBpm = 400 },
		new SongStyle { MinTempoBpm = 400, MaxTempoBpm = 400 },
		new SongStyle { RootMidiLow = 80, RootMidiHigh = 80 },
		new SongStyle { RootMidiHigh = 80 },
		new SongStyle { MelodyLowMidi = 10 },
		new SongStyle { MelodyLowMidi = 110, MelodyHighMidi = 120 }
	};

	[Fact]
	public void DefaultStyle_IsValid() => new SongStyle().Validate();

	[Theory]
	[MemberData(nameof(InvalidStyles))]
	public void Style_RejectsInconsistentValues(SongStyle style)
	{
		var act = style.Validate;
		act.Should().Throw<ArgumentException>();
	}

	[Fact]
	public void WavEncoder_ClampsAndRejectsABadRate()
	{
		var wav = WavEncoder.Encode([2f, -2f, 0f], 8000);
		BitConverter.ToInt16(wav, 44).Should().Be(32767);
		BitConverter.ToInt16(wav, 46).Should().Be(-32767);
		BitConverter.ToInt16(wav, 48).Should().Be(0);
		var act = () => WavEncoder.Encode([0f], 0);
		act.Should().Throw<ArgumentOutOfRangeException>();
	}
}
