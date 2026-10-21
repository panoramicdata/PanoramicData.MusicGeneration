# Arrangement studio

The demo's `/soundfonts` lesson sits between pitch and frequency and `/song`, the arrangement studio. Enable sound, choose a sampled instrument and play two octaves with mouse, touch or keyboard. Clicking near the top of a key produces a softer strike and a slower added gain ramp; the bottom produces a stronger strike and faster ramp. MIDI velocity selects playing strength; the extra ramp demonstrates attack separately from the bank's recorded envelope. Space or Enter uses medium strength. The first load downloads the bundled GeneralUser GS bank; subsequent navigation reuses it.

## Writing a song

Genre selects a suggested ensemble, tempo, rhythm and harmony. Each can then be overridden independently. Pop, rock, reggae, bossa nova, jazz, ragtime, classical and orchestral templates are available. Ragtime and classical start with seven independent layers; orchestral starts with twelve. The editable layer panel supports one to 24 instruments, including duplicate presets, each with its own role, register, MIDI strength, level and enabled state.

Choose a named progression or enter comma-separated chord symbols. In C major, Four chords is Câ€“Gâ€“Amâ€“F (Iâ€“Vâ€“viâ€“IV); other choices include doo-wop, jazz turnaround, twelve-bar blues, Canon, minor descent and a ragtime circle. Custom chords support major, minor, dominant/major/minor seventh, half-diminished seventh, diminished, augmented and suspended qualities. Named patterns transpose with the key; custom symbols specify absolute roots. Minor descent deliberately remains a minor pattern, and the ragtime circle uses major/dominant chords.

The composer uses inversions that reduce chord movement, chord tones on strong melody beats, scale passing tones and rhythm-specific accompaniment. Composer 3 creates and stores two-bar verse, chorus and related bridge motifs from seeded rhythmic cells and pitch gestures, rather than selecting from four fixed contours. Phrase development (default 60%) can alter answers, fragment/invert the third phrase and close the fourth. The first phrase remains a recognisable statement on return. These remain generative templates, with richer harmonic rhythm, articulation and ensemble listening still in the backlog.

The selected progression now supplies all section plans. At full contrast, verses rotate its cycle by half its length, bridges reverse it and intros preview its first/last chords. Named patterns receive section-ending cadences; custom verses and bridges retain the selected chord symbols when progression variation is zero. Outro cadences use the selected key. Zero contrast and zero progression variation repeat the selected cycle unchanged in the body. The bass also respects diminished/augmented fifths instead of always imposing a perfect fifth. This fixes composer 2's unrelated fixed verse/bridge progressions, which made the progression selector ineffective in verses.

Use **New melody** to change the independent melody seed while retaining the song seed, harmony, percussion and other backing parts. Blank Melody seed follows the song seed. A fixed Melody seed retains the underlying relative-pitch/rhythm ideas as other settings change; realised pitches still adapt to the key and chords. It is not an editor for pinning exact notes or regenerating just one section. Settings imports and local saves retain the seed; saved written scores also retain the motif definitions and performed notes. The score details include the actual section chord sequences.

Length defaults to a 210-second ceiling at 160 bpm with 10% swing, and permits 6â€“300 seconds. Complete verses and choruses contain four two-bar phrases. The planner reserves an outro, adds complete sections and finishes early when necessary. Render changes explicitly after editing. Rendering yields to the browser and reports the current layer and percentage. The player owns the aligned section/chord lanes and note display; its visualizer selector also offers the built-in spectrogram.

## Library and portability

Save song locally stores the written score, settings and bank identity in IndexedDB for this browser and origin. It stores no audio. Opening a saved song replays its written notes rather than recomposing from its seed. Clearing browser site data removes the library. Export song JSON makes a portable backup; export settings JSON keeps the controls and instrument choices without the notes. Import accepts either file, up to 8 MiB. Unsupported file schemas and invalid score/settings are rejected with a visible error.

WAV and MP3 downloads contain the currently rendered mix. MP3 encoding runs in a Web Worker at 128 kbps mono, with progress feedback. No song data is sent to a server. Localhost and the published domain have separate libraries.

## Library API and reproducibility

```csharp
var settings = ArrangementPresets.ForGenre(MusicGenre.Orchestral) with
{
	Seed = 42,
	Seconds = 60,
	Progression = "Four chords"
};
var score = ArrangementComposer.Compose(settings);
var renderer = new SoundFontRenderer(File.ReadAllBytes("my-bank.sf2"), "my-bank");
var audio = await renderer.RenderScoreAsync(score);
File.WriteAllBytes("arrangement.wav", audio.Song.ToWav());
```

Composition is separate from synthesis. `ArrangementComposer.Version` is now 4 and governs new written-note generation independently of legacy `SongGenerator.Version` 3. `ArrangedScore.Version` remains the additive score file schema (1), while `ComposerVersion` records which algorithm wrote it. Version 2 introduced progression/intro factors, participation and ending plans; version 3 fixes progression ownership and adds independent melody seeds and optional stored `Motifs`. Earlier scores replay their written notes rather than recomposing; scores without ending metadata retain their previous duration/audio shaping. `NoteEvent.LayerId` identifies the instrument and `Velocity` supplies per-event strength. The legacy default is 100, preserving existing SoundFont part velocities and both procedural golden fixtures.

`RenderScoreAsync` renders each layer separately and mixes sequentially, retaining one aggregate buffer rather than every raw stem. Layer edits require rendering again. The final mix is peak-normalised, so a layer's gain is relative to the other audible layers. The existing six-role `RenderAsync` API still returns raw stems. The studio renders at 22,050 Hz; 300-second, 24-layer sessions can take substantially longer than short previews. Bank bytes, renderer/runtime identity and settings affect sampled PCM; cross-platform bit-identical sampled audio is not promised. See [SOUNDFONTS.md](SOUNDFONTS.md) for bank terms and rendering limits.

## MP3 redistribution notices

The demo redistributes unmodified **lamejs** by Alexander Shtadler and contributors, including the JavaScript port of LAME, from [zhuker/lamejs](https://github.com/zhuker/lamejs), commit `582bbba6a12f981b984d8fb9e1874499fed85675`. It is licensed under LGPL-3.0. It is loaded as a separate script by the MP3 worker, rather than embedded in either NuGet package.

`wwwroot/vendor/lamejs/` contains the unmodified `lame.all.js`, upstream LICENSE notice, GNU LGPL/GPL texts and the complete corresponding source archive `source.zip`. The page links both licence and source at the same published location. Keep these assets together when hosting or redistributing the demo. GeneralUser GS has separate asset terms; the core and adapter package licences remain MIT.

Research, musical examples, probability semantics and ending policies: [MUSICAL_EXPRESSION.md](MUSICAL_EXPRESSION.md).

Composer 4 fixes the intro and bridge/break at four bars and schedules the break near two-thirds of the available body, after complete verse/chorus pairs. It reserves an eight-bar chorus return and the outro. Very short songs omit sections that cannot fit completely. The SoundFonts lesson now also offers a separate experimental sample-free instrument lab; song rendering continues to use the selected sampled bank. See [measured MIDI and timbre comparisons](SCORE_AND_TIMBRE_STUDY.md) for results and limitations.
