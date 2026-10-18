# Development prompt for PanoramicData.MusicGeneration

Give this whole file to a Claude Code session opened in this repository. It is written to be self-contained.

---

You are developing `PanoramicData.MusicGeneration`, a deterministic, seeded music generation library for .NET with an educational browser demo, in this repository (panoramicdata/PanoramicData.MusicGeneration). The first version is already built and bootstrapped; you are extending it. Start by reading `README.md`, `docs/DESIGN.md` and the code (it is small), then run `dotnet build` and `dotnet test` and report the starting state faithfully.

## Why this exists

PanoramicData.SoundPrint (github.com/panoramicdata/PanoramicData.SoundPrint, an audio fingerprinting library) needs music-like test audio that is varied, repeatable and free of copyright, plus the ground truth of what was played. It currently has a throwaway generator (`SyntheticSong` in its `PanoramicData.SoundPrint.SyntheticAudio` project) that this library supersedes and improves on (its melody sat an octave or two too high; here it is held between middle C and C6 by default). The generator is interesting and useful in its own right, so it lives here as a separate package and teaching resource. SoundPrint will take this library as a dependency **later**, once it is published and stable; do not edit SoundPrint.

## Hard rules

- Public repository, MIT licence. No recordings, samples or third-party music anywhere. Everything is synthesised arithmetically. Generated audio is not committed (the `/out/` folder is gitignored).
- **Determinism is the product.** The same seed, length, sample rate and style give identical notes always and identical samples on every machine until `SongGenerator.Version` increments. Any change that alters output for an existing seed is a breaking change: bump `Version`, deliberately re-record the golden hash and tell the owner. New features must not change the output of existing seeds unless you bump the version; prefer new `SongStyle` options whose defaults reproduce the current output.
- No `System.Random` in the library (use `Pcg32` with a distinct stream per independent decision) and no `Math.Sin` on the default path (use `DeterministicMath`); `UsePlatformSine` is the opt-out.
- Tests first (xUnit v3, AwesomeAssertions, Microsoft.Testing.Platform). Never skip or weaken a test to make it pass; unit tests must never skip (`failSkips: true`). Run commands and read the output before claiming anything works. Keep 100% line and branch coverage of the library, with justified exclusions only.
- .NET 10, nullable enabled, warnings as errors, central package management, NuGet versions pinned exactly. No optional parameters on public methods (use overloads), XML docs on every public member.
- No em dashes anywhere (code, docs, commits, chat). Use commas, colons, semicolons or new sentences.
- Do not add Co-Authored-By or any Claude or Anthropic attribution to commits or pull requests.
- Do not edit anything outside this repository. Do not push to other repositories.
- Commit after each task with a clear message. Commit to `main` directly; no pull requests. Before each push build the whole solution (`dotnet build`) and run the tests: a push once broke CI because a project had not been built locally.
- When you need the owner's answer, ask with the structured question tool, not prose.
- Use a scratch folder outside the repo (or the gitignored `/out/`) for experiments.
- The bash tool in this environment fails on heredocs containing apostrophes; write files with the file-writing tool. C# files must have CRLF line endings (`.editorconfig` requires it and the build enforces it).

## Repository standards

The repository was bootstrapped to the standards enforced by panoramicdata/PanoramicData.NugetManagement (see `NEW_NUGETS.md` there and the paragon Splunk.Api): `.editorconfig`, `global.json` with the Microsoft.Testing.Platform runner, NBGV `version.json`, `Directory.Build.props`, `Directory.Packages.props`, CI and CodeQL workflows, Dependabot, `Publish.ps1`, `SECURITY.md`, `CONTRIBUTING.md` (the standard text, kept verbatim because rule COM-02 compares it exactly), snupkg symbols, coverage, `CLAUDE.md` and `AGENTS.md`. Do not redo it; keep it green. The governance assessment can be run with a scratch harness around the NugetManagement rule library (clone it to a scratch folder, read only); the Codacy public API lists a public repository's issues without a token. Codacy exclusions live in `.codacy.yml` (tests and the demo). The Codacy project, badge and `CODACY_PROJECT_TOKEN` secret are set up by the owner (a sibling repo's `../PanoramicData.Skills/.github/skills/codacy` skill can mint the token); until then the Codacy rules in the assessment will fail and that is expected, not a regression.

