using System.Text.Json;

namespace PanoramicData.MusicGeneration.Test;

public class ArrangementTests
{
	[Fact]
	public void FourChords_AreExplicitAndTransposeWithTheKey()
	{
		var settings = ArrangementPresets.ForGenre(MusicGenre.Pop);
		ChordProgressions.Resolve(settings).Select(c => c.Symbol).Should().Equal("C", "G", "Am", "F");
		ChordProgressions.Resolve(settings with { Key = 2 }).Select(c => c.Symbol).Should().Equal("D", "A", "Bm", "G");
		ChordProgressions.Resolve(settings with { Progression = "Ragtime circle" }).Select(c => c.Symbol).Should().Equal("C", "A7", "D7", "G7");
	}

	[Theory]
	[InlineData("F#m7", 6, "0,3,7,10")]
	[InlineData("Bbmaj7", 10, "0,4,7,11")]
	[InlineData("Bdim", 11, "0,3,6")]
	[InlineData("Dsus4", 2, "0,5,7")]
	public void ExplicitChordSymbols_ResolveKnownIntervals(string symbol, int root, string intervals)
	{
		var chord = ChordProgressions.Parse(symbol);
		chord.Root.Should().Be(root);
		string.Join(',', chord.Intervals).Should().Be(intervals);
	}

	[Theory]
	[InlineData(MusicGenre.Pop)]
	[InlineData(MusicGenre.Rock)]
	[InlineData(MusicGenre.Reggae)]
	[InlineData(MusicGenre.BossaNova)]
	[InlineData(MusicGenre.Ragtime)]
	[InlineData(MusicGenre.Classical)]
	[InlineData(MusicGenre.Orchestral)]
	[InlineData(MusicGenre.Jazz)]
	public void Genres_CreateRepeatableBoundedLayeredScores(MusicGenre genre)
	{
		var settings = ArrangementPresets.ForGenre(genre) with { Seconds = 12 };
		var first = ArrangementComposer.Compose(settings);
		var second = ArrangementComposer.Compose(settings);
		first.Tracks.Count.Should().Be(settings.Layers.Count);
		foreach (var track in first.Tracks)
		{
			track.Notes.Should().Equal(second.Tracks.Single(t => t.Instrument.Id == track.Instrument.Id).Notes);
			track.Notes.Should().OnlyContain(n => n.StartSeconds >= 0 && n.StartSeconds + n.DurationSeconds <= settings.Seconds + 1e-9);
			track.Notes.Should().OnlyContain(n => n.LayerId == track.Instrument.Id && n.Velocity >= 1 && n.Velocity <= 127);
		}
	}

	[Fact]
	public void StrongMelodyNotesAndChordVoicings_FollowTheWrittenHarmony()
	{
		var settings = ArrangementPresets.ForGenre(MusicGenre.Pop) with { Seconds = 20 };
		var score = ArrangementComposer.Compose(settings);
		var notes = score.Tracks.Where(t => t.Instrument.Pattern is LayerPattern.Melody or LayerPattern.Chords).SelectMany(t => t.Notes);
		foreach (var note in notes.Where(n => Math.Abs(n.StartSeconds * settings.Tempo / 60 - Math.Round(n.StartSeconds * settings.Tempo / 60)) < 1e-8))
		{
			var chord = score.Chords.Last(c => c.StartSeconds <= note.StartSeconds + 1e-9);
			chord.Intervals.Should().Contain((note.MidiNote - chord.Root + 120) % 12);
		}
		var pad = score.Tracks.Single(t => t.Instrument.Pattern == LayerPattern.Chords);
		var voicings = pad.Notes.GroupBy(n => n.StartSeconds).Take(2).Select(g => g.Select(n => n.MidiNote).Order().ToArray()).ToArray();
		voicings[0].Should().Equal(48, 52, 55);
		voicings[1].Zip(voicings[0], (a, b) => Math.Abs(a - b)).Sum().Should().BeLessThan(12);
	}

	[Theory]
	[InlineData(60)]
	[InlineData(120)]
	[InlineData(300)]
	public void CompleteVersesAndChoruses_AlwaysHaveFourTwoBarPhrases(int seconds)
	{
		var score = ArrangementComposer.Compose(ArrangementPresets.ForGenre(MusicGenre.Pop) with { Seconds = seconds, Tempo = 87 });
		score.Sections.Where(s => s.Kind is SectionKind.Verse or SectionKind.Chorus)
			.Should().OnlyContain(s => s.Bars == 8 && s.PhraseCount == 4 && s.PhraseBars == 2);
		score.Sections[^1].EndSeconds.Should().Be(seconds);
		score.Chords[^1].EndSeconds.Should().Be(seconds);
	}

