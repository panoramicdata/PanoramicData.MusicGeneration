# Browser demo

`PanoramicData.MusicGeneration.Demo` is a Blazor WebAssembly app that runs the library in the browser. There is no server: songs are generated, drawn and played entirely on your device.

## Run it

```shell
dotnet run --project PanoramicData.MusicGeneration.Demo
```

Open the address it prints. To publish static files for any web host:

```shell
dotnet publish PanoramicData.MusicGeneration.Demo -c Release -o site
```

and serve `site/wwwroot` with a rule that returns `index.html` for unknown paths.

## Pages

| Page | What it teaches |
|---|---|
| **Overview** | What a seed does and what the library is for. |
| **Pitch and frequency** | Octaves as doublings, the twelve-tone ratio 2^(1/12), the formula `f = 440 * 2^((m - 69)/12)`, cents, and harmonics. Releasing the note or detune slider generates and plays a two-second tone; Play it replays the selection. The player's built-in spectrogram shows the actual audio's fundamental and harmonics. The initial tone is generated silently on page load. |
| **Build a song** | Choose a seed, which parts play and the melody's range; see the piano roll of every note (axes labelled), play the result, and read how each part is made, with the formulas. |

## Hosting it (GitHub Pages)

The repository publishes the demo to the `gh-pages` branch with `.github/workflows/pages.yml` on every change to the library or the demo, at `https://music.panoramicdata.com/`. The workflow uses `/` as the base address, writes the custom-domain `CNAME`, copies `index.html` to `404.html` for deep links, and adds `.nojekyll`.

The Joker DNS record is `music.panoramicdata.com CNAME panoramicdata.github.io`, with a 3,600-second TTL. The repository Pages domain is `music.panoramicdata.com`. Keep the deployment workflow CNAME in sync with any future hostname change.

## Performance note

The library's bit-exact sine is slow in WebAssembly's interpreter, so the demo sets `SongStyle.UsePlatformSine` and says so on the page. Ten seconds of audio renders in about 0.3 s that way.

Song generation shows an indeterminate progress bar and separate messages for generating music and preparing playback. Mute and solo show a mix-update message. Controls are disabled while work is in progress, and the browser paints the feedback before synchronous audio processing starts.

## Track controls and player notes

The song page captures raw stems once. Mute and solo update playback, the piano roll and the notes table without regenerating. The gain stays fixed at the original full mix gain. Selecting a different mix reloads playback from the start. Solo takes precedence over mute; leaving solo restores the mute choices.

The Visualizer selector switches between the piano roll and PDMediaPlayer's built-in spectrogram of the audible mix. Switching views keeps the same audio and playback position; returning to the piano roll redraws it. The selected view is retained when generating another song or changing the mix. Spectrogram analysis supports the full 300-second song limit.

Both song and pitch pages use PDMediaPlayer. The pitch page uses its built-in spectrogram without a MIDI lane. The song page supplies PDMediaNote events and PDMediaSegment sections to its integrated piano roll. The component owns pitch/time labels, section annotations and the playhead, aligned with its seek bar. There is no separate demo note canvas.

For component development, UseLocalBlazor=true builds against the sibling project. BlazorProjectPath can point at a separate Blazor worktree. Normal builds use the pinned released package.

The song page defaults the melody range to C3–C5 (MIDI 48–72). The range controls can lower or raise it; section register variations remain within that selected range.

## Section composition

The song page's Composition selector offers free-running generation, returning verses/choruses and a through-composed comparison. Selecting a structured example sets a 60-second preview; the length slider can then shorten or extend it. The player segment lane, piano-roll labels and section table share the same performed boundaries. Each complete verse and chorus contains four phrases. The table reports planned phrases, bars per phrase, cropped sections, transition fills and the final tonic cadence. Requests too short for the prescribed form crop its final audible section; longer requests fit whole-bar phrase lengths while retaining four repeats.

For a direct structured preview, use `/song?form=sections`; `/song?form=through` selects the comparison. Both preserve the same opening, tempo, key and section allocation. Later returns in the through-composed mode use new material identities, so this compares repetition in composition rather than claiming an identical written score. Short durations can omit the bridge or outro; the table makes this visible.

## SoundFont comparison

Choose SoundFont instruments or open `/song?form=sections&sound=soundfont`. The demo loads GeneralUser GS 2.0.3 once (30.8 MiB), then renders the same written notes with selectable instruments and a drum kit. Procedural synthesis remains available for comparison. SF2 files can also be loaded locally. See [SoundFont design, asset terms and resource budgets](SOUNDFONTS.md).
