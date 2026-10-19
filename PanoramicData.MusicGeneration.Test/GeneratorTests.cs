namespace PanoramicData.MusicGeneration.Test;

public class GeneratorTests
{
	private static ulong Fnv(ReadOnlySpan<float> samples)
	{
		var h = 14695981039346656037UL;
		foreach (var s in samples)
		{
			h = (h ^ BitConverter.SingleToUInt32Bits(s)) * 1099511628211UL;
		}

		return h;
	}

	[Fact]
	public void SameSeed_GivesTheSameSamplesAndNotes()
	{
		var a = SongGenerator.Generate(7, 12, 11025);
		var b = SongGenerator.Generate(7, 12, 11025);

		a.Samples.Should().Equal(b.Samples);
		a.Notes.Should().Equal(b.Notes);
		a.Info.Should().Be(b.Info);
	}

	[Fact]
	public void GoldenHash_PinsTheOutputAcrossMachinesAndVersions()
	{
		var song = SongGenerator.Generate(42, 8, 11025);
		song.Info.TempoBpm.Should().Be(GoldenTempo);
		Fnv(song.Samples).Should().Be(GoldenHash);
	}

	private const int GoldenTempo = 87;
	private const ulong GoldenHash = 8783139537262375303UL;

	[Fact]
	public void AlgorithmVersion_IsPinnedWithTheGoldenHash() => SongGenerator.Version.Should().Be(3, "version 3 changes phrase-fitted forms while retaining the default audio hash");

	[Fact]
	public void DifferentSeeds_GiveDifferentSongs()
	{
		var hashes = Enumerable.Range(0, 20).Select(seed => Fnv(SongGenerator.Generate(seed, 6, 8000).Samples)).ToHashSet();
		hashes.Should().HaveCount(20);
	}

	[Fact]
	public void Song_HasTheRequestedLengthAndPeaksAtZeroPointNine()
	{
		var song = SongGenerator.Generate(3, 10, 22050);

		song.Samples.Length.Should().Be(220500);
		song.SampleRate.Should().Be(22050);
		song.Duration.TotalSeconds.Should().BeApproximately(10, 0.001);
		song.Samples.Max(Math.Abs).Should().BeApproximately(0.9f, 0.001f);
	}

	[Fact]
	public void DifferentSampleRates_PlayTheSameNotes()
	{
		var low = SongGenerator.Generate(9, 8, 11025);
		var high = SongGenerator.Generate(9, 8, 44100);
		low.Notes.Should().Equal(high.Notes);
		low.Info.Should().Be(high.Info);
	}

	[Fact]
	public void Notes_AreInStartOrderAndInsideTheStyleRanges()
	{
		var style = new SongStyle();
		for (var seed = 0; seed < 30; seed++)
		{
			var song = SongGenerator.Generate(seed, 20, 8000, style);
			song.Notes.Select(n => n.StartSeconds).Should().BeInAscendingOrder();
			var melody = song.Notes.Where(n => n.Voice == VoiceKind.Melody).ToList();
			melody.Should().NotBeEmpty();
			melody.Should().OnlyContain(n => n.MidiNote >= style.MelodyLowMidi && n.MidiNote <= style.MelodyHighMidi);
			song.Notes.Where(n => n.Voice == VoiceKind.Bass).Should().OnlyContain(n => n.MidiNote >= style.RootMidiLow && n.MidiNote <= style.RootMidiHigh + 24);
			song.Info.TempoBpm.Should().BeInRange(style.MinTempoBpm, style.MaxTempoBpm);
			song.Info.RootMidi.Should().BeInRange(style.RootMidiLow, style.RootMidiHigh);
			Math.Abs(song.Info.DetuneCents).Should().BeLessThanOrEqualTo(style.MaxDetuneCents);
		}
	}

	[Fact]
	public void MelodyNotes_BelongToTheSongsScale()
	{
		for (var seed = 0; seed < 25; seed++)
		{
			var song = SongGenerator.Generate(seed, 20, 8000);
			var scale = Scales.All.Single(s => s.Name == song.Info.ScaleName);
			var allowed = scale.Semitones.Select(s => (song.Info.RootMidi + s) % 12).ToHashSet();
			song.Notes.Where(n => n.Voice == VoiceKind.Melody).Should().OnlyContain(n => allowed.Contains(n.MidiNote % 12));
		}
	}

	[Fact]
	public void RenderedFrequency_IncludesTheSongsTuning()
	{
		var song = SongGenerator.Generate(5, 10, 11025);
		var note = song.Notes.First(n => n.Voice == VoiceKind.Melody);
		note.FrequencyHz.Should().BeApproximately(Pitch.FrequencyOf(note.MidiNote, song.Info.ReferenceHz), 1e-9);
	}

