namespace PanoramicData.MusicGeneration.Test;

public class MelodicDiversityTests
{
	private static ArrangementSettings Settings => ArrangementPresets.ForGenre(MusicGenre.Pop) with
	{
		IntroVariation = 0,
		ProgressionVariation = 0,
		Variation = 0,
		Layers = [new() { Id = "lead", Pattern = LayerPattern.Melody }],
		Endings = new() { Fade = 0, Cadence = 1, Tag = 0, Stop = 0 }
	};

	[Fact]
	public void SelectedProgression_ChangesTheVerseEvenAtFullSectionContrast()
	{
		var signatures = ChordProgressions.Names.Where(n => n != "Custom").Select(name =>
		{
			var score = ArrangementComposer.Compose(Settings with { Progression = name });
			var verse = score.Sections.First(s => s.Kind == SectionKind.Verse);
			return string.Join(',', score.Chords.Where(c => c.StartSeconds >= verse.StartSeconds && c.StartSeconds < verse.EndSeconds).Select(c => c.Symbol));
		});
		signatures.Distinct().Count().Should().BeGreaterThanOrEqualTo(8);
	}

	[Fact]
	public void CustomProgression_IsTheSourceOfVerseHarmony()
	{
		var score = ArrangementComposer.Compose(Settings with { Progression = "Custom", CustomChords = "Ebmaj7, Abmaj7, Fm7, Bb7" });
		var verse = score.Sections.First(s => s.Kind == SectionKind.Verse);
		var symbols = score.Chords.Where(c => c.StartSeconds >= verse.StartSeconds && c.StartSeconds < verse.EndSeconds).Select(c => c.Symbol);
		symbols.Should().OnlyContain(s => new[] { "Ebmaj7", "Abmaj7", "Fm7", "Bb7" }.Contains(s));
	}

	[Fact]
	public void Seeds_OfferDifferentRhythmicAndMelodicIdeasWithIdenticalHarmony()
	{
		var scores = Enumerable.Range(0, 64).Select(seed => ArrangementComposer.Compose(Settings with { Seed = seed })).ToArray();
		var rhythms = scores.Select(s => string.Join(',', Opening(s).Select(n => $"{n.StartSeconds:R}/{n.DurationSeconds:R}")));
		var pitches = scores.Select(s => string.Join(',', Opening(s).Select(n => n.MidiNote)));
		rhythms.Distinct().Count().Should().BeGreaterThanOrEqualTo(24);
		pitches.Distinct().Count().Should().BeGreaterThanOrEqualTo(48);
		foreach (var score in scores) { score.Chords.Select(c => (c.Symbol, c.StartSeconds, c.EndSeconds)).Should().Equal(scores[0].Chords.Select(c => (c.Symbol, c.StartSeconds, c.EndSeconds))); }
	}

	private static IEnumerable<NoteEvent> Opening(ArrangedScore score)
	{
		var verse = score.Sections.First(s => s.Kind == SectionKind.Verse);
		return score.Tracks[0].Notes.Where(n => n.StartSeconds >= verse.StartSeconds && n.StartSeconds < verse.StartSeconds + 8 * 60.0 / score.Settings.Tempo);
	}

	[Fact]
	public void IndependentMelodySeed_PreservesHarmonyBackingAndParticipation()
	{
		var settings = ArrangementPresets.ForGenre(MusicGenre.Pop) with { MelodySeed = 42, Seconds = 60 };
		var first = ArrangementComposer.Compose(settings);
		var second = ArrangementComposer.Compose(settings with { MelodySeed = 43 });
		first.Chords.Select(c => (c.Symbol, c.StartSeconds)).Should().Equal(second.Chords.Select(c => (c.Symbol, c.StartSeconds)));
		first.Participation.Should().Equal(second.Participation);
		foreach (var track in first.Tracks.Where(t => t.Instrument.Pattern is not (LayerPattern.Melody or LayerPattern.Countermelody)))
		{
			track.Notes.Should().Equal(second.Tracks.Single(t => t.Instrument.Id == track.Instrument.Id).Notes);
		}
		first.Tracks.Single(t => t.Instrument.Pattern == LayerPattern.Melody).Notes.Should().NotEqual(second.Tracks.Single(t => t.Instrument.Pattern == LayerPattern.Melody).Notes);
	}

