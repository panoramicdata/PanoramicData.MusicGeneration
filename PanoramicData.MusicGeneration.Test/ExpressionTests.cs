using System.Text.Json;

namespace PanoramicData.MusicGeneration.Test;

public class ExpressionTests
{
	private static ArrangementSettings Settings => ArrangementPresets.ForGenre(MusicGenre.Pop) with
	{
		Seconds = 210,
		Tempo = 160,
		Swing = 0.1,
		IntroVariation = 0,
		ProgressionVariation = 0,
		SectionContrast = 1,
		Endings = new() { Fade = 0, Cadence = 1, Tag = 0, Stop = 0 }
	};

	[Fact]
	public void HitSchemaVariants_ExposeDistinctStartingPoints()
	{
		ChordProgressions.Resolve(Settings with { Progression = "Anthem" }).Select(c => c.Symbol).Should().Equal("F", "G", "C", "Am");
		ChordProgressions.Resolve(Settings with { Progression = "Minor pop" }).Select(c => c.Symbol).Should().Equal("Am", "F", "C", "G");
	}

	[Fact]
	public void Defaults_AreUpbeatWithAThreeMinuteThirtyCeiling()
	{
		var settings = ArrangementPresets.ForGenre(MusicGenre.Pop);
		settings.Tempo.Should().Be(160);
		settings.Swing.Should().Be(0.1);
		settings.Seconds.Should().Be(210);
		ArrangementComposer.Version.Should().Be(2);
	}

	[Theory]
	[InlineData(6, 40)]
	[InlineData(6, 220)]
	[InlineData(61, 87)]
	[InlineData(210, 160)]
	[InlineData(300, 160)]
	public void Ceiling_AlwaysReservesAnOutroWithoutCroppingASongSection(int seconds, int tempo)
	{
		var score = ArrangementComposer.Compose(Settings with { Seconds = seconds, Tempo = tempo });
		score.Sections[^1].Kind.Should().Be(SectionKind.Outro);
		score.Sections.Should().OnlyContain(s => !s.IsTruncated);
		score.PerformedSeconds.Should().BeInRange(0.1, seconds);
		score.Ending!.EndSeconds.Should().Be(score.Sections[^1].EndSeconds);
		score.PerformedSeconds.Should().BeApproximately(score.Ending.EndSeconds + score.Ending.ReleaseSeconds, 1e-8);
		score.Tracks.SelectMany(t => t.Notes).Should().OnlyContain(n => n.StartSeconds + n.DurationSeconds <= score.Ending.EndSeconds + 1e-8);
		foreach (var section in score.Sections.Where(s => s.Kind is SectionKind.Verse or SectionKind.Chorus))
		{
			section.Bars.Should().Be(8);
			section.PhraseCount.Should().Be(4);
			section.PhraseBars.Should().Be(2);
		}
	}

	[Fact]
	public void SectionHarmony_HasDistinctFunctionsAndContrastZeroSharesTheCycle()
	{
		var score = ArrangementComposer.Compose(Settings);
		Symbols(score, SectionKind.Verse).Take(4).Should().Equal("Am", "F", "Dm", "G7");
		Symbols(score, SectionKind.Chorus).Take(4).Should().Equal("C", "G", "Am", "F");
		Symbols(score, SectionKind.Bridge).Take(4).Should().Equal("F", "Dm", "Am", "G7");
		Symbols(score, SectionKind.Verse).Last().Should().Be("G7");
		Symbols(score, SectionKind.Chorus).Last().Should().Be("C");
		var shared = ArrangementComposer.Compose(Settings with { SectionContrast = 0 });
		Symbols(shared, SectionKind.Verse).Should().Equal(Symbols(shared, SectionKind.Chorus));
	}

	[Fact]
	public void VariationFactors_AlterHarmonyAndOpeningWithoutChangingUnrelatedLayerDecisions()
	{
		var plain = ArrangementComposer.Compose(Settings);
		var varied = ArrangementComposer.Compose(Settings with { ProgressionVariation = 1 });
		varied.Chords.Select(c => c.Symbol).Should().NotEqual(plain.Chords.Select(c => c.Symbol));
		varied.Participation.Should().Equal(plain.Participation);
		var introLengths = Enumerable.Range(0, 20).Select(seed => ArrangementComposer.Compose(Settings with { Seed = seed, IntroVariation = 1 }).Sections[0].Bars).ToHashSet();
		introLengths.Should().Contain(4).And.Contain(8);
	}

