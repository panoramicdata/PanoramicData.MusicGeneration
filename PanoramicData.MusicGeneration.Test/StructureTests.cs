using System.Text.Json;

namespace PanoramicData.MusicGeneration.Test;

public class StructureTests
{
	private static SongStyle Style(SongForm form) => new() { Form = form, MinTempoBpm = 120, MaxTempoBpm = 120 };

	private static SongForm RepeatedForm() => new()
	{
		EndWithCadence = false,
		Sections =
		[
			new() { Kind = SectionKind.Verse, MaterialId = "A" },
			new() { Kind = SectionKind.Chorus, MaterialId = "B" },
			new() { Kind = SectionKind.Verse, MaterialId = "A" },
			new() { Kind = SectionKind.Chorus, MaterialId = "B" },
			new() { Kind = SectionKind.Bridge, MaterialId = "C" },
			new() { Kind = SectionKind.Chorus, MaterialId = "B" }
		]
	};

	private static object[] RelativeNotes(GeneratedSong song, SectionEvent section) => [.. song.Notes.Where(n => n.SectionIndex == section.Index).Select(n => (object)(n.Voice, Math.Round(n.StartSeconds - section.StartSeconds, 8), Math.Round(n.DurationSeconds, 8), n.MidiNote, n.FrequencyHz))];

	[Fact]
	public void ReturningChoruses_ReuseTheirWrittenMaterial()
	{
		var song = SongGenerator.Generate(42, 30, 8000, Style(RepeatedForm()));
		song.Sections.Select(s => s.MaterialId).Should().Equal("A", "B", "A", "B", "C", "B");
		RelativeNotes(song, song.Sections[1]).Should().NotBeEmpty().And.Equal(RelativeNotes(song, song.Sections[3]));
		RelativeNotes(song, song.Sections[5]).Should().Equal(RelativeNotes(song, song.Sections[1]));
		RelativeNotes(song, song.Sections[0]).Should().NotEqual(RelativeNotes(song, song.Sections[1]));
		song.Notes.Should().OnlyContain(n => n.SectionIndex != null);
	}

	[Fact]
	public void Forms_RoundTripThroughJsonAndKeepTheirOutput()
	{
		var form = SongForms.VerseChorus;
		var restored = JsonSerializer.Deserialize<SongForm>(JsonSerializer.Serialize(form))!;
		var original = SongGenerator.Generate(7, 20, 8000, Style(form));
		var roundTrip = SongGenerator.Generate(7, 20, 8000, Style(restored));
		roundTrip.Samples.Should().Equal(original.Samples);
		roundTrip.Notes.Should().Equal(original.Notes);
		roundTrip.Sections.Should().Equal(original.Sections);
	}

	[Fact]
	public void WholeBarFitting_HasKnownBoundariesAndPreservesTempo()
	{
		var form = new SongForm
		{
			DurationMode = FormDurationMode.FitWholeBars,
			Sections = [new() { MaterialId = "A", Bars = 1 }, new() { MaterialId = "B", Bars = 2 }, new() { MaterialId = "C", Bars = 1 }]
		};
		var info = new SongInfo(0, 120, "Major", 36, 0, 0.5);
		var sections = SectionPlanner.Place(form, info, 10.5);
		sections.Select(s => s.Bars).Should().Equal(2, 2, 1);
		sections.Select(s => s.StartSeconds).Should().Equal(0.5, 4.5, 8.5);
		sections.Select(s => s.EndSeconds).Should().Equal(4.5, 8.5, 10.5);
		sections[^1].HasCadence.Should().BeTrue();
		sections.Should().OnlyContain(s => !s.IsTruncated);
	}

	[Fact]
	public void Truncation_ClipsEventsAndDoesNotInventAFinalCadence()
	{
		var form = RepeatedForm() with { EndWithCadence = true };
		var longSong = SongGenerator.Generate(42, 30, 8000, Style(form));
		var seconds = longSong.Info.LeadInSeconds + 0.03;
		var cropped = SongGenerator.Generate(42, seconds, 8000, Style(form));
		cropped.Sections.Should().HaveCount(1);
		cropped.Sections[0].IsTruncated.Should().BeTrue();
		cropped.Sections[0].HasCadence.Should().BeFalse();
		cropped.Notes.Should().OnlyContain(n => n.StartSeconds < seconds && n.StartSeconds + n.DurationSeconds <= seconds + 1e-10);
		cropped.Samples.Length.Should().Be((int)(seconds * 8000));
	}

