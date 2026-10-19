# PanoramicData.MusicGeneration design note

Status: algorithm version 2. Numbers marked (measured) come from the unit tests; everything else is design.

## 1. Purpose and constraints

Produce varied, repeatable, copyright-free music with its ground truth, for three uses: test and benchmark data for audio-analysis code (the first consumer is PanoramicData.SoundPrint), teaching material, and as a small, readable example of procedural audio.

Hard constraints:

- **Reproducible for ever.** Same seed, length, sample rate and style give identical notes always, and identical samples on every machine, until `SongGenerator.Version` increments.
- **No recordings or samples.** All sound is synthesised arithmetically.
- **No dependencies** beyond the .NET base class library. The library has no I/O of its own: it returns arrays and bytes.
- **Public API explicit about units**: seconds are `double`, frequencies are hertz, pitches are MIDI note numbers, detuning is cents.

## 2. Determinism contract

Three sources of difference between machines or versions are removed:

1. **Random numbers.** `Random` with a seed is not promised to give the same sequence in every .NET version. The library uses PCG32 (PCG-XSH-RR, 64-bit state, 32-bit output; O'Neill 2014) written out in `Pcg32`, with a stream number so that independent decisions (global choices, each bar, the melody, the noise) draw from independent sequences. Its first outputs for seed 42, stream 54 are asserted to equal the published reference values 0xA15C02B7, 0x7B47F409, 0xBA1D3330, 0x83D2F293.
2. **Sine.** `Math.Sin` calls the platform C runtime, which may differ in the last bit between operating systems and CPUs. `DeterministicMath.Sin` reduces the argument by Cody-Waite subtraction and evaluates a Taylor polynomial using only IEEE 754 additions and multiplications, which every machine computes identically, and the .NET JIT does not fuse into FMA. `SongStyle.UsePlatformSine` opts out for speed.
3. **Algorithm changes.** Any change to the output for an existing seed increments `SongGenerator.Version`, and a golden test of the samples' hash fails until the new hash is deliberately recorded.

The remaining platform-dependent calls (`Math.Exp`, `Math.Pow` for frequencies and envelopes) are used with arguments for which the platforms agree to the precision that matters; the golden hash is the safeguard, and if it ever differs on a platform that is a defect to fix by replacing the call with a deterministic one.

## 3. What a seed chooses

All from PCG32 stream 0, in this order:

| Choice | Range |
|---|---|
| Tempo | `MinTempoBpm` to `MaxTempoBpm` inclusive (default 78 to 147) |
| Scale | one of `SongStyle.Scales` (default: major, natural minor, major pentatonic, minor pentatonic, dorian) |
| Root | MIDI `RootMidiLow` to `RootMidiHigh` (default 36 to 47: C2 to B2) |
| Brightness &rho; | 0.35 to 0.85 (harmonic roll-off of the melody; the pad uses &rho;/4) |
| Vibrato rate | `VibratoMinHz` to `VibratoMaxHz` (default 4.5 to 6.5 Hz) |
| Detune | -45 to +45 cents (default `MaxDetuneCents`), applied to the whole song: A4 = 440 &middot; 2<sup>cents/1200</sup> |
| Kick base frequency | 42 to 66 Hz |
| Lead-in | 0 to one bar of silence, which moves the song off the beat grid of any other song |

## 4. Structure and parts

The song is a sequence of bars of four beats (`beat = 60 / tempo`). The bar loop uses a stream per bar (`1000 + bar`), so changing the number of parts does not change what the other parts play. The melody uses stream 1 and the drum noise stream 2.

| Part | Algorithm |
|---|---|
| Pad | Per bar: a random scale degree `d`, a random inversion 0 to 2; notes at degrees `d`, `d+2`, `d+4` above root + `12 * PadOctaveOffset` semitones (default one octave), the lowest `inversion` of them lifted one octave (`scale.Count` degrees). Each pitch is folded down by octaves until below `MelodyLowMidi`. Amplitude `PadLevel` (default 0.04), 2 harmonics, roll-off brightness / 4, attack `PadAttackSeconds` (default 0.12 s), decay 0.3. |
| Bass | Per beat, probability 0.65 of a note at root-relative degree `d` (0.7) or `d + fifth` (0.3), where the fifth is 4 degrees for 7-note scales and 3 for pentatonic scales (both 7 semitones). Length 1.6 beats, amplitude 0.22, 3 harmonics, &rho; 0.5, decay 6. |
| Melody | Random walk over scale degrees starting mid-range; step uniform in [-3, 3], clamped to the degrees whose MIDI note lies in `[MelodyLowMidi, MelodyHighMidi]` (the degree-0 note is the lowest note of the key at or above `MelodyLowMidi`). Length half, one or one-and-a-half beats; probability 0.85 of sounding. Amplitude 0.20, 6 harmonics, vibrato depth `VibratoDepth` (default 0.12%), decay 5. |
| Kick | Per beat, probability 0.8 on beats 1 and 3, 0.25 on beats 2 and 4. 0.18 s sine with frequency `f0 + 110 e^(-28t)` and amplitude `0.5 e^(-14t)`. |
| Snare | Beats 2 and 4, probability 0.85. 0.12 s noise burst, amplitude 0.20. |
| Hi-hat | A 0.04 s burst on each beat (amplitude 0.07) and, with probability 0.7, on the off-beat (0.05). |

**Tone synthesis.** A note of length *L*, frequency *f* and weight &rho;: `x(t) = A * env(t) * &sum;_{h=1..H} &rho;^(h-1) sin(2&pi; h f' t)` where instantaneous `f' = f (1 + d(t) sin(2&pi; v t))` when vibrato is on. `d(t) = VibratoDepth * clamp((t - VibratoDelaySeconds) / VibratoRampSeconds, 0, 1)`; defaults are a 0.15 s delay and 0.2 s ramp. Notes shorter than 1/3 s have no vibrato. Phase is the sample-wise integral of frequency, harmonics above Nyquist minus 200 Hz are skipped, and `env(t) = min(1, 120 t) * e^(-d t / L) * min(1, max(0, 40 (L - t)))` (attack 1/120 s, exponential decay, release 1/40 s). The base phase uses absolute time, with a continuous accumulated modulation offset.

**Noise.** `y_n = w_n - w_(n-1)` (first difference of white noise, so low frequencies are cut) times `e^(-6 n / N)`.

**Mixing.** Parts are summed into a double buffer and the whole non-silent song is scaled so that the loudest sample is exactly 0.9. Silent songs remain silent.

`GenerateStems` additionally captures a double buffer per `VoiceKind`. These are **before normalisation**: sum them, then multiply by `GeneratedSong.NormalisationGain` to reproduce `Samples` within float rounding. Capturing stems does not change the mix. Muting and soloing should retain the full mix gain to preserve relative track levels; a subset can peak higher through loss of cancellation, and WAV encoding clips values outside [-1, 1]. Stems allocate six extra double buffers and are opt-in. Ordinary `Generate` leaves `Stems` empty.

`Kick`, `Snare` and `HiHat` switches default true and are gated by `Drums`. Muted noise voices still advance the shared noise stream, so other drum waveforms remain identical. The switches and stem capture introduce no additional change to the default version 2 output.

## Section composition

`SongStyle.Form` is null by default, preserving the version-2 output and golden hash. A `SongForm` is a serialisable list of `SongSection` occurrences with kind, case-sensitive material identity and template length in four-beat bars. `SongForms.VerseChorus` supplies an intro, A-B-A-B-C-B and an outro.

The composer builds a note/timbre template per material identity using stable character hashing and independent seeded streams, then performs each occurrence. This stage does not synthesise temporary audio. Repeated identities must have the same template length. Register shifts, deterministic melody thinning and voice selection vary an occurrence without regenerating its source material. Different absolute oscillator phases mean repeated written patterns need not have bit-identical audio. Full melodic motif transformations and harmonic progressions remain separate roadmap features.

Duration policies:

- `Truncate` keeps the specified bar counts. The requested duration crops excess material; a completed shorter form leaves silence afterwards. Notes are clipped to section boundaries.
- `FitWholeBars` keeps the seed's tempo and lead-in. It gives every section one bar when possible, allocates remaining whole bars by the template-length weights, and breaks fractional-allocation ties by section order. Material cycles when an occurrence exceeds its template length. A request with fewer bars than sections keeps a prefix, one bar per occurrence. With less than one bar remaining it crops the first section; requests ending before the lead-in contain silence. A rounding tolerance below one nanosecond handles arithmetic at an exact bar boundary. The remaining fractional-bar tail is silent.

Pickups start up to one beat before the section body, bounded at zero, and carry the upcoming section's index. Transition fills replace the last beat's drums with four snare hits. A completed final section can replace its final bar with a tonic chord, bass, melody and kick, followed by a short release gap. A cropped section does not claim a final cadence. Section voice choices intersect the global style switches, so muting still preserves other stems.

`GeneratedSong.Sections` reports source indices, roles, material identities, performed and planned boundaries, assigned bars, pickup starts, fills and cadences. `NoteEvent.SectionIndex` links notes to their occurrence, including pickups; it is null in free-running mode. The form and notes are sample-rate independent. The structured golden fixture uses seed 42, 30 s, 8,000 Hz, a fixed 120 bpm and SongForms.VerseChorus; its FNV sample hash is `699470266271215891`. The UI renders these boundaries on the player and piano roll and offers a through-composed comparison with the same opening, section lengths, key and tempo.

## 5. API

```csharp
GeneratedSong SongGenerator.Generate(int seed, double seconds, int sampleRate);
GeneratedSong SongGenerator.Generate(int seed, double seconds, int sampleRate, SongStyle style);
GeneratedSong SongGenerator.GenerateStems(int seed, double seconds, int sampleRate);
GeneratedSong SongGenerator.GenerateStems(int seed, double seconds, int sampleRate, SongStyle style);
const int SongGenerator.Version;

record GeneratedSong(SongInfo Info, float[] Samples, int SampleRate, IReadOnlyList<NoteEvent> Notes) { TimeSpan Duration; byte[] ToWav(); }
record SongInfo(int Seed, int TempoBpm, string ScaleName, int RootMidi, double DetuneCents, double LeadInSeconds) { double ReferenceHz; }
readonly record struct NoteEvent(VoiceKind Voice, double StartSeconds, double DurationSeconds, int MidiNote, double FrequencyHz);
record SongStyle { tempo range, Scales, root range, melody range, MaxDetuneCents, Pad/Bass/Melody/Drums switches, UsePlatformSine }
static class Scales { Major, NaturalMinor, MajorPentatonic, MinorPentatonic, Dorian, All }
static class Pitch { FrequencyOf(midi), FrequencyOf(midi, referenceHz), NameOf(midi) }
static class WavEncoder { byte[] Encode(ReadOnlySpan<float>, int sampleRate) }
```

Limits: length up to one hour; sample rate 8,000 to 96,000 Hz. Notes are returned in start order; `FrequencyHz` is 0 for noise drums.

There are no optional parameters on public methods (overloads instead), which keeps the surface friendly to static analysis and to languages without optional-argument support.

## 6. Performance

Rendering is additive synthesis in plain loops: cost is proportional to (samples &times; sounding harmonics). Native code renders a 30 s song at 22,050 Hz in well under a second. The bit-exact sine is a 9-term polynomial, which is slow in an interpreted WebAssembly runtime; `UsePlatformSine` exists for that case (the demo renders 10 s of audio in about 0.3 s with it).

## 7. Testing

xUnit v3 with AwesomeAssertions, skips treated as failures, regression coverage of the library. The tests cover: determinism, a golden hash of a rendered song, the PCG32 reference values, independence of notes from sample rate, the style ranges holding for many seeds, melody notes belonging to the song's scale, every voice playing in a long enough song, nothing repeating exactly, a Goertzel measurement that a melody note puts its energy at its stated frequency, voices switching off cleanly, argument validation, WAV structure, and `UsePlatformSine` agreeing with the exact sine to within 1e-4.

## 8. Non-goals (for now)

- Not a general synthesiser, sequencer or MIDI library; no MIDI file I/O, no stereo, no effects.
- Not an attempt at musically ambitious composition. The output is pleasant and varied, not memorable.
- No real-instrument sample playback.

## 9. Intellectual property

Everything is generated from first principles (equal temperament, additive synthesis, simple envelopes, a published pseudo-random generator, PCG32 by Melissa O'Neill, whose reference implementation is Apache-2.0 / MIT licensed and whose algorithm is written out here from the paper). No samples, recordings or third-party music are used, so generated audio carries no third-party copyright. No patent search was made; the techniques used are textbook and decades old. This is a research note, not legal advice.

## Algorithm history

- Version 1: original pad at root + 24, amplitude 0.10; immediate 0.4% melody vibrato.
- Version 2: lower, quieter pad with slower attack; configurable delayed, ramped 0.12% melody vibrato using integrated phase and no vibrato on short notes. Seed 42, 8 s, 11,025 Hz: FNV sample hash `8783139537262375303`, tempo 87 bpm. Track switches and optional raw stems preserve this default output.

## 10. Open decisions for further development

See [DEVELOPMENT_PROMPT.md](DEVELOPMENT_PROMPT.md): musical structure (sections that repeat with variation, as real songs do), more timbres, swing and time signatures, MIDI export, and the educational site's remaining lessons.