	[Fact]
	public void Presence_IsAWholeSectionDecisionWithExplicitOccurrenceOverrides()
	{
		var settings = Settings;
		var bass = settings.Layers.Single(l => l.Pattern == LayerPattern.Bass);
		var lead = settings.Layers.Single(l => l.Pattern == LayerPattern.Melody) with
		{
			Presence = new() { Verse = 0, Overrides = [new(SectionKind.Verse, 3, 1), new(SectionKind.Chorus, 2, 0)] }
		};
		var score = ArrangementComposer.Compose(settings with { Layers = [bass, lead] });
		score.Participation.Where(p => p.LayerId == bass.Id).Should().OnlyContain(p => p.Present);
		var choices = score.Participation.Where(p => p.LayerId == lead.Id).ToArray();
		choices.Single(p => score.Sections[p.SectionIndex].Kind == SectionKind.Verse && p.Occurrence == 3).Present.Should().BeTrue();
		choices.Single(p => score.Sections[p.SectionIndex].Kind == SectionKind.Chorus && p.Occurrence == 2).Present.Should().BeFalse();
		var notes = score.Tracks.Single(t => t.Instrument.Id == lead.Id).Notes;
		foreach (var choice in choices.Where(p => !p.Present)) { notes.Should().NotContain(n => n.SectionIndex == choice.SectionIndex); }
		var extra = lead with { Id = "another-layer" };
		var enlarged = ArrangementComposer.Compose(score.Settings with { Layers = [bass, lead, extra] });
		enlarged.Participation.Where(p => p.LayerId != extra.Id).Should().Equal(score.Participation);
	}

	[Fact]
	public void StraightBeatAnswers_FitTheGapsInTheMainHook()
	{
		var settings = Settings;
		var lead = settings.Layers.Single(l => l.Pattern == LayerPattern.Melody) with { Presence = new() };
		var answer = lead with { Id = "answer", Pattern = LayerPattern.Countermelody };
		var score = ArrangementComposer.Compose(settings with { Layers = [lead, answer] });
		var melody = score.Tracks[0].Notes;
		var responses = score.Tracks[1].Notes.Where(n => score.Sections[n.SectionIndex!.Value].Kind is SectionKind.Verse or SectionKind.Chorus).ToArray();
		responses.Should().NotBeEmpty();
		foreach (var response in responses)
		{
			melody.Should().NotContain(n => n.StartSeconds < response.StartSeconds + response.DurationSeconds && n.StartSeconds + n.DurationSeconds > response.StartSeconds);
		}
	}

	[Fact]
	public void QuarterProbability_IsRepeatableAndProducesBothParticipationOutcomes()
	{
		var layer = Settings.Layers[0] with { Presence = new() { Verse = 0.25 } };
		var score = ArrangementComposer.Compose(Settings with { Layers = [layer] });
		var restored = JsonSerializer.Deserialize<ArrangedScore>(JsonSerializer.Serialize(score))!;
		restored.Participation.Should().Equal(score.Participation);
		var outcomes = Enumerable.Range(0, 100).SelectMany(seed => ArrangementComposer.Compose(Settings with { Seed = seed, Layers = [layer] }).Participation)
			.Where(p => Math.Abs(p.Probability - 0.25) < 1e-8).ToArray();
		outcomes.Count(p => p.Present).Should().BeInRange(outcomes.Length / 6, outcomes.Length / 3);
	}

	[Theory]
	[InlineData(EndingKind.Fade)]
	[InlineData(EndingKind.Cadence)]
	[InlineData(EndingKind.Tag)]
	[InlineData(EndingKind.Stop)]
	public void EndingWeights_SelectAnInspectableImplementedEnding(EndingKind kind)
	{
		var weights = new EndingWeights { Fade = 0, Cadence = 0, Tag = 0, Stop = 0 };
		weights = kind switch
		{
			EndingKind.Fade => weights with { Fade = 1 },
			EndingKind.Cadence => weights with { Cadence = 1 },
			EndingKind.Tag => weights with { Tag = 1 },
			_ => weights with { Stop = 1 }
		};
		var score = ArrangementComposer.Compose(Settings with { Endings = weights });
		score.Ending!.Kind.Should().Be(kind);
		if (kind != EndingKind.Fade) { score.Chords[^1].Symbol.Should().Be("C"); }
		if (kind == EndingKind.Cadence) { score.Chords[^2].Symbol.Should().Be("G7"); }
	}

	[Fact]
	public void ExpressionValidation_RejectsInvalidProbabilitiesAndAmbiguousOverrides()
	{
		Assert.Throws<ArgumentException>(() => ArrangementComposer.Compose(Settings with { ProgressionVariation = double.NaN }));
		Assert.Throws<ArgumentException>(() => ArrangementComposer.Compose(Settings with { Endings = new() { Fade = 0, Cadence = 0, Tag = 0, Stop = 0 } }));
		var layer = Settings.Layers[0] with { Presence = new() { Overrides = [new(SectionKind.Verse, 3, 0.25), new(SectionKind.Verse, 3, 1)] } };
		Assert.Throws<ArgumentException>(() => ArrangementComposer.Compose(Settings with { Layers = [layer] }));
	}

	private static IEnumerable<string> Symbols(ArrangedScore score, SectionKind kind)
	{
		var section = score.Sections.First(s => s.Kind == kind);
		return score.Chords.Where(c => c.StartSeconds >= section.StartSeconds && c.StartSeconds < section.EndSeconds).Select(c => c.Symbol);
	}
}