	[Theory]
	[InlineData(0.001)]
	[InlineData(0.2)]
	[InlineData(2)]
	[InlineData(5)]
	public void ShortRequests_AreBoundedAndCanOmitLaterSections(double seconds)
	{
		var song = SongGenerator.Generate(42, seconds, 8000, Style(SongForms.VerseChorus));
		song.Samples.Length.Should().Be((int)(seconds * 8000));
		song.Sections.Should().OnlyContain(s => s.StartSeconds < seconds && s.EndSeconds <= seconds);
		song.Sections.Select(s => s.Index).Should().BeInAscendingOrder();
		song.Notes.Should().OnlyContain(n => n.StartSeconds >= 0 && n.StartSeconds < seconds);
	}

	[Fact]
	public void CompletedForm_EndsOnTheTonicAndThenSilence()
	{
		var form = new SongForm { Sections = [new() { MaterialId = "ending", Kind = SectionKind.Outro, Bars = 1 }] };
		var song = SongGenerator.Generate(2, 8, 8000, Style(form));
		var section = song.Sections[0];
		section.HasCadence.Should().BeTrue();
		var melody = song.Notes.Where(n => n.Voice == VoiceKind.Melody).ToArray();
		melody.Should().ContainSingle();
		(melody[0].MidiNote % 12).Should().Be(song.Info.RootMidi % 12);
		song.Samples.Skip((int)Math.Ceiling(section.EndSeconds * 8000)).Should().OnlyContain(s => s == 0);
	}

	[Fact]
	public void PickupsAndFills_AreOwnedByTheCorrectSection()
	{
		var form = new SongForm
		{
			EndWithCadence = false,
			Sections = [new() { MaterialId = "A", Bars = 1, TransitionFill = true }, new() { MaterialId = "B", Bars = 1, PickupBeats = 0.5 }]
		};
		var song = SongGenerator.Generate(42, 10, 8000, Style(form));
		var transition = song.Sections[0].EndSeconds;
		var fill = song.Notes.Where(n => n.SectionIndex == 0 && n.Voice == VoiceKind.Snare && n.StartSeconds >= transition - 0.5).ToArray();
		fill.Select(n => Math.Round(n.StartSeconds - transition, 6)).Should().Equal(-0.5, -0.375, -0.25, -0.125);
		song.Notes.Should().Contain(n => n.SectionIndex == 1 && n.Voice == VoiceKind.Melody && n.StartSeconds < transition);
		song.Sections[1].PickupStartSeconds.Should().BeApproximately(transition - 0.25, 1e-10);
	}

	[Fact]
	public void Variation_ChangesTheOccurrenceWithoutRerollingTheMaterial()
	{
		var form = new SongForm
		{
			EndWithCadence = false,
			Sections = [new() { MaterialId = "A", Bars = 2 }, new() { MaterialId = "A", Bars = 2, MelodyDensity = 0, Voices = [VoiceKind.Pad] }]
		};
		var song = SongGenerator.Generate(4, 12, 8000, Style(form));
		var first = song.Notes.Where(n => n.SectionIndex == 0 && n.Voice == VoiceKind.Pad).ToArray();
		var second = song.Notes.Where(n => n.SectionIndex == 1).ToArray();
		second.Should().OnlyContain(n => n.Voice == VoiceKind.Pad);
		second.Select(n => n.MidiNote).Should().Equal(first.Select(n => n.MidiNote));
		second.Select(n => n.DurationSeconds).Should().Equal(first.Select(n => n.DurationSeconds));
	}

	[Fact]
	public void StructuredNotes_DoNotDependOnSampleRateOrVoiceMuting()
	{
		var style = Style(SongForms.VerseChorus);
		var full = SongGenerator.GenerateStems(7, 18, 8000, style);
		var high = SongGenerator.Generate(7, 18, 11025, style);
		high.Notes.Should().Equal(full.Notes);
		high.Sections.Should().Equal(full.Sections);
		var muted = SongGenerator.GenerateStems(7, 18, 8000, style with { Snare = false });
		muted.Notes.Should().Equal(full.Notes.Where(n => n.Voice != VoiceKind.Snare));
		foreach (var voice in Enum.GetValues<VoiceKind>().Where(v => v != VoiceKind.Snare))
		{
			muted.Stems[voice].Should().Equal(full.Stems[voice]);
		}
	}

	[Fact]
	public void InvalidForms_AreRejected()
	{
		SongForm[] invalid =
		[
			new(), new() { Sections = null! }, new() { Sections = [null!] },
			new() { Sections = [new() { MaterialId = " " }] }, new() { Sections = [new() { Bars = 0 }] },
			new() { Sections = [new() { PhraseCount = 0 }] }, new() { Sections = [new() { PhraseCount = 17 }] },
			new() { Sections = [new() { Kind = (SectionKind)99 }] },
			new() { Sections = [new()], DurationMode = (FormDurationMode)99 },
			new() { Sections = [new() { MelodyDensity = double.NaN }] },
			new() { Sections = [new() { MelodyOctaveOffset = 3 }] },
			new() { Sections = [new() { PickupBeats = 2 }] },
			new() { Sections = [new() { Voices = [(VoiceKind)99] }] },
			new() { Sections = [new() { MaterialId = "A", Bars = 1 }, new() { MaterialId = "A", Bars = 2 }] },
			new() { Sections = [new() { MaterialId = "A", PhraseCount = 2 }, new() { MaterialId = "A", PhraseCount = 4 }] }
		];
		foreach (var form in invalid)
		{
			var generate = () => SongGenerator.Generate(1, 2, 8000, Style(form));
			generate.Should().Throw<ArgumentException>();
		}
	}

