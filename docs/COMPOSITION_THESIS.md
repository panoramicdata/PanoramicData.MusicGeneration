# Composition as identity, expectation and purposeful change

## A living thesis for PanoramicData.MusicGeneration

Thesis revision **0.3**, 22 October 2026. Companion rule set **0.4.0**. This is a working design thesis, not an academic qualification, a universal account of music or a claim that the generator already implements its proposals.

**Central proposition:** a convincing composition establishes an audible identity, develops expectations around it, and makes its departures and returns meaningful. Its instruments, harmony, rhythm, form and performance should cooperate in that purpose. Randomness supplies possibilities; selection and relationships give those possibilities direction.

Our application should make these relationships audible, inspectable and editable. It should help a listener become a composer by exposing meaningful choices gradually. Better samples and more tracks expand the palette, but cannot substitute for deciding what the music is saying and which part deserves attention.

The thesis develops the source-based studies in [Musical expression](MUSICAL_EXPRESSION.md) and [Hooks and development](HOOKS_AND_DEVELOPMENT.md). The normative rules are maintained once in [composition-rules.json](composition-rules.json), which also supplies the app's rule cards. Rule IDs are stable references, not an order in which every composition must obey instructions.

## 1. What we mean by composition

Composition determines musical material and relationships: notes, durations, rhythms, harmonic movement and the organisation of ideas. Arrangement distributes that material among roles and instruments. Performance determines how a particular realisation articulates, stresses, times and releases it. Production determines how those performances occupy recorded space and reach a listener. These activities interact, but separating them makes cause and effect easier to understand.

A guitar riff can be both compositional identity and an arrangement signature. A held chord can acquire motion through orchestration. A repeated note can become a phrase through timing and emphasis. Therefore, we should neither reduce composition to a chord list nor treat everything beyond the score as cosmetic. We should preserve the written idea while allowing several arrangements and performances of it.

Our current program is a deterministic procedural composer. The word model here means its explicit musical data and decision processes; this thesis does not assume a trained neural model. A seed should select reproducible decisions within a declared composer version. A saved written score should retain those decisions even after a new version offers better choices.

## 2. Identity comes before novelty — R01, R02

A hook is any recognisable idea that attracts and holds attention. It may be a melody, bass figure, rhythmic gesture, chordal sonority, sound or interplay between parts. A chorus is a formal role, not a synonym for hook. Music can contain several hooks, and music without a prominent singable refrain can still have a strong identity.

The useful unit is often small enough to remember: a rhythm with a characteristic rest, an interval followed by a turn, a bass pickup, an answering accent. Our system should represent such material independently of its eventual pitches and instrument. A motif needs a rhythm, contour or interval pattern, accents, rests, phrase boundaries and an identity that survives its realisations. A realisation should record which aspects it preserves and which it changes.

Recognisability does not require literal repetition on every return. It requires audible landmarks. Reharmonising a phrase should preserve intended arrival notes or explicitly record a change in their function. Automatically snapping every accented note to the nearest chord tone can erase the very interval that made the phrase distinctive. It can also exclude expressive suspensions and other non-chord tones whose resolution is the point of the phrase.

