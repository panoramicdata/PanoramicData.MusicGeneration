# Making the arrangements less static

Research and implementation notes, 20 October 2026. The mechanisms below are design choices informed by the cited sources; they are not a formula for producing a hit.

## Why ABBA is a useful reference

Benny Andersson describes writing melody together with chords before arranging bass and drums in his [Music Business Worldwide interview](https://www.musicbusinessworldwide.com/every-songwriter-needs-to-sit-there-keep-going-and-wait-for-it-to-happen/). Björn Ulvaeus describes developing short ideas on piano/guitar and checking whether a melody survives that stripped-down setting in his [published interview](https://link.springer.com/chapter/10.1007/978-3-031-66363-5_1). Their accounts support starting with a strong musical idea and refining it rather than relying on instrument count.

ABBA's official [Mamma Mia recording account](https://abbasite.com/articles/mamma-mia-the-song-that-saved-abba/) describes the importance of riffs, harmony parts and the distinctive marimba opening. Their [Dancing Queen account](https://abbasite.com/articles/dancing-queen/) describes deliberately developing the dance backing and then refining the production over months. Our inference: a primary hook needs supporting hooks, purposeful texture and a convincing groove; adding more simultaneous notes does not supply those automatically. These sources describe particular creative processes, not an experimentally established explanation of catchiness.

The arranger was missing much of that coordination: one chord loop under every section, a very small recurring contour, almost continuous parts and endings imposed by elapsed time. Composer version 2 addresses those structural weaknesses. It does not reproduce ABBA melodies, recordings, vocal performances or production.

## Chords, hooks and expectation

[Open Music Theory's four-chord schemas](https://viva.pressbooks.pub/openmusictheory/chapter/4-chord-schemas/) discusses the familiar I–V–vi–IV / vi–IV–I–V family, the I–vi–IV–V doo-wop family and rotated forms. Its [earlier singer/songwriter chapter](https://openmusictheory.github.io/popRockHarmony-sscp.html) uses U2's “With or Without You” to illustrate tonic-starting I–V–vi–IV; the [doo-wop chapter](https://openmusictheory.github.io/popRockHarmony-dooWop.html) discusses “Viva la Vida” beginning the related cycle on IV. Harmonic vocabulary is reusable; a melody, rhythm, bass hook, phrase placement and performance give an individual recording its identity.

[Melody and Phrasing](https://viva.pressbooks.pub/openmusictheory/chapter/melody-and-phrasing/) describes a four-phrase pattern of statement, restatement/response, departure and conclusion. The implementation keeps a seeded two-bar idea recognisable, changes the third phrase, leaves breathing space, gives the chorus a distinct rhythm/register and places answering figures late in a bar. These are simple first steps; the engine still needs stronger melodic candidate selection, richer interval/rhythm vocabulary and longer-range development.

Progression variation controls functional substitutions or seventh-chord colours at selected phrase positions. Composer 2 used unrelated fixed verse/bridge plans, making progression selection ineffective there at full contrast. Composer 3 corrects this: verses rotate the selected cycle, bridges reverse it and intros preview its first/last chords. For Four chords in C major, the verse starts Am–F–C–G and the chorus C–G–Am–F; the four-bar bridge closes F–Am–G–G7. Named patterns receive section-ending cadences; custom verses/bridges retain selected symbols when progression variation is zero. Outro cadences use the selected key. At zero contrast and progression variation, the body shares the selected cycle. This is a tonal template rather than a comprehensive style model.

## Evolving instrumentation

Each layer now has intro, verse, chorus, bridge and outro probabilities, plus one-based occurrence overrides. The decision is made once per entire section and saved with the notes. A bass can stay at 100%, an answer can use 25% in verses, and chorus 2 can explicitly omit the snare. Stable layer identifiers isolate decisions from unrelated instrument additions. Musical rests still occur inside a participating part.

Preset probabilities are editable design defaults. Intro variation changes the likelihood of a hook teaser/build within the fixed four-bar opening; dynamics rise through the intro. Verse endings add a short snare transition when that part participates. These rules create coordinated changes rather than independent per-note dropout.

## Endings and the duration ceiling

[Open Music Theory's formal-functions chapter](https://openmusictheory.github.io/popRockForm-functions) discusses repeated closing material, thinning texture, fades and harmonic closure. The [Open University's lesson on endings](https://www.open.edu/openlearn/history-the-arts/listening-form-popular-music/content-section-12) distinguishes repeated/faded conclusions and codas/outros.

The arranger supports weighted choices: repeat the hook and fade, V7–I with a held chord, a repeated two-bar tag followed by V7–I, or a coordinated final hit. Default relative weights are 35/35/20/10, chosen for this demo, not inferred frequencies in successful songs. Weights are normalised; zero disables a type and a single positive weight forces it. Fade is applied to PCM; every new ending has an audio release and a short final anti-click taper.

The planner reserves the outro first, places only complete sections, then finishes early. It does not truncate a verse to fill the requested time. Complete verses/choruses retain four two-bar phrases. Extremely short ceilings can yield only an outro, with a shortened single bar when the tempo makes even a whole bar impossible. The actual duration and chosen ending are visible and stored. Defaults are now a 210-second ceiling, 160 bpm and a 10% offbeat delay; the maximum ceiling remains 300 seconds.

## What still makes a human performance different

The [Witek et al. groove experiment](https://doi.org/10.1371/journal.pone.0094446) found that intermediate syncopation produced higher groove-related pleasure and movement ratings in the tested drum breaks. It does not establish an ideal swing setting, prove that random timing errors improve music, or generalise every result to every musical culture.

Next listening priorities are melody-only auditions, phrase dynamics, interplay that responds to actual lead rests, instrument-specific articulation/ranges, coherent bass hooks, expressive timing and vocal/lyrical meaning. Stereo space and production also matter. These remain broader work in #8–#24; this change supplies inspectable structural/performance controls, not a human-equivalence claim.

For contrasting swing, rock, dance and classical case studies, see [Hooks, development and musical identity](HOOKS_AND_DEVELOPMENT.md). It maps the requested standards and representative artists to the existing enhancement issues and proposes the next implementation sequence.

The [living composition thesis](COMPOSITION_THESIS.md) develops these findings into contextual design principles. Its [versioned rule catalogue](composition-rules.json) also supplies the app's Composition thesis page.

## Replay contract

`ArrangementComposer.Version` is now 4. Legacy `SongGenerator.Version` remains 3 with both original golden fixtures unchanged. Written score schema remains 1: optional performed-duration, ending, participation and motif fields preserve replay of earlier saved notes. Composer 3 stores seeded two-bar ideas and adds an optional independent melody seed; its generated output intentionally differs. Earlier scores replay their notes; those without ending metadata retain their original duration and audio shaping. Exact sampled audio still depends on the bank and renderer/runtime. Settings-only imports deliberately recompose using the current composer. See [STUDIO.md](STUDIO.md) for the new melodic controls and remaining limitations.

Composer 4 fixes the intro and bridge/break at four bars and schedules the break near two-thirds of the available body, after complete verse/chorus pairs. It reserves an eight-bar chorus return and the outro. Very short songs omit sections that cannot fit completely. The SoundFonts lesson now also offers a separate experimental sample-free instrument lab; song rendering continues to use the selected sampled bank. See [measured MIDI and timbre comparisons](SCORE_AND_TIMBRE_STUDY.md) for results and limitations.