	[Fact]
	public void WholeBarFitting_HandlesAnExactFractionalTempoBoundary()
	{
		var form = SongForms.VerseChorus with { DurationMode = FormDurationMode.FitWholeBars };
		var info = new SongInfo(42, 87, "Major", 36, 0, 0.17);
		var seconds = info.LeadInSeconds + (14 * 4 * 60.0 / info.TempoBpm);
		var sections = SectionPlanner.Place(form, info, seconds);
		sections.Sum(s => s.Bars).Should().Be(14);
		sections[^1].EndSeconds.Should().Be(seconds);
		sections[^1].HasCadence.Should().BeTrue();
		sections.Should().OnlyContain(s => !s.IsTruncated);
	}

	[Fact]
	public void RegisterVariation_PreservesPitchClassesAndTheUnderlyingRhythm()
	{
		var form = new SongForm
		{
			EndWithCadence = false,
			Sections = [new() { MaterialId = "A" }, new() { MaterialId = "A", MelodyOctaveOffset = -1 }]
		};
		var song = SongGenerator.Generate(4, 12, 8000, Style(form) with { MelodyHighMidi = 108 });
		var first = song.Notes.Where(n => n.SectionIndex == 0 && n.Voice == VoiceKind.Melody).ToArray();
		var second = song.Notes.Where(n => n.SectionIndex == 1 && n.Voice == VoiceKind.Melody).ToArray();
		second.Select(n => n.MidiNote).Should().NotEqual(first.Select(n => n.MidiNote));
		second.Select(n => n.MidiNote % 12).Should().Equal(first.Select(n => n.MidiNote % 12));
		second.Select(n => Math.Round(n.StartSeconds - song.Sections[1].StartSeconds, 8)).Should()
			.Equal(first.Select(n => Math.Round(n.StartSeconds - song.Sections[0].StartSeconds, 8)));
		second.Should().OnlyContain(n => n.MidiNote >= 60 && n.MidiNote <= 108);
	}

	[Fact]
	public void StructuredStems_ReconstructTheOrdinaryMix()
	{
		var style = Style(SongForms.VerseChorus);
		var song = SongGenerator.GenerateStems(7, 18, 8000, style);
		var ordinary = SongGenerator.Generate(7, 18, 8000, style);
		song.Samples.Should().Equal(ordinary.Samples);
		song.Notes.Should().Equal(ordinary.Notes);
		for (var i = 0; i < song.Samples.Length; i++)
		{
			var sum = song.Stems.Values.Sum(stem => stem[i]);
			((float)(sum * song.NormalisationGain)).Should().BeApproximately(song.Samples[i], 1e-7f);
		}
	}

	[Fact]
	public void ASectionWithoutSnare_DoesNotClaimATransitionFill()
	{
		var form = SongForms.VerseChorus with { DurationMode = FormDurationMode.FitWholeBars };
		var sections = SectionPlanner.Place(form, new SongInfo(0, 120, "Major", 36, 0, 0), 30);
		sections.Single(s => s.Kind == SectionKind.Bridge).HasTransitionFill.Should().BeFalse();
	}

	[Theory]
	[InlineData(60)]
	[InlineData(120)]
	[InlineData(300)]
	public void PhraseFitting_KeepsFourPhrasesAndReturningMaterialLengths(double seconds)
	{
		var sections = SectionPlanner.Place(SongForms.VerseChorus, new SongInfo(42, 87, "Major", 36, 0, 0.17), seconds);
		var complete = sections.Where(s => !s.IsTruncated && s.Kind is SectionKind.Verse or SectionKind.Chorus).ToArray();
		complete.Should().NotBeEmpty();
		complete.Should().OnlyContain(s => s.PhraseCount == 4 && s.Bars == 4 * s.PhraseBars && s.PhraseBars >= 2);
		foreach (var group in sections.GroupBy(s => s.MaterialId))
		{
			group.Select(s => s.PhraseBars).Distinct().Should().ContainSingle();
		}
		sections.Should().OnlyContain(s => s.EndSeconds <= seconds);
	}