	[Fact]
	public void MelodyNote_PutsItsEnergyAtItsFrequency()
	{
		// A Goertzel filter at the note's frequency should respond far more strongly than one a fifth higher.
		var song = SongGenerator.Generate(11, 20, 22050, new SongStyle { Pad = false, Bass = false, Drums = false });
		var note = song.Notes.Where(n => n.Voice == VoiceKind.Melody && n.DurationSeconds > 0.4).OrderBy(n => n.StartSeconds).First();
		var start = (int)((note.StartSeconds + 0.05) * song.SampleRate);
		var length = (int)(0.2 * song.SampleRate);
		var at = Goertzel(song.Samples.AsSpan(start, length), note.FrequencyHz, song.SampleRate);
		var off = Goertzel(song.Samples.AsSpan(start, length), note.FrequencyHz * 1.41, song.SampleRate);

		at.Should().BeGreaterThan(off * 20);
	}

	private static double Goertzel(ReadOnlySpan<float> x, double frequency, int rate)
	{
		var w = 2 * Math.PI * frequency / rate;
		var coefficient = 2 * Math.Cos(w);
		double s1 = 0, s2 = 0;
		foreach (var v in x)
		{
			var s0 = v + (coefficient * s1) - s2;
			s2 = s1;
			s1 = s0;
		}

		return (s1 * s1) + (s2 * s2) - (coefficient * s1 * s2);
	}

	[Fact]
	public void PlatformSine_KeepsTheNotesAndChoicesAndStaysClose()
	{
		var exact = SongGenerator.Generate(4, 8, 11025);
		var fast = SongGenerator.Generate(4, 8, 11025, new SongStyle { UsePlatformSine = true });

		fast.Notes.Should().Equal(exact.Notes);
		fast.Info.Should().Be(exact.Info);
		var worst = exact.Samples.Zip(fast.Samples, (a, b) => Math.Abs(a - b)).Max();
		worst.Should().BeLessThan(1e-4f);
	}

	[Fact]
	public void SwitchingVoicesOff_RemovesTheirNotesAndSound()
	{
		var melodyOnly = SongGenerator.Generate(2, 12, 8000, new SongStyle { Pad = false, Bass = false, Drums = false });
		melodyOnly.Notes.Should().OnlyContain(n => n.Voice == VoiceKind.Melody);

		var drumsOnly = SongGenerator.Generate(2, 12, 8000, new SongStyle { Pad = false, Bass = false, Melody = false });
		drumsOnly.Notes.Where(n => n.Voice is VoiceKind.Kick or VoiceKind.Snare or VoiceKind.HiHat).Should().HaveCount(drumsOnly.Notes.Count);
		drumsOnly.Notes.Should().Contain(n => n.Voice == VoiceKind.Kick);

		var padOnly = SongGenerator.Generate(2, 12, 8000, new SongStyle { Bass = false, Melody = false, Drums = false });
		padOnly.Notes.Should().OnlyContain(n => n.Voice == VoiceKind.Pad);

		var bassOnly = SongGenerator.Generate(2, 12, 8000, new SongStyle { Pad = false, Melody = false, Drums = false });
		bassOnly.Notes.Should().OnlyContain(n => n.Voice == VoiceKind.Bass);
	}

	[Fact]
	public void AllVoicesPlay_InALongEnoughSong()
	{
		var song = SongGenerator.Generate(13, 30, 8000);
		song.Notes.Select(n => n.Voice).Distinct().Should().BeEquivalentTo(Enum.GetValues<VoiceKind>());
	}

	[Fact]
	public void NothingRepeatsExactly_TwoHalvesOfASongDiffer()
	{
		var song = SongGenerator.Generate(21, 40, 8000);
		var half = song.Samples.Length / 2;
		var first = song.Samples.AsSpan(0, half).ToArray();
		var second = song.Samples.AsSpan(half, half).ToArray();
		first.Should().NotEqual(second);
	}

	[Fact]
	public void Arguments_AreValidated()
	{
		var seconds = () => SongGenerator.Generate(1, 0, 11025);
		seconds.Should().Throw<ArgumentOutOfRangeException>();
		var longSong = () => SongGenerator.Generate(1, 4000, 11025);
		longSong.Should().Throw<ArgumentOutOfRangeException>();
		var lowRate = () => SongGenerator.Generate(1, 5, 4000);
		lowRate.Should().Throw<ArgumentOutOfRangeException>();
		var highRate = () => SongGenerator.Generate(1, 5, 192000);
		highRate.Should().Throw<ArgumentOutOfRangeException>();
		var noStyle = () => SongGenerator.Generate(1, 5, 11025, null!);
		noStyle.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void ShortSongs_WithDrumsNearTheEnd_DoNotOverrun()
	{
		for (var seed = 0; seed < 10; seed++)
		{
			var song = SongGenerator.Generate(seed, 1.3, 8000);
			song.Samples.Length.Should().Be(10400);
		}
	}

	[Fact]
	public void ToWav_ProducesAValidHeaderAndTheRightLength()
	{
		var song = SongGenerator.Generate(1, 2, 8000);
		var wav = song.ToWav();

		System.Text.Encoding.ASCII.GetString(wav, 0, 4).Should().Be("RIFF");
		System.Text.Encoding.ASCII.GetString(wav, 8, 4).Should().Be("WAVE");
		BitConverter.ToInt32(wav, 24).Should().Be(8000);
		BitConverter.ToInt32(wav, 40).Should().Be(song.Samples.Length * 2);
		wav.Length.Should().Be(44 + (song.Samples.Length * 2));
	}
}
