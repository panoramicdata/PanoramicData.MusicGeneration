# Musical flow in composer 5

The earlier arranger treated each chord as a writing clock: one chord per bar, with notes cut at the chord boundary. More chords or velocity randomness could not remove that structural rigidity. Composer 5 separates harmonic spans from the bar clock used by each instrument.

## Four independent controls

| Control | Behaviour | Zero |
| --- | --- | --- |
| Harmonic movement | Re-times eligible four-chord groups with held, half-bar or dotted spans, retaining their order and total length | One chord per bar |
| Chromatic colour | Inserts eligible dominant approaches or borrowed iv before tonic; adds short chromatic pickups when there is space | No added chromatic approaches; explicit custom chords are retained |
| Phrase expression | Two-bar timing/velocity contour, connections, prepared suspensions, resolving neighbours, common-tone ties and actual-rest answers | No new phrase shaping or gap coordination |
| Accompaniment variation | Selective late-phrase rests, alternating arpeggio direction and bass approaches | Regular accompaniment patterns |

These are probabilities/strengths within a bounded grammar, not measured artist profiles. More colour is not necessarily better. Repeated hooks can benefit from restraint; overlap and rigid timing can be intentional in other styles. Current answer coordination favours space over independent counterpoint.

The harmonic rhythm patterns are two/one/half/half bars, half/half/one/two, or dotted/half/dotted/half. Outros retain their planned ending harmony. Dominant approaches resolve to the immediately following chord; borrowed minor subdominants resolve to tonic. This does not implement arbitrary modulation or jazz substitutions. Leads may sustain across chords; a dissonance is allowed when its preparation or resolution gives it a purpose.

The phrase timing curve leans ahead inside two bars and returns to the boundary. It does not change the backing tempo and is not an expressive tempo map. Instrument-specific bowing, breath and legato samples remain separate future work. GeneralUser GS sampled rendering and the experimental generated-sound laboratory remain distinct.

## Evidence and limits

The design follows the distinction between harmonic pacing and metre in [Harmonic rhythm](https://musictheory.pugetsound.edu/mt21c/HarmonicRhythm.html), and between preparation, accented dissonance and resolution in [Non-chord tones](https://musictheory.pugetsound.edu/mt21c/NonChordTonesIntroduction.html) and [Adding non-chord tones](https://musictheory.pugetsound.edu/mt21c/AddingNonChordTones.html). These sources support the mechanisms; they do not prescribe universal slider values or prove that the generated pieces are successful.

Tests check continuous positive harmonic spans, directed destinations, cross-harmony notes, actual-rest answers, unchanged drum timing, deterministic reproduction, settings migration and duration boundaries. Such checks establish behaviour, not musical appeal. Original seed-42 Pop comparisons use the same GeneralUser GS 2.0.3 bank, tempo, mix and duration request. Compare the older composer with the new one, and also turn the four controls down to isolate the new mechanisms. An aesthetic preference still requires listening.

The local review artifacts in `temp/flow-audit` retain composer-4 and composer-5 score JSON and full WAVs. The preview's `flow-comparison/` page presents the first complete eight-bar verse (12 seconds at 160 bpm), with the louder full mix attenuated by 0.99 dB to match RMS. No stems are normalised independently. This is an unblinded, single-seed demonstration with several mechanisms changing together; it is not a perceptual study or proof of improvement. The previous baseline is release 0.1.21 (commit 3040a59). These generated audition files are local review artifacts, not bundled application assets.

## Replay and controls

Composer version is **5**; legacy SongGenerator remains version 3 with unchanged golden hashes. Stored written scores retain their notes and version when replayed. Regenerating settings uses the current composer, including when an old document is imported. Missing new controls in old demo documents migrate to zero. New documents preserve their explicit controls and optional chord-function/resolution metadata.

The studio groups settings into five keyboard-accessible tabs. Each row gives a setting, control and explanation; generation feedback and playback stay outside the tabs. This keeps the sound available while moving between related decisions.