	[Fact]
	public void PinnedIdea_SurvivesChangesToTheSongSeed()
	{
		var first = ArrangementComposer.Compose(Settings with { MelodySeed = 72, Seed = 1 });
		var second = ArrangementComposer.Compose(Settings with { MelodySeed = 72, Seed = 2 });
		foreach (var motif in first.Motifs) { motif.Notes.Should().Equal(second.Motifs.Single(m => m.Id == motif.Id).Notes); }
		first.Motifs.Single(m => m.Id == "Chorus").SourceId.Should().Be("Verse");
		first.Motifs.Single(m => m.Id == "Bridge").SourceId.Should().Be("Verse");
	}

	[Fact]
	public void Development_PreservesTheStatementAndChangesTheDeparture()
	{
		var repeated = ArrangementComposer.Compose(Settings);
		var developed = ArrangementComposer.Compose(Settings with { Variation = 1 });
		Opening(developed).Should().Equal(Opening(repeated));
		var verse = repeated.Sections.First(s => s.Kind == SectionKind.Verse);
		var middle = verse.StartSeconds + 16 * 60.0 / repeated.Settings.Tempo;
		var end = middle + 8 * 60.0 / repeated.Settings.Tempo;
		var plainNotes = repeated.Tracks[0].Notes.Where(n => n.StartSeconds >= middle && n.StartSeconds < end).ToArray();
		var changedNotes = developed.Tracks[0].Notes.Where(n => n.StartSeconds >= middle && n.StartSeconds < end).ToArray();
		changedNotes.Length.Should().BeLessThan(plainNotes.Length, "the departure fragments the original idea");
		changedNotes.Select(n => n.MidiNote).Should().NotEqual(plainNotes.Where((_, i) => i % 2 == 0).Select(n => n.MidiNote));
	}

	[Fact]
	public void ReturningChorus_RecallsItsFirstTwoBars()
	{
		var score = ArrangementComposer.Compose(Settings with { Variation = 1 });
		var choruses = score.Sections.Where(s => s.Kind == SectionKind.Chorus).Take(2).ToArray();
		choruses.Should().HaveCount(2);
		var first = PhraseNotes(score, choruses[0]);
		var returned = PhraseNotes(score, choruses[1]);
		returned.Length.Should().Be(first.Length);
		for (var i = 0; i < first.Length; i++)
		{
			returned[i].MidiNote.Should().Be(first[i].MidiNote);
			(returned[i].StartSeconds - choruses[1].StartSeconds).Should().BeApproximately(first[i].StartSeconds - choruses[0].StartSeconds, 1e-8);
			returned[i].DurationSeconds.Should().BeApproximately(first[i].DurationSeconds, 1e-8);
		}
	}

	[Theory]
	[InlineData(6)]
	[InlineData(11)]
	public void Transposition_PreservesOpeningPitchClassesInsteadOfFlatteningHighNotes(int key)
	{
		foreach (var seed in Enumerable.Range(0, 16))
		{
			var original = Opening(ArrangementComposer.Compose(Settings with { Seed = seed })).ToArray();
			var transposed = Opening(ArrangementComposer.Compose(Settings with { Seed = seed, Key = key })).ToArray();
			transposed.Length.Should().Be(original.Length);
			transposed.Should().OnlyContain(n => n.MidiNote >= 48 && n.MidiNote <= 72);
			for (var i = 0; i < original.Length; i++) { ((transposed[i].MidiNote - original[i].MidiNote + 120) % 12).Should().Be(key); }
		}
	}

	private static NoteEvent[] PhraseNotes(ArrangedScore score, SectionEvent section) =>
		[.. score.Tracks[0].Notes.Where(n => n.StartSeconds >= section.StartSeconds && n.StartSeconds < section.StartSeconds + 8 * 60.0 / score.Settings.Tempo)];

	[Theory]
	[InlineData("Bdim", 11, 6)]
	[InlineData("Caug", 0, 8)]
	public void Bass_RespectsTheSelectedChordFifth(string chord, int root, int fifth)
	{
		var score = ArrangementComposer.Compose(Settings with
		{
			Progression = "Custom",
			CustomChords = chord,
			SectionContrast = 0,
			Layers = [new() { Id = "bass", Pattern = LayerPattern.Bass }]
		});
		var notes = score.Tracks[0].Notes.Where(n => n.StartSeconds < score.Ending!.StartSeconds).ToArray();
		notes.Should().OnlyContain(n => (n.MidiNote - root + 120) % 12 == 0 || (n.MidiNote - root + 120) % 12 == fifth);
		notes.Should().Contain(n => (n.MidiNote - root + 120) % 12 == fifth);
	}
}