## Solution layout

- `PanoramicData.MusicGeneration`: the library (no dependencies).
- `PanoramicData.MusicGeneration.Test`: unit tests.
- `PanoramicData.MusicGeneration.Demo`: Blazor WebAssembly educational site, published to the `gh-pages` branch by `.github/workflows/pages.yml`.

## Phases and checkpoints

Work unattended within a phase. Stop and wait for the owner at each checkpoint, summarising what was done, the numbers and any decision needed.

1. **Verify.** Build, test, check coverage is 100%, run the demo (`dotnet run --project PanoramicData.MusicGeneration.Demo`) and look at every page in a real browser (a puppeteer-core script driving Edge works; headless "virtual time" screenshots can show a false "Loading"). Check the governance assessment. Report.
2. **Musical structure.** Real songs repeat with variation; the current generator deliberately never repeats. Add `SongStyle` options (defaults reproduce today's output) for: sections (verse, chorus, bridge) that repeat a chord progression and a melodic phrase with controlled variation; time signatures other than 4/4; swing; key changes; humanised timing and velocity; a `Humanise`/`Dynamics` control. Each with tests, including that defaults leave the golden hash unchanged. Offer the owner a decision if a change would need a `Version` bump.
3. **Timbres and ground truth.** More instrument timbres (plucked, bowed, organ, bell) as additive recipes; simple filters and envelopes; optional stereo; a ground-truth export (`NoteEvent` list as JSON or CSV, and a MIDI file writer); a `SongSpec` type that fully describes a song so it can be saved and replayed.
4. **Educational site.** Extend the demo in the same style as the existing pages (A-level maths audience; formulas, interactive controls, axes labelled, honest limits). Lessons to add: harmony and consonance (frequency ratios and why 3:2 sounds good; the circle of fifths; chords on the piano roll); rhythm and time signatures; additive synthesis and Fourier (build a square wave from harmonics, with a spectrum plot); envelopes (attack, decay, release, with the curves); noise and drums (white noise, filtering, the kick's falling pitch); randomness and determinism (PCG32 step by step, why a seed is a song, why `System.Random` is not enough); and a lesson that analyses a generated song's spectrogram next to its piano roll so the notes can be matched to the bright lines. When the owner's `PDMediaPlayer` (PanoramicData.Blazor) gains its spectrogram, key-point and landmark overlays, use it for the players.
5. **SoundPrint handover.** Prepare for SoundPrint to depend on this package: freeze and document the API; add a `Version` history in `docs/`; supply a migration note for replacing `SyntheticAudio.SyntheticSong`; provide any ergonomics SoundPrint's tests need (for example generating at several sample rates, voices on and off, and a `Seeds.Mix`-style helper to derive related seeds). Do not edit SoundPrint.

**CHECKPOINT after phases 1, 2 and 3: stop for approval.**

6. **Docs and quality.** README and docs current, XML docs complete, Codacy grade A (once the owner has set the project up), governance passes every rule, demo verified in a real browser.

**CHECKPOINT: stop before the first NuGet publish for approval.**

7. **Release.** Publish through the repository's process (`Publish.ps1`, which tags and lets CI publish through NuGet trusted publishing; the owner creates the nuget.org trusted-publishing policy for the package id `PanoramicData.MusicGeneration` first: a missing policy makes the "Login to NuGet" step fail with HTTP 401). Verify the package appears on NuGet and comment on this repository's tracking issue if there is one.

## Reporting

At each checkpoint and at the end, report faithfully: what passed, what failed, what was skipped and why, with command output. Do not claim success without running the verification commands. GitHub's API rate limit (5,000 an hour) is shared across tools and agents: poll sparingly.
