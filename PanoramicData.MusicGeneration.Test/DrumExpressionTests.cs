using System.Text.Json;
using System.Text.Json.Nodes;
using PanoramicData.MusicGeneration.Demo.Pages;

namespace PanoramicData.MusicGeneration.Test;

public class DrumExpressionTests
{
	private static ArrangementSettings Settings => ArrangementPresets.ForGenre(MusicGenre.Pop) with
	{
		Seconds = 90,
		DrumVariation = 0,
		DrumFills = 0,
		DrumHumanity = 0,
		Layers = [.. ArrangementPresets.ForGenre(MusicGenre.Pop).Layers.Select(l => l with { Presence = new() })]
	};

	[Fact]
	public void Fills_OfferDifferentOrchestrationsAndTripletsAtLandmarks()
	{
		var scores = Enumerable.Range(0, 8).Select(seed => ArrangementComposer.Compose(Settings with { Seed = seed, DrumFills = 1 })).ToArray();
		var notes = scores.SelectMany(Kit).ToArray();
		notes.Should().Contain(n => n.MidiNote == 49);
		notes.Should().Contain(n => new[] { 50, 47, 45, 43 }.Contains(n.MidiNote));
		notes.Should().Contain(n => Math.Abs(n.StartSeconds * Settings.Tempo / 60 % 1 - 1.0 / 3) < 1e-7);
		foreach (var score in scores)
		{
			foreach (var note in Kit(score).Where(n => new[] { 50, 47, 45, 43 }.Contains(n.MidiNote)))
			{
				var section = score.Sections[note.SectionIndex!.Value];
				var position = (note.StartSeconds - section.StartSeconds) * Settings.Tempo / 60;
				var bar = (int)(position / 4 + 1e-8);
				((bar + 1) % 4 == 0).Should().BeTrue();
				section.Kind.Should().NotBe(SectionKind.Outro);
			}
		}
	}

	[Fact]
	public void Fills_LeaveRoomInTheHatPartAndRequireAnAudibleSnarePlayer()
	{
		var settings = Settings with { DrumFills = 1 };
		var played = ArrangementComposer.Compose(settings);
		var verse = played.Sections.First(s => s.Kind == SectionKind.Verse);
		var start = verse.EndSeconds - 2 * 60.0 / settings.Tempo;
		played.Tracks.Single(t => t.Instrument.Pattern == LayerPattern.HiHat).Notes.Should().NotContain(n => n.StartSeconds >= start && n.StartSeconds < verse.EndSeconds);
		var muted = ArrangementComposer.Compose(settings with { Layers = [.. settings.Layers.Select(l => l.Pattern == LayerPattern.Snare ? l with { Enabled = false } : l)] });
		muted.Tracks.Single(t => t.Instrument.Pattern == LayerPattern.HiHat).Notes.Count(n => n.StartSeconds >= start && n.StartSeconds < verse.EndSeconds).Should().Be(4);
	}

	[Fact]
	public void Variation_AddsQuietGhostsAndChangesTwoBarGestures()
	{
		var score = ArrangementComposer.Compose(Settings with { DrumVariation = 1 });
		var verse = score.Sections.First(s => s.Kind == SectionKind.Verse);
		var snare = score.Tracks.Single(t => t.Instrument.Pattern == LayerPattern.Snare).Notes.Where(n => n.SectionIndex == verse.Index).ToArray();
		var ghosts = snare.Where(n => n.StartSeconds < verse.EndSeconds - 30.0 / Settings.Tempo)
			.Where(n => Math.Abs(n.StartSeconds * Settings.Tempo / 60 % 1 - 0.75) < 1e-7).ToArray();
		ghosts.Should().NotBeEmpty();
		ghosts.Max(n => n.Velocity).Should().BeLessThan(snare.Where(n => Math.Abs(n.StartSeconds * Settings.Tempo / 60 % 1) < 1e-7).Min(n => n.Velocity) - 20);
		Kit(score).Count().Should().BeGreaterThan(Kit(ArrangementComposer.Compose(Settings)).Count());
	}

	[Fact]
	public void Humanity_ShapesTouchAndSnarePlacementWhileAnchoringTheKickPulse()
	{
		var plain = ArrangementComposer.Compose(Settings);
		var human = ArrangementComposer.Compose(Settings with { DrumHumanity = 1 });
		var verse = human.Sections.First(s => s.Kind == SectionKind.Verse);
		var kick = human.Tracks.Single(t => t.Instrument.Pattern == LayerPattern.Kick).Notes;
		kick.Select(n => n.StartSeconds).Should().Equal(plain.Tracks.Single(t => t.Instrument.Pattern == LayerPattern.Kick).Notes.Select(n => n.StartSeconds));
		var snare = human.Tracks.Single(t => t.Instrument.Pattern == LayerPattern.Snare).Notes.Where(n => n.SectionIndex == verse.Index).ToArray();
		(snare[0].StartSeconds - verse.StartSeconds - 60.0 / Settings.Tempo).Should().BeInRange(0.01, 0.016);
		snare.Select(n => n.Velocity).Distinct().Count().Should().BeGreaterThan(3);
	}

	[Fact]
	public void DrumControls_PreserveHarmonyMelodyAndOtherInstruments()
	{
		var plain = ArrangementComposer.Compose(Settings);
		var varied = ArrangementComposer.Compose(Settings with { DrumVariation = 1, DrumFills = 1, DrumHumanity = 1 });
		varied.Chords.Should().BeEquivalentTo(plain.Chords);
		varied.Participation.Should().Equal(plain.Participation);
		foreach (var track in plain.Tracks.Where(t => t.Instrument.Bank < 128))
		{
			varied.Tracks.Single(t => t.Instrument.Id == track.Instrument.Id).Notes.Should().Equal(track.Notes);
		}
	}

