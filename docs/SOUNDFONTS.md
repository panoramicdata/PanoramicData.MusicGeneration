# Optional SoundFont rendering

Decision: retain the dependency-free procedural core and publish a separate
`PanoramicData.MusicGeneration.SoundFonts` adapter using MeltySynth 2.4.1 (MIT).
The adapter accepts caller-supplied SF2 bytes and renders an existing `GeneratedSong`.
It never downloads assets. Installing the core package adds neither MeltySynth nor a bank.

```csharp
using PanoramicData.MusicGeneration;
using PanoramicData.MusicGeneration.SoundFonts;

var score = SongGenerator.Generate(42, 60, 11025);
var renderer = new SoundFontRenderer(File.ReadAllBytes("my-bank.sf2"), "my-bank");
var result = await renderer.RenderAsync(score);
File.WriteAllBytes("sampled.wav", result.Song.ToWav());
```

The bank must contain the selected presets. Inspect `renderer.Presets` and supply
`SoundFontRenderOptions.Parts` to change them. Bank and program numbers are zero-based.
Defaults are strings (0:48), acoustic bass (0:32), piano (0:0), and the standard
percussion kit (128:0), with fixed per-role MIDI velocities. Drum note keys use the
score's General MIDI keys (36 kick, 38 snare, 42 closed hi-hat). Velocity is currently
an instrument setting; expressive event velocities/articulations remain issue #12/#16.
Melodic banks are 0–127 and percussion banks 128–255. Unsupported/missing presets
fail before rendering instead of silently changing instrument identity.

`RenderAsync` returns the original note list, section boundaries and song choices,
new mono PCM, six raw stems and one mix gain. Pitch-bend follows `Info.DetuneCents`
on every channel, including percussion. `FrequencyHz` remains score tuning metadata;
sampled drums and instruments have their own spectra and sample tuning. The existing
noise-drum frequency fields are not measurements of a sampled drum's fundamental.
PCM downmix averages the bank's left/right channels. Effects are disabled to keep
stems independent. Muting retains the original full-mix gain. Sample release tails
are allowed within, and clipped at, the requested duration. A fractional sample at
the chosen output rate is rounded to the nearest sample.

Notes are scheduled at rounded sample positions; MeltySynth's 64-sample processing
quantum bounds audible event timing precision. Melodic note overlaps use separate
MIDI channels so an earlier note-off cannot stop a later same-key note. Each melodic
role supports at most 15 simultaneous written notes, with up to 256 sounding sample
voices including layers and release tails. Exceeding written overlap fails explicitly;
MeltySynth may steal sample voices at its polyphony limit. Repeated percussion keys
share the kit channel and are released when their last overlapping event ends.

## Replay and platform boundary

`SoundFontRenderResult.Metadata` is serialisable and records the adapter contract
version, MeltySynth assembly version, exact bank SHA-256, caller-provided bank name,
sample rate, bank/program/velocity choices, block size, polyphony and effects policy.
Keep the score and metadata together. The score already carries seed, tuning and
section identity; reproducing its composition also requires the original style and
`SongGenerator.Version`. The core's golden fixtures and algorithm version are unchanged.

Fresh synthesizers reproduce PCM with the same bank/settings on the same runtime.
The adapter uses MeltySynth floating-point filters/interpolation, so bit-identical PCM
across CPU/runtime/browser versions is not promised. Written note identity and timing
remain inspectable and independent of the selected bank. Native .NET and browser
WebAssembly are supported without a native audio library or a server. The adapter
renders offline PCM; real-time streaming, MIDI-file export, SF3 compression, stereo
output and event-level articulation are outside this first version.

MeltySynth is chosen over native FluidSynth because it targets .NET Standard 2.1
without native dependencies, allowing the same adapter in the Blazor demo. FluidSynth
remains an alternative for native/streaming applications. Richer procedural synthesis
retains its smaller asset/memory footprint and arithmetic ownership; sampled rendering
provides recognisable instruments with a separate asset and reproducibility boundary.

## Demonstration asset and redistribution

The demo self-hosts an **unmodified GeneralUser GS 2.0.3** bank by **S. Christian Collins**:

- Source: <https://github.com/mrbumpy409/GeneralUser-GS>
- Pinned upstream commit: `684543d5e5efaef08d02be50dcda8d552478fa60`.
- File: `GeneralUser-GS.sf2`, 32,319,396 bytes (30.8 MiB).
- SHA-256: `9575028C7A1F589F5770FCCC8CFF2734566AF40CD26ED836944E9A5152688CFE`.
- Author's site: <https://www.schristiancollins.com/generaluser>.
- Terms: upstream `documentation/LICENSE.txt`, GeneralUser GS License v2.0,
  copied verbatim beside the demo asset as `GeneralUser-GS-LICENSE.txt`.

The author's custom licence permits software-project use and modification/packaging;
it is **not represented as the MIT licence**. The supplied text also records the
author's uncertainty about some historic sample origins. That text and attribution
are retained in full. This bank is chosen for an explicitly labelled demonstration;
no exclusive ownership or independent clearance of the recordings is claimed.
The adapter package contains no bank and makes no licensing choice for callers.
The author's request to self-host rather than hotlink download files is respected.
MeltySynth's MIT notice is also available in the demo's `soundfonts` directory.

FluidR3 GM (MIT) is an alternative, but its approximately 141 MiB SF2 is larger than
this first adapter's 64 MiB input cap. TimGM6mb is smaller but has different GPL asset
terms. GeneralUser GS was selected for the initial preview's compact General MIDI
palette, with its precise custom terms above.

## Demo and resource budgets

Open `/song?form=sections&sound=soundfont` or choose **SoundFont instruments**. It
loads the self-hosted bank once per page. Subsequent sound switches reuse the exact
written score; instrument changes re-render it. Mute/solo remix the selected renderer's
stems. Both the piano roll and spectrogram describe the selected audible mix. The
procedural option remains the default. The range starts at C3–C5 in either rendering.
The adapter does not move pitches to suit an instrument's playable range.

The demo renders SoundFonts at 22,050 Hz and procedural audio at 11,025 Hz. Progress
reports identify the current stem and overall completion. Callback yields let the
browser paint during long renders. The adapter accepts cancellation between blocks.
Browser users may load an SF2 locally; no upload takes place. The six-part demo needs
both a melodic preset and a drum kit, although API consumers can render a silent role
with a suitable assignment. Missing banks, invalid SF2 data and absent presets surface
as errors while the previous player remains available.

Raw SoundFont stems alone cost `6 × 8 × rate × seconds` bytes. At 300 seconds and
22,050 Hz that is about 303 MiB; output PCM and temporary mixing buffers add about
76 MiB at peak. The demo also retains the procedural comparison and loaded bank, so
long previews require several hundred MiB and substantially more CPU than the current
oscillators. The 64 MiB bank cap and 300-second UI limit are explicit. Use shorter
previews on memory-constrained devices. No bank is added to NuGet packages.

## Primary references

- MeltySynth API examples and MIT licence: <https://github.com/sinshu/meltysynth>.
- GeneralUser GS README and licence: <https://github.com/mrbumpy409/GeneralUser-GS>.
- FluidSynth architecture: <https://www.fluidsynth.org/api/Introduction.html>.

SoundFonts contain audio instrument samples/programs. They are unrelated to music
notation glyph fonts such as SMuFL.

A desktop Chromium verification rendered a 300-second structured preview in 31 seconds, with 48 distinct progress updates, and completed its full-length spectrogram. This is a measured development-machine result, not a performance guarantee for other devices.
