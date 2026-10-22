using System.Text.Json;
using PanoramicData.MusicGeneration.Demo.Pages;

namespace PanoramicData.MusicGeneration.Test;

public class FlowTests
{
	private static ArrangementSettings Settings => ArrangementPresets.ForGenre(MusicGenre.Pop) with
	{
		Seconds = 90,
		HarmonicMovement = 1,
		ChromaticColour = 1,
		PhraseExpression = 1,
		AccompanimentVariation = 1
	};

	[Fact]
	public void Harmony_IsContinuousWithHeldAndHalfBarChordsAndDirectedColour()
	{
		var score = ArrangementComposer.Compose(Settings);
		var bar = 240.0 / Settings.Tempo;
		score.Chords.Should().Contain(c => c.EndSeconds - c.StartSeconds > bar + 0.01);
		score.Chords.Should().Contain(c => c.EndSeconds - c.StartSeconds < bar - 0.01);
		for (var i = 1; i < score.Chords.Count; i++)
		{
			score.Chords[i].StartSeconds.Should().BeApproximately(score.Chords[i - 1].EndSeconds, 1e-8);
			if (score.Chords[i - 1].ResolvesTo is { } target) { score.Chords[i].Symbol.Should().Be(target); }
		}
		score.Chords.Should().Contain(c => c.Function == "Borrowed minor subdominant");
		score.Chords.Should().Contain(c => c.Function == "Dominant approach");
		score.Chords.Should().OnlyContain(c => c.EndSeconds > c.StartSeconds);
	}

	[Fact]
	public void PhraseExpression_CrossesHarmonyAndLeavesActualSpaceForAnswers()
	{
		var settings = Settings with { Layers = [new() { Id = "lead", Pattern = LayerPattern.Melody }, new() { Id = "answer", Pattern = LayerPattern.Countermelody }] };
		var score = ArrangementComposer.Compose(settings);
		var lead = score.Tracks[0].Notes;
		var answers = score.Tracks[1].Notes;
		lead.Should().Contain(n => score.Chords.Any(c => c.StartSeconds > n.StartSeconds + 0.01 && c.StartSeconds < n.StartSeconds + n.DurationSeconds - 0.01));
		answers.Should().NotBeEmpty();
		answers.Should().OnlyContain(a => !lead.Any(n => n.StartSeconds < a.StartSeconds + a.DurationSeconds && n.StartSeconds + n.DurationSeconds > a.StartSeconds));
		foreach (var track in score.Tracks)
		{
			track.Notes.Should().OnlyContain(n => n.DurationSeconds > 0 && n.StartSeconds >= 0 && n.StartSeconds + n.DurationSeconds <= score.Ending!.EndSeconds + 1e-8);
		}
	}

	[Fact]
	public void HarmonicTiming_DoesNotRestartTheDrumGrid()
	{
		var plain = ArrangementComposer.Compose(Settings with { HarmonicMovement = 0, ChromaticColour = 0 });
		var flowing = ArrangementComposer.Compose(Settings);
		foreach (var track in plain.Tracks.Where(t => t.Instrument.Pattern is LayerPattern.Kick or LayerPattern.Snare or LayerPattern.HiHat))
		{
			flowing.Tracks.Single(t => t.Instrument.Id == track.Instrument.Id).Notes.Should().Equal(track.Notes);
		}
	}

	[Fact]
	public void FlowSettings_RoundTripAndMissingFieldsDefaultToZeroForOldDocuments()
	{
		var document = new StudioDocument { Settings = Settings, Score = ArrangementComposer.Compose(Settings) };
		var json = JsonSerializer.Serialize(document, StudioJsonContext.Default.StudioDocument);
		var restored = JsonSerializer.Deserialize(StudioDocumentMigration.ExpandDefaults(json), StudioJsonContext.Default.StudioDocument)!;
		restored.Settings.Should().BeEquivalentTo(Settings);
		restored.Score!.Chords.Should().BeEquivalentTo(document.Score!.Chords);
		var old = JsonSerializer.Deserialize(StudioDocumentMigration.ExpandDefaults("{\"Settings\":{}}"), StudioJsonContext.Default.StudioDocument)!;
		old.Settings.HarmonicMovement.Should().Be(0);
		old.Settings.ChromaticColour.Should().Be(0);
		old.Settings.PhraseExpression.Should().Be(0);
		old.Settings.AccompanimentVariation.Should().Be(0);
	}

	[Theory]
	[InlineData(double.NaN)]
	[InlineData(-0.01)]
	[InlineData(1.01)]
	public void InvalidFlowControls_AreRejected(double value)
	{
		foreach (var settings in new[] { Settings with { HarmonicMovement = value }, Settings with { ChromaticColour = value }, Settings with { PhraseExpression = value }, Settings with { AccompanimentVariation = value } })
		{
			Assert.Throws<ArgumentException>(() => ArrangementComposer.Compose(settings));
		}
	}
}