	[Fact]
	public void ReggaeAndBossa_AreDifferentRhythmicEventsRatherThanLabels()
	{
		var settings = ArrangementPresets.ForGenre(MusicGenre.Pop) with { Tempo = 120, Seconds = 12, Sections = false };
		var reggae = ArrangementComposer.Compose(settings with { Beat = MusicBeat.Reggae });
		var rock = ArrangementComposer.Compose(settings with { Beat = MusicBeat.Rock });
		var bossa = ArrangementComposer.Compose(settings with { Beat = MusicBeat.BossaNova });
		reggae.Tracks.Single(t => t.Instrument.Pattern == LayerPattern.Kick).Notes.Should().OnlyContain(n => Math.Abs(n.StartSeconds % 2 - 1) < 1e-9);
		reggae.Tracks[0].Notes.Select(n => n.StartSeconds).Should().NotEqual(rock.Tracks[0].Notes.Select(n => n.StartSeconds));
		bossa.Tracks.Single(t => t.Instrument.Pattern == LayerPattern.Snare).Notes.Should().OnlyContain(n => n.MidiNote == 37);
		bossa.Tracks[0].Notes.Should().Contain(n => Math.Abs(n.StartSeconds - 0.375) < 1e-9);
	}

	[Fact]
	public void AddingAndChangingInstruments_DoesNotPerturbOtherWrittenParts()
	{
		var settings = ArrangementPresets.ForGenre(MusicGenre.Classical) with { Seconds = 12 };
		var first = ArrangementComposer.Compose(settings);
		var extra = settings.Layers[0] with { Id = "extra-violin", Program = 73 };
		var changed = ArrangementComposer.Compose(settings with { Layers = [.. settings.Layers, extra] });
		foreach (var track in first.Tracks)
		{
			changed.Tracks.Single(t => t.Instrument.Id == track.Instrument.Id).Notes.Should().Equal(track.Notes);
		}
		ArrangementPresets.ForGenre(MusicGenre.Orchestral).Layers.Count.Should().BeGreaterThan(6);
		ArrangementPresets.ForGenre(MusicGenre.Ragtime).Layers.Count.Should().BeGreaterThan(6);
	}

	[Fact]
	public void ScoreRoundTrip_PreservesWrittenNotesAndInstrumentIdentity()
	{
		var score = ArrangementComposer.Compose(ArrangementPresets.ForGenre(MusicGenre.Orchestral) with { Seconds = 6 });
		var restored = JsonSerializer.Deserialize<ArrangedScore>(JsonSerializer.Serialize(score))!;
		restored.Version.Should().Be(1);
		restored.Tracks.Select(t => t.Instrument).Should().Equal(score.Tracks.Select(t => t.Instrument));
		foreach (var track in restored.Tracks) { track.Notes.Should().Equal(score.Tracks.Single(t => t.Instrument.Id == track.Instrument.Id).Notes); }
	}

	[Fact]
	public void Seed_ChangesTheOpeningMotifWithoutChangingTheSelectedHarmony()
	{
		var settings = ArrangementPresets.ForGenre(MusicGenre.Pop) with { Seconds = 12 };
		var first = ArrangementComposer.Compose(settings);
		var different = Enumerable.Range(43, 8).Select(seed => ArrangementComposer.Compose(settings with { Seed = seed }));
		var melody = first.Tracks.Single(t => t.Instrument.Pattern == LayerPattern.Melody).Notes.Select(n => n.MidiNote).ToArray();
		different.Should().Contain(s => !s.Tracks.Single(t => t.Instrument.Pattern == LayerPattern.Melody).Notes.Select(n => n.MidiNote).SequenceEqual(melody));
		foreach (var score in different) { score.Chords.Select(c => c.Symbol).Should().Equal(first.Chords.Select(c => c.Symbol)); }
	}

	[Fact]
	public void InvalidSettingsAndUnknownChords_AreRejected()
	{
		var settings = ArrangementPresets.ForGenre(MusicGenre.Pop);
		Assert.Throws<ArgumentException>(() => ChordProgressions.Parse("H#magic"));
		Assert.Throws<ArgumentException>(() => ArrangementComposer.Compose(settings with { Swing = double.NaN }));
		Assert.Throws<ArgumentException>(() => ArrangementComposer.Compose(settings with { Layers = [settings.Layers[0], settings.Layers[0]] }));
		Assert.Throws<ArgumentNullException>(() => ArrangementComposer.Compose(settings with { Layers = [null!] }));
	}
}
