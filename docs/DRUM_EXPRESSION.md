# Groove, variation and expressive drums

Composer **7** adds three independent drum controls. These are original procedural drum gestures, not reconstructions of named drummers or measured artist settings. They implement parts of R05 (groove), R06 (space between parts) and R09 (phrase performance).

| Control | Behaviour |
| --- | --- |
| Drum variation, default 55% | Quiet snare ghosts, kick anticipations, two-bar hat pickups and occasional rests; open hats and a swing-chorus ride option |
| Drum fills, default 60% | Chance and scope of fills at four-bar and section endings, short pickups through snare/tom runs and triplets; occasional chorus landing crashes |
| Drum humanity, default 60% | Role-based accent/velocity touches, a four-bar dynamic arc and a bounded phrase timing curve: snare slightly behind, hats ahead, kick pulse anchors fixed |

Fills are coordinated across the kit. Regular snare strokes inside the fill window are replaced; hats leave that window free. The kick can retain the fill's opening anchor. Written roll/triplet groupings are not swung again, preventing high swing values from reversing adjacent strokes. A fill is available only when an enabled, audible snare part actually participates in that section. Muting or omitting that player therefore does not leave holes in the hat part for an inaudible fill.

At full fill intensity a section ending always gets a fill when the snare participates; internal four-bar boundaries have a 35% chance. Lower settings reduce those probabilities and scope. Short fills contain two strokes in half a beat; medium fills use three or four in a beat; longer fills select four eighths, eight sixteenths, six triplets or a syncopated five-stroke shape across two beats. Snare/tom orchestration and a velocity build lead towards the next arrival. These are bounded templates, not a rudiment grammar or a full physical drummer model.

Humanity is not independent timing jitter. At 100%, the snare's offset is around 12–15 ms behind the swung grid; hats and selected kick anticipations sit up to 6 ms ahead with a small shared phrase contour. Downbeats and integer-beat kick anchors remain fixed. These offsets are design choices, not an empirical optimum. A straight programmed groove is valid: turn humanity down to retain the grid. Ghosts remain deliberately quiet even with humanity at zero.

Bossa retains cross-stick rather than acquiring rock tom runs. Reggae retains its one-drop kick anchor. Waltz and Arpeggio retain the existing no-kit behaviour. Planned outros keep their ending behaviour; new fill runs are excluded from outros. Variation, dynamics and orchestration can still be stylistically unsuitable for a particular score, so audition them in context.

## Controls and replay

The controls appear under Groove & expression and round-trip through settings, song exports and local saves. Changing them does not rewrite harmony, melody or non-drum instruments. They use song-seeded streams independent of melody generation. Fill decisions are shared by kit roles and independent of harmonic event count.

All three at zero retain the earlier literal drum templates, including their small two-hit verse pickup. Thus zero fills means no new fill orchestration, not removal of that earlier gesture. Old demo documents explicitly migrate missing fields to zero. Stored written scores replay their saved notes; regenerating settings uses composer 7. Legacy SongGenerator remains version 3 with unchanged golden hashes.

## Evaluation

Event checks cover multiple fill shapes, toms/triplets, landmark placement, hat space, actual snare participation, ghost-note contrast, kick anchors, duration/velocity bounds, deterministic generation, independent non-drum parts and persistence. An original matched comparison should audition the kit and full ensemble with the same seed, bank, settings and instrument gains, matching whole-mix loudness by attenuation only. Such examples expose the mechanism; they do not establish a universal preference or replace listening.

The local preview's `drum-comparison/index.html` provides two matched 24-second pairs: isolated kit and full ensemble, covering the first Pop verse/chorus at 160 bpm with seed 42, 10% swing and GeneralUser GS 2.0.3. Drum presence is fixed to 100% in both versions. Literal uses zero for all three controls; expressive uses the defaults. RMS matching attenuates the expressive kit by 0.11 dB and the literal full band by 0.38 dB; the other clips need no attenuation. Original score JSON is retained in `temp/drum-audit`. These local generated audition artifacts are not bundled application assets or a blinded listening study.
