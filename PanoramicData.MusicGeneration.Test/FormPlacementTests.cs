namespace PanoramicData.MusicGeneration.Test;

public class FormPlacementTests
{
	private static readonly int[] Lengths = [120, 210, 300];

	[Theory]
	[InlineData(60, 40)]
	[InlineData(120, 87)]
	[InlineData(210, 160)]
	[InlineData(300, 220)]
	public void OpeningAndBreak_AreFourCompleteBars(int seconds, int tempo)
	{
		foreach (var seed in Enumerable.Range(0, 12))
		{
			var settings = ArrangementPresets.ForGenre(MusicGenre.Pop) with
			{
				Seed = seed,
				Seconds = seconds,
				Tempo = tempo,
				Endings = new() { Fade = 0, Cadence = 1, Tag = 0, Stop = 0 }
			};
			var score = ArrangementComposer.Compose(settings);
			score.Sections[0].Kind.Should().Be(SectionKind.Intro);
			score.Sections.Where(s => s.Kind is SectionKind.Intro or SectionKind.Bridge).Should().OnlyContain(s => s.Bars == 4);
			score.Sections.Count(s => s.Kind == SectionKind.Bridge).Should().BeLessThanOrEqualTo(1);
		}
	}

	[Fact]
	public void LongerSongs_PlaceTheBreakLaterAndReturnToACompleteChorus()
	{
		var settings = ArrangementPresets.ForGenre(MusicGenre.Pop) with
		{
			Endings = new() { Fade = 0, Cadence = 1, Tag = 0, Stop = 0 }
		};
		var scores = Lengths.Select(seconds => ArrangementComposer.Compose(settings with { Seconds = seconds })).ToArray();
		var breaks = scores.Select(s => s.Sections.Single(p => p.Kind == SectionKind.Bridge)).ToArray();
		breaks.Select(s => s.StartSeconds).Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
		foreach (var score in scores)
		{
			var departure = score.Sections.Single(s => s.Kind == SectionKind.Bridge);
			var bodyStart = score.Sections[0].EndSeconds;
			var bodyEnd = score.Sections[^1].StartSeconds;
			((departure.StartSeconds - bodyStart) / (bodyEnd - bodyStart)).Should().BeInRange(0.5, 0.8);
			score.Sections[departure.Index + 1].Kind.Should().Be(SectionKind.Chorus);
			score.Sections[departure.Index + 1].Bars.Should().Be(8);
		}
	}

	[Fact]
	public void TooShortForFourBarIntro_PreservesTheOutroAndOmitsTheIntro()
	{
		var settings = ArrangementPresets.ForGenre(MusicGenre.Pop) with { Seconds = 6, Tempo = 40 };
		var score = ArrangementComposer.Compose(settings);
		score.Sections.Should().NotContain(s => s.Kind == SectionKind.Intro);
		score.Sections[^1].Kind.Should().Be(SectionKind.Outro);
		score.PerformedSeconds.Should().BeLessThanOrEqualTo(6);
	}
}
