using System.Text.Json;
using System.Text.Json.Nodes;
using PanoramicData.MusicGeneration.Demo.Pages;

namespace PanoramicData.MusicGeneration.Test;

public class PianoWritingTests
{
	private static ArrangementSettings Settings => ArrangementPresets.ForGenre(MusicGenre.Pop) with { Seconds = 60 };

	[Fact]
	public void PianoLead_HasTwoRegistersAndTrebleIntervalsWhileRetainingTheTune()
	{
		var plain = ArrangementComposer.Compose(Settings with { Layers = [new() { Id = "piano", Pattern = LayerPattern.Melody, PianoTwoHands = false }] });
		var played = ArrangementComposer.Compose(plain.Settings with { Layers = [plain.Settings.Layers[0] with { PianoTwoHands = true }] });
		var melody = plain.Tracks[0].Notes;
		var piano = played.Tracks[0].Notes;
		foreach (var note in melody) { piano.Should().Contain(note); }
		piano.Should().Contain(n => n.MidiNote < 48);
		piano.GroupBy(n => n.StartSeconds).Should().Contain(g => g.Count(n => n.MidiNote >= 55) >= 2);
		piano.Where(n => !melody.Contains(n)).Should().OnlyContain(n => n.Velocity < melody.Max(m => m.Velocity));
		played.Sections[^1].Kind.Should().Be(SectionKind.Outro);
		piano.Where(n => n.SectionIndex == played.Sections[^1].Index).Should().Contain(n => n.MidiNote < 55);
	}

	[Fact]
	public void Ragtime_AddsTrebleChordsWithoutDuplicatingItsExistingLeftHand()
	{
		var settings = ArrangementPresets.ForGenre(MusicGenre.Ragtime) with { Seconds = 60 };
		var plain = ArrangementComposer.Compose(settings with { Layers = [.. settings.Layers.Select(l => l with { PianoTwoHands = false })] });
		var played = ArrangementComposer.Compose(settings);
		var source = plain.Tracks.Single(t => t.Instrument.Id == "lead").Notes;
		var piano = played.Tracks.Single(t => t.Instrument.Id == "lead").Notes;
		var added = piano.Where(n => !source.Contains(n)).ToArray();
		added.Should().NotBeEmpty();
		added.Should().OnlyContain(n => source.Any(s => s.StartSeconds == n.StartSeconds && s.MidiNote - n.MidiNote >= 3 && s.MidiNote - n.MidiNote <= 12));
		piano.GroupBy(n => n.StartSeconds).Should().Contain(g => g.Count() >= 3);
		foreach (var id in new[] { "bass", "stride", "clarinet", "banjo", "tuba" })
		{
			played.Tracks.Single(t => t.Instrument.Id == id).Notes.Should().Equal(plain.Tracks.Single(t => t.Instrument.Id == id).Notes);
		}
	}

	[Theory]
	[InlineData(0, -2)]
	[InlineData(0, 0)]
	[InlineData(11, 2)]
	public void PianoWriting_IsBoundedRepeatableAndRespectsSilentSections(int key, int octave)
	{
		var layer = new InstrumentLayer { Id = "piano", Pattern = LayerPattern.Arpeggio, Octave = octave, Presence = new() { Verse = 0 } };
		var settings = Settings with { Key = key, Layers = [layer] };
		var first = ArrangementComposer.Compose(settings);
		var second = ArrangementComposer.Compose(settings);
		first.Tracks[0].Notes.Should().Equal(second.Tracks[0].Notes);
		first.Tracks[0].Notes.Should().OnlyContain(n => n.MidiNote >= 21 && n.MidiNote <= 108 && n.DurationSeconds > 0);
		foreach (var section in first.Sections)
		{
			var notes = first.Tracks[0].Notes.Where(n => n.SectionIndex == section.Index);
			if (section.Kind == SectionKind.Verse) { notes.Should().BeEmpty(); }
			notes.Should().OnlyContain(n => n.StartSeconds >= section.StartSeconds && n.StartSeconds + n.DurationSeconds <= section.EndSeconds + 1e-8);
		}
	}

	[Fact]
	public void PianoSwitch_RoundTripsAndOldDocumentsRetainSingleLineSetting()
	{
		var document = new StudioDocument { Settings = Settings, Score = ArrangementComposer.Compose(Settings) };
		var json = JsonSerializer.Serialize(document, StudioJsonContext.Default.StudioDocument);
		var restored = JsonSerializer.Deserialize(StudioDocumentMigration.ExpandDefaults(json), StudioJsonContext.Default.StudioDocument)!;
		restored.Settings.Layers[0].PianoTwoHands.Should().BeTrue();
		var editor = new LayerEditor(restored.Settings.Layers[0]);
		editor.ToLayer().PianoTwoHands.Should().BeTrue();
		var old = JsonNode.Parse(json)!;
		foreach (var layer in old[nameof(StudioDocument.Settings)]!["Layers"]!.AsArray()) { layer!.AsObject().Remove("PianoTwoHands"); }
		var migrated = JsonSerializer.Deserialize(StudioDocumentMigration.ExpandDefaults(old.ToJsonString()), StudioJsonContext.Default.StudioDocument)!;
		migrated.Settings.Layers.Should().OnlyContain(l => !l.PianoTwoHands);
		foreach (var track in document.Score!.Tracks) { restored.Score!.Tracks.Single(t => t.Instrument.Id == track.Instrument.Id).Notes.Should().Equal(track.Notes); }
	}
}
