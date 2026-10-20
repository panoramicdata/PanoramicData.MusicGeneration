# PanoramicData.MusicGeneration

[![NuGet](https://img.shields.io/nuget/v/PanoramicData.MusicGeneration.svg)](https://www.nuget.org/packages/PanoramicData.MusicGeneration)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/)
[![CI](https://github.com/panoramicdata/PanoramicData.MusicGeneration/actions/workflows/ci.yml/badge.svg)](https://github.com/panoramicdata/PanoramicData.MusicGeneration/actions/workflows/ci.yml)

Deterministic, seeded music generation for .NET. A **seed** becomes a whole song: a key and scale, a tempo, a new chord every bar, a bass line, a tune with optional recurring sections, and a drum pattern, rendered to PCM. It also hands back **the list of notes it played**, so you can score a pitch tracker, an onset detector or a fingerprinting library against the truth.

- **Reproducible for ever.** The same seed, length, sample rate and style give the same song on any machine and any .NET version: the library carries its own random number generator (PCG32) and its own bit-exact sine.
- **Varied.** Tempo, key, scale, harmony, melody, drum pattern, timbre, tuning and lead-in all come from the seed. The default free-running mode avoids exact loops; opt into a form for returning verses and choruses.
- **Ground truth included.** Every note and drum hit comes back with its start, length, MIDI note and rendered frequency.
- **Dependency-free procedural core.** The original renderer synthesises sine waves and noise. Optional SoundFont rendering uses caller-supplied samples with their own asset licences.
- **Arrangement studio.** Choose known chord progressions, independent genre/rhythm templates and up to 24 instrument layers; save written scores locally and download WAV or MP3 in the demo.
- **Educational.** The demo site explains the maths: equal temperament, harmonics, envelopes, noise and the random number generator.

> **Try it in your browser:** the demo runs the whole library locally (once published, at the address shown in the repository's About box). See [docs/DEMO.md](docs/DEMO.md).

## Installation

```shell
dotnet add package PanoramicData.MusicGeneration
```

Targets .NET 10. Sampled rendering is available in the separate `PanoramicData.MusicGeneration.SoundFonts` NuGet package. See the [SoundFont guide](docs/SOUNDFONTS.md) and [arrangement studio](docs/STUDIO.md).

## Quick start

```csharp
using PanoramicData.MusicGeneration;

// 30 seconds of music at 22,050 Hz from seed 42.
GeneratedSong song = SongGenerator.Generate(seed: 42, seconds: 30, sampleRate: 22050);

float[] samples = song.Samples;               // mono, loudest sample exactly 0.9
byte[] wav = song.ToWav();                    // 16-bit mono WAV, ready to save or play

Console.WriteLine($"{song.Info.TempoBpm} bpm, {Pitch.NameOf(song.Info.RootMidi)} {song.Info.ScaleName}");
foreach (var note in song.Notes.Where(n => n.Voice == VoiceKind.Melody).Take(5))
{
	Console.WriteLine($"{note.StartSeconds:0.00}s  {Pitch.NameOf(note.MidiNote)}  {note.FrequencyHz:0.0} Hz  for {note.DurationSeconds:0.00}s");
}
```

Choose what may vary, or switch parts off, with a `SongStyle`:

```csharp
var style = new SongStyle
{
	MinTempoBpm = 100, MaxTempoBpm = 120,
	Scales = [Scales.MinorPentatonic, Scales.Dorian],
	MelodyLowMidi = 60, MelodyHighMidi = 84,   // middle C to C6
	Drums = false                              // no drums
};
var song = SongGenerator.Generate(7, 20, 44100, style);
```

Capture tracks once for a mute/solo UI:

```csharp
var song = SongGenerator.GenerateStems(42, 10, 11025);
double[] melody = song.Stems[VoiceKind.Melody]; // before normalisation
// Sum the desired stems, then apply song.NormalisationGain.
```

`SongStyle.Kick`, `Snare` and `HiHat` independently gate the drums. `PadOctaveOffset`, `PadLevel` and `PadAttackSeconds` control the quieter pad. `VibratoDepth`, `VibratoMinHz`, `VibratoMaxHz`, `VibratoDelaySeconds` and `VibratoRampSeconds` control the singer; depth zero disables vibrato. Algorithm version 2 changes the sound for existing seeds; see the [algorithm history](docs/DESIGN.md#algorithm-history).

## Recurring sections

Use an explicit form for an intro, A-B-A-B-C-B and an outro:

```csharp
var style = new SongStyle { Form = SongForms.VerseChorus };
var song = SongGenerator.Generate(42, 60, 11025, style);
foreach (var section in song.Sections)
{
	Console.WriteLine($"{section.Kind} {section.MaterialId}: {section.StartSeconds:0.00}-{section.EndSeconds:0.00}s");
}
```

Matching material identities reuse the written pattern. Each occurrence can change melody register, density and active voices, or add pickups and transition fills. Forms round-trip through System.Text.Json; each note identifies its source section. The example uses four phrases per verse and chorus, fitted in whole-bar phrases at the chosen tempo. Short requests explicitly crop the final audible section and can omit later sections. Algorithm version 3 changes this structured preset while preserving the default free-running audio hash. See [section composition](docs/DESIGN.md#section-composition) for duration and ending rules.

## What a song contains

| Part | How it is made |
|---|---|
| **Pad** | One triad per bar on a random scale degree with a random inversion; 2 soft harmonics, slow attack and decay, folded below the melody. |
| **Bass** | A note on most beats: the chord root, sometimes its fifth; low, 3 harmonics, quick decay. |
| **Melody** | A random walk through the scale, clamped to the melody range, with half, one or one-and-a-half beat notes, 15% rests, 6 harmonics and a gentle vibrato. |
| **Kick** | A sine whose frequency falls as <code>f<sub>0</sub> + 110 e<sup>-28t</sup></code>. |
| **Snare, hi-hat** | White noise through a first-difference filter times a fast decay. |

Full description, formulas and parameters: [docs/DESIGN.md](docs/DESIGN.md).

## Determinism

Reproducibility is the point, so it is tested hard: a golden hash of a generated song, golden values for the random number generator (they match the published PCG32 reference outputs), and a check that changing the sample rate does not change the notes. `SongGenerator.Version` names the algorithm version; any change that alters the output for an existing seed is a breaking change and increments it.

If arithmetic speed matters more than bit-exactness (for example in an interpreted WebAssembly runtime, where the library's own sine is slow), set `SongStyle.UsePlatformSine`. The notes and choices are unchanged; only the last bit of each sample may then differ between operating systems.

## Documentation

| Document | For |
|---|---|
| [docs/DESIGN.md](docs/DESIGN.md) | Goals, algorithm, parameters, determinism contract, API |
| [docs/DEMO.md](docs/DEMO.md) | The browser demo and how to host it |
| [docs/STUDIO.md](docs/STUDIO.md) | SoundFont keyboard, layered composition, browser library and MP3 export |
| [docs/DEVELOPMENT_PROMPT.md](docs/DEVELOPMENT_PROMPT.md) | The brief for developing the library further |

## Development

```shell
dotnet build                                          # warnings are errors
dotnet test --project PanoramicData.MusicGeneration.Test
dotnet run --project PanoramicData.MusicGeneration.Demo
```

The library has unit tests for synthesis, voice independence and raw stem reconstruction. Contributions: see [CONTRIBUTING.md](CONTRIBUTING.md) and [SECURITY.md](SECURITY.md).

## Links

- NuGet: https://www.nuget.org/packages/PanoramicData.MusicGeneration
- Source: https://github.com/panoramicdata/PanoramicData.MusicGeneration
- Issues: https://github.com/panoramicdata/PanoramicData.MusicGeneration/issues

## License

MIT, see [LICENSE](LICENSE).

## Optional SoundFont instruments

The separate `PanoramicData.MusicGeneration.SoundFonts` package renders existing scores using caller-supplied SF2 banks. The demo offers a GeneralUser GS comparison. See [usage and asset terms](docs/SOUNDFONTS.md).
