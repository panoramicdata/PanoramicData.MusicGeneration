# Two hands and a voiced treble part

Composer 6 adds piano-specific writing for General MIDI bank 0 programs 0–7 when a layer plays Melody or Arpeggio. The per-layer **Two-hand piano** switch defaults on for newly created layers. This implements an initial part of R07 (instrument-specific writing), in cooperation with R06 (attention and ensemble space).

The original melody remains the top line. Selected longer, near-beat chord tones gain a quieter chord tone underneath; ragtime can use two inner notes, producing three-note right-hand voicings. Inner voices stay within an octave below the melody and at least a minor third beneath it. Passing notes, suspensions and shorter runs can remain single notes. Added tones stop at the harmony boundary, and avoid re-attacking a pitch that the original line is already sounding.

An otherwise unsupported piano melody/arpeggio gains quieter low-register bass/chord gestures, using the selected beat template. The left-hand register sits beneath the written line, with each voicing bounded to an octave. Solo endings also receive a lower root with the planned final gesture. The original melody's register is preserved; this does not move it back up an octave. Extreme user octave shifts can leave too little room below the melody, in which case the extra notes are omitted.

An audible bass or chord part on the same piano program supplies the existing accompaniment for that section. The melody then gains treble voicings without a second generated left hand. Thus the ragtime preset retains its separate stride chords and bass. Other instrument families and dedicated bass, chord and answering roles keep their existing writers. Added inner tones are 18 velocity units softer than their source note; left-hand notes are 28 units softer (ending roots 24). These are design defaults, not artist measurements.

This is a bounded arrangement heuristic, not a complete fingering, pedal or conservatoire piano-performance model. Left-hand rhythm follows the selected groove; all parts still need listening and editorial judgement. In particular, physical feasibility across independent user-stacked piano layers is not guaranteed by a per-layer octave bound.

## Replay and evaluation

The written arranger advances to version **6**; legacy SongGenerator and its golden hashes are unchanged. Replaying a saved score uses its stored notes. Newly generating settings uses composer 6. Missing PianoTwoHands fields in older demo documents migrate to false, while explicit selections round-trip through import/export, the editor and local saving.

Checks cover retained tune notes, added treble intervals and three-note ragtime voicings, quieter supporting notes, lower-register accompaniment and endings, existing-stride reuse, other instrument parts, presence, duration bounds, deterministic generation and JSON replay. A matched original solo-piano/ragtime audition should compare the switch off/on with the same seed, key, groove, bank and mix. Full-mix RMS matching should attenuate the louder clip, without separately normalising the hands. These checks establish mechanisms; listening remains necessary to assess musical appeal.

Local review artifacts retain original seed-42 scores in `temp/piano-audit`. The preview's `piano-comparison/index.html` provides the first complete verse with the switch off/on: solo piano at 160 bpm (12 seconds) and piano-only ragtime at 116 bpm (16.55 seconds), using GeneralUser GS 2.0.3. Full-mix RMS matching attenuates the off version by 1.65 dB for solo and 2.08 dB for ragtime; on versions need no attenuation. The ragtime pair retains the same stride/bass parts. These unblinded examples demonstrate the new writing; they are not a listening study and are not bundled app assets.
