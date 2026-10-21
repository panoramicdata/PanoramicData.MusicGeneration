using System.Text.Json;
using PanoramicData.MusicGeneration;
using PanoramicData.MusicGeneration.SoundFonts;

var output = args.Length == 0 ? "temp/score-research" : args[0];
Directory.CreateDirectory(output);
foreach (var genre in Enum.GetValues<MusicGenre>())
{
	var settings = ArrangementPresets.ForGenre(genre) with { Seconds = 210 };
	var score = ArrangementComposer.Compose(settings);
	File.WriteAllText(Path.Combine(output, $"generated-{genre}.json"), JsonSerializer.Serialize(score));
}
var bankPath = "PanoramicData.MusicGeneration.Demo/wwwroot/soundfonts/GeneralUser-GS.sf2";
var bank = new SoundFontRenderer(File.ReadAllBytes(bankPath), "GeneralUser GS 2.0.3");
var profiles = new[] { ("flute", OrchestralTimbres.Flute, 73), ("violin", OrchestralTimbres.Violin, 40), ("trumpet", OrchestralTimbres.Trumpet, 56) };
foreach (var (name, profile, program) in profiles)
{
	foreach (var midi in new[] { 72, 74 })
	{
		var note = new OrchestralNote { MidiNote = midi, SampleRate = 44100, Seconds = 2, Velocity = 90 };
		var samples = OrchestralSynthesizer.Render(profile, note);
		File.WriteAllBytes(Path.Combine(output, $"{name}-{midi}-generated.wav"), WavEncoder.Encode(samples, note.SampleRate));
		var geometric = Array.AsReadOnly(Enumerable.Range(0, 6).Select(h => Math.Pow(0.6, h)).ToArray());
		var baseline = profile with { AttackSpectrum = geometric, SustainSpectrum = geometric, NoiseLevel = 0, VibratoCents = 0, PitchDriftCents = 0, VelocityBrightness = 0 };
		File.WriteAllBytes(Path.Combine(output, $"{name}-{midi}-baseline.wav"), WavEncoder.Encode(OrchestralSynthesizer.Render(baseline, note), note.SampleRate));
		var written = new GeneratedSong(new(42, 120, "Major", 36, 0, 0), new float[3 * note.SampleRate], note.SampleRate,
			[new(VoiceKind.Melody, 0, 2, midi, Pitch.FrequencyOf(midi)) { Velocity = 90 }]);
		var options = new SoundFontRenderOptions { SampleRate = note.SampleRate };
		options = options with { Parts = [.. options.Parts.Select(p => p.Voice == VoiceKind.Melody ? p with { Program = program, Velocity = 100 } : p)] };
		var rendered = await bank.RenderAsync(written, options).ConfigureAwait(false);
		File.WriteAllBytes(Path.Combine(output, $"{name}-{midi}-soundfont.wav"), rendered.Song.ToWav());
		File.WriteAllText(Path.Combine(output, "bank.json"), JsonSerializer.Serialize(rendered.Metadata));
	}
}
Console.WriteLine($"Exported composer {ArrangementComposer.Version} arrangements and dry C5/D5 comparisons to {output}.");