ABBA offers a useful balance: Benny Andersson describes melody and chords as foundations, while the band's account of Mamma Mia shows how production gestures contribute to a recording's identity. Neither observation establishes a guaranteed formula. For us, it suggests auditioning an original idea with a plain sound and in context, then selecting deliberately rather than accepting the first seed. [Andersson interview](https://www.musicbusinessworldwide.com/every-songwriter-needs-to-sit-there-keep-going-and-wait-for-it-to-happen/), [ABBA recording account](https://abbasite.com/articles/mamma-mia-the-song-that-saved-abba/).

## 3. Repetition and development operate at several scales — R02, R03

Repetition makes a relationship available to memory. Development changes its direction, function or context while retaining a connection to what preceded it. Changing every event can destroy continuity; changing almost no pitches can still produce a meaningful journey if the surrounding texture, harmony or performance evolves.

We propose four operations: establish, vary, develop and recall. Establish presents material clearly. Vary changes selected details while protecting landmarks. Develop transforms or combines material toward a destination. Recall restores a recognisable idea after an absence that makes its return significant. A single variation percentage cannot describe all four operations.

At phrase scale, development may use a different ending, fragmentation, sequence, inversion, rhythmic augmentation or an answer. At section scale, it may change harmonic rhythm, register or foreground role. At whole-piece scale, it may withhold an idea, bring it back in another context, or reveal a relationship between previously separate themes. These processes should have explicit source relationships in the score.

Bach's inventions provide a compact example of ideas developed by combining lines; the Netherlands Bach Society describes this as part of their teaching purpose. The proposed lesson is economy and transformation, not adding a Baroque instrument to an unchanged pop algorithm. [Performer and historical context](https://www.bachvereniging.nl/en/bwv/bwv-772-786).

Four two-bar phrases remain our default verse and chorus design. They should have different functions, such as statement, restatement, departure and conclusion, rather than being four unmotivated loops. This is a starting form. A suite, dance strain or contrapuntal passage may require a different organisation.

## 4. Harmony is directed time — R04

A progression is more than an ordered set of chord names. The time spent on each harmony, the bass movement, inversion, voice leading, preparation and type of arrival all affect its function. The same chord family can support very different musical results when its harmonic rhythm and phrase placement change.

Our harmonic plan should therefore describe spans in beats, local tonal context and intended destinations. A chord may last several bars; another may occupy part of one bar. A prechorus may increase activity before an arrival. A bridge may suspend closure. A repeated chorus may regain stability without mechanically ending every phrase on the tonic.

Tension is contextual. Dissonance may resolve, remain as colour or be the stable vocabulary of a style. A minor chord is not a universal sadness instruction, and a major chord is not a happiness switch. We should expose harmonic direction, stability and colour as choices, with examples that let users hear the distinctions.

Known progressions are useful teaching and composition materials. They are not proxies for the full appeal of the recordings that use them. Store a pattern independently of the key, and explain how voicing, rhythm and melodic emphasis change its effect. Our research report supplies case studies and score references rather than treating a famous title as a preset specification.

## 5. Groove is a relationship between parts — R05, R06

A beat grid is a coordinate system. Groove emerges from patterns of attack, rest, duration and accent around it, including the way parts reinforce or leave room for one another. A bass line and kick should be composed in relation to each other. A guitar can contribute motion by avoiding their strongest attacks. Different traditions permit different relationships.

Our current 10% swing default delays selected offbeats. It is a user preference, not an empirical optimum or a complete swing performance. Compound metre and swung subdivision should be represented separately. Deliberate lead or drag must relate to phrase and instrumental role; independent timing noise is not an adequate account of human performance.

Witek and colleagues found an intermediate-syncopation preference for groove-related ratings in their tested drum breaks. That supports testing structured rhythmic complexity; it does not prescribe one amount for all music or establish that timing errors improve a track. [Original experiment](https://doi.org/10.1371/journal.pone.0094446).

Daft Punk's recording practice also reminds us that programmed organisation and played rhythm sections can coexist. Our inference is to model repeatable interactions and articulation, not to simulate humans by adding arbitrary instability. [Engineer Mick Guzauski's account](https://sonicscoop.com/icons-mick-guzauski-on-engineering-and-mixing-daft-punks-random-access-memories/).

## 6. An ensemble needs an allocation of attention — R06, R07

Each layer should have a purpose: foreground statement, answer, harmonic support, rhythmic support, sustained colour or transitional gesture. Several instruments can share a role, but arbitrary stacking should not create accidental duplicates or continuous competition for the same register.

Participation probabilities answer whether a part is present in a section. They do not answer what it does while present. We also need density, entry shape, response windows, register, duration and foreground priority. Musical constraints should resolve combinations that would remove the intended hook or fill its breathing space. The user should be able to override the intention explicitly.

The response must follow the actual lead phrase, not a presumed empty beat. A countermelody can coexist with the lead when register, contour and rhythm support it; universal non-overlap would be another poor rule. The practical question is whether the relationship is intentional and audible.

The palette matters through idiom. Strummed guitar, bowed strings, brass breath, piano decay and vocal phrasing imply different attacks, releases and feasible gestures. SoundFonts improve timbre but do not supply that writing. Instrument range and articulation should constrain proposed notes before rendering, with transparent exceptions for deliberately unusual effects.

## 7. Form and performance can imply a journey — R08, R09, R10

We should represent energy, tension, density, register and stability separately. A quiet suspended passage can be tense; a loud repeating chorus can be stable. A narrative plan can specify introduction, growth, interruption, climax and release without pretending that numerical envelopes convey a particular plot to every listener.

Verses, choruses, bridges and prechoruses are useful functional relationships. Open Music Theory describes prechorus energy gain and uses Livin' on a Prayer to distinguish several such roles. These functions should guide musical decisions rather than merely label time ranges. [Formal functions and examples](https://openmusictheory.github.io/popRockForm-functions).

There should also be forms with no conventional chorus. Brian May's account of Bohemian Rhapsody describes its construction in pieces; our inference is that a sectional suite needs its own transitions and dramatic proportions. Hey Jude demonstrates another possibility: a long repeated ending grows through instrumentation and performance. [May interview](https://brianmay.com/brian-news/2021/01/brian-may-total-guitar-feature/), [Beatles archive](https://www.thebeatles.com/hey-jude).

Performance should make phrase intent audible through stress, attack, duration, breathing and timing. A crescendo ought to have a phrase or structural purpose, not simply increase gain throughout the piece. Melodic peaks need preparation; pauses can be consequential. Vocal and lyrical storytelling adds semantics that our present instrumental composer does not supply. An emotional tag cannot fill that gap by itself.

Every generated song must keep an audible ending within its duration ceiling. Reserve the outro before allocating the body, and prefer a complete earlier finish to an arbitrary cutoff. Cadence, tag, coordinated stop and fade are useful choices; future ending plans can add refrains and staged departures. The exact ending and release must be stored for replay. This product requirement is stronger than the stylistic recommendations elsewhere in the thesis.

## 8. The user needs musical agency — R11

Expose choices in the order that they become useful. A first experience should offer an idea, a groove, an ensemble and a form. Advanced controls can then reveal how the idea develops, how harmony moves, where parts enter and how phrases are performed. Every control should explain what it changes, what it preserves and a comparison the user can hear.

Independent levers should correspond to genuinely different decisions. Motif development, progression variation, section contrast and performance expression should not be aliases for randomness. When parameters interact, show the consequence: stronger harmonic novelty may need more melodic adaptation; more foreground layers may need less density per layer.

Pinning selected material is a necessary form of authorship. The user should be able to retain a hook while changing its bass or orchestration, regenerate a bridge without losing the chorus, and compare performances of the same written notes. These are proposed capabilities. The current UI's seed, variation and layer controls provide a foundation, not all of this agency already.

## 9. Artist and period comparisons are evidence-bearing recipes — R12

An artist is not one fixed position on a collection of sliders. Work changes across recordings, periods, collaborators, live contexts and production methods. Future demonstrations should name a specific reference and version, describe the mechanism being studied and identify what our example leaves out. A date is descriptive metadata, not evidence of a continuous stylistic trajectory.

A recipe should group musical levers: motif persistence and transformation, harmonic rhythm and destination, metre/subdivision, role interaction, density trajectory, articulation and production. It should also record evidence and confidence. A low/medium/high description is a teaching hypothesis until we can relate it to measurable musical events; percentages should not be invented for an artist.

For example, an original exercise can contrast a persistent dance groove with a slowly unfolding texture, holding the underlying motif constant. Another can contrast two-voice imitation with a chorus-based arrangement. These demonstrate combinations of mechanisms. They are not reconstructions, endorsed artist settings or claims to generate new work by the named musicians.

The first future gallery should use the references in the research report and compare recordings individually. Later, source-backed observations from multiple periods can show changes within an artist's practice. Add original audio examples only when the required levers exist, and label the version of the rules and composer used to generate them. The gallery is tracked in [issue #39](https://github.com/panoramicdata/PanoramicData.MusicGeneration/issues/39).

## 10. Evaluation and revision — R13

An inspectable rule is not necessarily an aesthetically successful rule. Event tests should verify promises about transformation, harmonic destination, timing, range and duration. Listening should assess whether the intended identity, direction and interaction are perceived. Neither a golden hash nor a numerical musicality score can stand in for that judgement.

Use original comparison scores with the same seed, bank and comparable loudness. Audition melody alone, the rhythm section and the full ensemble. Compare literal repetition against controlled development, and random independent parts against coordinated roles. Use blinded labels where practical; record listener disagreement and context rather than smoothing it into a claim of universal preference. This work is tracked in issue #37.

Different pieces require different criteria. Singability is appropriate for some refrains, not every texture. A sparse work should not be penalised for avoiding a dramatic chorus, nor a contrapuntal work graded solely as a dance track. Rules describe options and constraints within an intent, not one aesthetic ranking.

The canonical rule catalogue has its own semantic version and revision history. Clarifications increment the patch version; additions and compatible extensions increment the minor version; changes to the meaning or removal of a rule require a major version and a migration note. Never reuse an ID for a different idea. Retired rules retain their identity and explanation in the history.

Each composition change should identify affected rules, describe evidence or reasoning, update implementation status and supply an audible comparison when behaviour changes. Research can revise a rule without changing the composer; changing new seeded output requires a composer-version decision. Existing saved scores must continue to replay their written decisions. The rule-set version is explanatory provenance, not an instruction to recompose old scores.

## Current commitments and next step

Revision 0.1 commits us to explicit musical relationships, transparent controls, contextual rules and honest evaluation. It does not promise an automatic hit generator. The immediate implementation sequence is persistent, auditionable motifs (#8), coordinated ensemble phrases (#19), harmonic spans and destinations (#9), richer functional forms (#20), then more idiomatic performance (#16, #10, #11).

The app presents the rule catalogue with current and planned capabilities distinguished. Composer 3 supplies an initial step for R01/R02/R04/R11: stored two-bar ideas, a separate melody seed, stronger phrase development and harmony derived from the selected progression. Exact-note editing, candidate ranking and richer ensemble interaction remain proposed. As we develop further theory lessons, every new lever should join this explanation: what decision it gives the composer, which relationship it changes, and how we can demonstrate that change by listening.

## Revision 0.2: measure the gap before declaring success

The [score and timbre study](SCORE_AND_TIMBRE_STUDY.md) adds thirteen parsed MIDI references and reproducible orchestral spectrogram comparisons. Our dense orchestral preset still changes activity little; this supports prioritising independent phrase roles and entrances over track count. Score-derived MIDI and unverified popular arrangements must not be mistaken for recorded human performances. Fixed-duration density windows are diagnostics, not musical-quality measures.

Composer 4 implements four-bar intro/break sections and a later departure in longer songs (R03/R08), retaining the complete-ending constraint. A separate sample-free instrument experiment supplies attack/sustain and register-dependent spectra plus noise, envelope and pitch parameters (R07). Unfitted-note results are mixed, especially flute; fitted spectral resemblance cannot establish perceptual equivalence. R13 requires independent recordings, articulations and blind listening before claiming realism. Existing saved scores replay their written events; the experiment does not replace the sampled studio renderer.

## Composer 5: relationships across boundaries — R04, R05, R06, R09, R11

The practical response to the rigid bar grid is documented in [Musical flow](MUSICAL_FLOW.md). Harmonic events have independently planned durations; instrument writers retain their own subdivision clocks. A held harmony need not restart a melody or drums. Dissonance can carry a destination, and a sustained note can change harmonic meaning.

Phrase expression uses a bounded two-bar contour, connected durations and prepared/resolving non-chord tones. Answering parts consult actual lead occupancy. These mechanisms support flow; they do not establish that an output is compelling. Listening, selection and revision remain essential. Full tempo maps, instrument-specific transitions and editable long-form phrase intentions remain future work.