	[Fact]
	public void FourPhrases_RepeatTheSameBassIdeaFourTimes()
	{
		var form = new SongForm
		{
			EndWithCadence = false,
			Sections = [new() { Kind = SectionKind.Verse, MaterialId = "A", Bars = 2, PhraseCount = 4 }]
		};
		var song = SongGenerator.Generate(42, 18, 8000, Style(form));
		var start = song.Sections[0].StartSeconds;
		var phrase = song.Notes.Where(n => n.Voice == VoiceKind.Bass && n.StartSeconds < start + 4)
			.Select(n => (Math.Round(n.StartSeconds - start, 8), n.MidiNote)).ToArray();
		phrase.Should().NotBeEmpty();
		for (var repeat = 1; repeat < 4; repeat++)
		{
			var at = start + repeat * 4;
			song.Notes.Where(n => n.Voice == VoiceKind.Bass && n.StartSeconds >= at - 1e-9 && n.StartSeconds < at + 4 - 1e-9)
				.Select(n => (Math.Round(n.StartSeconds - at, 8), n.MidiNote)).Should().Equal(phrase);
		}
	}

	[Fact]
	public void PhraseFitting_ShortRequestsCropRatherThanClaimThreeCompletePhrases()
	{
		var sections = SectionPlanner.Place(SongForms.VerseChorus, new SongInfo(0, 120, "Major", 36, 0, 0), 30);
		sections.Should().HaveCount(3);
		sections[^1].Kind.Should().Be(SectionKind.Chorus);
		sections[^1].PhraseCount.Should().Be(4);
		sections[^1].PhraseBars.Should().Be(2);
		sections[^1].PlannedEndSeconds.Should().Be(34);
		sections[^1].EndSeconds.Should().Be(30);
		sections[^1].IsTruncated.Should().BeTrue();
	}

	[Fact]
	public void StructuredGoldenHash_PinsThePresetOutput()
	{
		var song = SongGenerator.Generate(42, 30, 8000, Style(SongForms.VerseChorus));
		var hash = 14695981039346656037UL;
		foreach (var sample in song.Samples)
		{
			hash = (hash ^ BitConverter.SingleToUInt32Bits(sample)) * 1099511628211UL;
		}
		hash.Should().Be(16170790293787036396UL);
	}

	[Fact]
	public void PhraseFitting_HasKnownLongFormAllocationAndDoesNotDependOnMaterialNames()
	{
		var form = SongForms.VerseChorus;
		var info = new SongInfo(42, 87, "Major", 36, 0, 0.17);
		var sections = SectionPlanner.Place(form, info, 300);
		sections.Select(s => s.Bars).Should().Equal(2, 20, 20, 20, 20, 4, 20, 2);
		var through = form with { Sections = [.. form.Sections.Select((s, i) => s with { MaterialId = $"material-{i}" })] };
		var comparison = SectionPlanner.Place(through, info, 300);
		comparison.Select(s => (s.StartSeconds, s.EndSeconds, s.PhraseBars)).Should()
			.Equal(sections.Select(s => (s.StartSeconds, s.EndSeconds, s.PhraseBars)));
	}

	[Theory]
	[InlineData(44)]
	[InlineData(45)]
	[InlineData(47)]
	[InlineData(60)]
	[InlineData(65)]
	[InlineData(108)]
	[InlineData(109)]
	public void PhraseFitting_UsesAvailableBarsWithoutBreakingPhraseCounts(int bars)
	{
		var sections = SectionPlanner.Place(SongForms.VerseChorus, new SongInfo(0, 120, "Major", 36, 0, 0.5), 0.5 + 2 * bars);
		sections.Sum(s => s.Bars).Should().Be(bars);
		sections.Should().OnlyContain(s => !s.IsTruncated);
		sections.Where(s => s.Kind is SectionKind.Verse or SectionKind.Chorus)
			.Should().OnlyContain(s => s.PhraseCount == 4 && s.Bars == 4 * s.PhraseBars);
	}

	[Fact]
	public void FittedPhrase_ComposesEveryAssignedBarRatherThanLeavingGaps()
	{
		var form = new SongForm
		{
			DurationMode = FormDurationMode.FitWholePhrases,
			EndWithCadence = false,
			Sections = [new() { MaterialId = "A", PhraseCount = 4, Bars = 1 }]
		};
		var song = SongGenerator.Generate(42, 60, 8000, Style(form) with { Bass = false, Melody = false, Drums = false });
		var section = song.Sections[0];
		section.PhraseBars.Should().BeGreaterThan(1);
		var firstPhraseEnd = section.StartSeconds + 2 * section.PhraseBars;
		var bars = song.Notes.Where(n => n.Voice == VoiceKind.Pad && n.StartSeconds < firstPhraseEnd - 1e-9)
			.Select(n => (int)Math.Floor((n.StartSeconds - section.StartSeconds) / 2 + 1e-9)).Distinct().Order().ToArray();
		bars.Should().Equal(Enumerable.Range(0, section.PhraseBars));
	}
}