	[Theory]
	[InlineData(0.2, 3.5, 4)]
	[InlineData(0.5, 3, 5)]
	[InlineData(1, 2, 9)]
	public void FillIntensity_BoundsTheWindowAndStrokeCount(double intensity, double firstBeat, int maximum)
	{
		var scores = Enumerable.Range(0, 16).Select(seed => ArrangementComposer.Compose(Settings with { Seed = seed, DrumFills = intensity }));
		var pickups = new List<double>();
		foreach (var score in scores)
		{
			var snare = score.Tracks.Single(t => t.Instrument.Pattern == LayerPattern.Snare).Notes;
			foreach (var section in score.Sections)
			{
				var bars = snare.Where(n => n.SectionIndex == section.Index)
					.GroupBy(n => (int)Math.Floor((n.StartSeconds - section.StartSeconds) * Settings.Tempo / 240 + 1e-8));
				foreach (var bar in bars)
				{
					bar.Count().Should().BeLessThanOrEqualTo(maximum);
					var positions = bar.Select(n => (n.StartSeconds - section.StartSeconds) * Settings.Tempo / 60 - bar.Key * 4);
					pickups.AddRange(positions.Where(p => Math.Abs(p - Math.Round(p)) > 1e-7));
				}
			}
		}
		pickups.Should().NotBeEmpty();
		pickups.Should().OnlyContain(p => p >= firstBeat - 1e-7 && p < 4);
	}

	[Theory]
	[InlineData(40)]
	[InlineData(160)]
	[InlineData(220)]
	public void DrumWriting_IsRepeatableAndRespectsSectionAndDurationBounds(int tempo)
	{
		var settings = Settings with { Tempo = tempo, DrumVariation = 1, DrumFills = 1, DrumHumanity = 1 };
		var first = ArrangementComposer.Compose(settings);
		Kit(first).Should().Equal(Kit(ArrangementComposer.Compose(settings)));
		foreach (var note in Kit(first))
		{
			var section = first.Sections[note.SectionIndex!.Value];
			note.StartSeconds.Should().BeGreaterThanOrEqualTo(section.StartSeconds);
			(note.StartSeconds + note.DurationSeconds).Should().BeLessThanOrEqualTo(section.EndSeconds + 1e-8);
			note.DurationSeconds.Should().BeGreaterThan(0);
			note.Velocity.Should().BeInRange(1, 127);
		}
	}

	[Fact]
	public void DrumControls_RoundTripAndOldDocumentsDefaultThemToZero()
	{
		var settings = Settings with { DrumVariation = 0.8, DrumFills = 0.9, DrumHumanity = 0.7 };
		var json = JsonSerializer.Serialize(new StudioDocument { Settings = settings }, StudioJsonContext.Default.StudioDocument);
		var restored = JsonSerializer.Deserialize(StudioDocumentMigration.ExpandDefaults(json), StudioJsonContext.Default.StudioDocument)!;
		restored.Settings.Should().BeEquivalentTo(settings);
		var node = JsonNode.Parse(json)!;
		foreach (var name in new[] { "DrumVariation", "DrumFills", "DrumHumanity" }) { node[nameof(StudioDocument.Settings)]!.AsObject().Remove(name); }
		var old = JsonSerializer.Deserialize(StudioDocumentMigration.ExpandDefaults(node.ToJsonString()), StudioJsonContext.Default.StudioDocument)!;
		old.Settings.DrumVariation.Should().Be(0);
		old.Settings.DrumFills.Should().Be(0);
		old.Settings.DrumHumanity.Should().Be(0);
	}

	[Fact]
	public void FillSubdivisions_AreNotSwungAgainAtExtremeSwing()
	{
		var straight = ArrangementComposer.Compose(Settings with { DrumFills = 1, Swing = 0 });
		var swung = ArrangementComposer.Compose(Settings with { DrumFills = 1, Swing = 0.4 });
		var verse = straight.Sections.First(s => s.Kind == SectionKind.Verse);
		var start = verse.EndSeconds - 120.0 / Settings.Tempo;
		var first = straight.Tracks.Single(t => t.Instrument.Pattern == LayerPattern.Snare).Notes.Where(n => n.StartSeconds >= start && n.StartSeconds < verse.EndSeconds);
		var second = swung.Tracks.Single(t => t.Instrument.Pattern == LayerPattern.Snare).Notes.Where(n => n.StartSeconds >= start && n.StartSeconds < verse.EndSeconds);
		first.Should().NotBeEmpty();
		second.Should().Equal(first);
	}

	[Theory]
	[InlineData(double.NaN)]
	[InlineData(double.PositiveInfinity)]
	[InlineData(-0.1)]
	[InlineData(1.1)]
	public void InvalidDrumControls_AreRejected(double value)
	{
		foreach (var settings in new[] { Settings with { DrumVariation = value }, Settings with { DrumFills = value }, Settings with { DrumHumanity = value } })
		{
			Assert.Throws<ArgumentException>(() => ArrangementComposer.Compose(settings));
		}
	}

	private static IEnumerable<NoteEvent> Kit(ArrangedScore score) => score.Tracks.Where(t => t.Instrument.Bank >= 128).SelectMany(t => t.Notes);
}
