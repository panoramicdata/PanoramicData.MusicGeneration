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
| **Pitch and frequency** | Octaves as doublings, the twelve-tone ratio 2^(1/12), the formula `f = 440 * 2^((m - 69)/12)`, cents, and harmonics; play any note and see its frequency and harmonic series. |
| **Build a song** | Choose a seed, which parts play and the melody's range; see the piano roll of every note (axes labelled), play the result, and read how each part is made, with the formulas. |

## Hosting it (GitHub Pages)

The repository publishes the demo to the `gh-pages` branch with `.github/workflows/pages.yml` on every change to the library or the demo, at `https://music.panoramicdata.com/`. The workflow uses `/` as the base address, writes the custom-domain `CNAME`, copies `index.html` to `404.html` for deep links, and adds `.nojekyll`.

The Joker DNS record is `music.panoramicdata.com CNAME panoramicdata.github.io`, with a 3,600-second TTL. The repository Pages domain is `music.panoramicdata.com`. Keep the deployment workflow CNAME in sync with any future hostname change.

## Performance note

The library's bit-exact sine is slow in WebAssembly's interpreter, so the demo sets `SongStyle.UsePlatformSine` and says so on the page. Ten seconds of audio renders in about 0.3 s that way.

## Track controls and player notes

The song page captures raw stems once. Mute and solo update playback, the piano roll and the notes table without regenerating. The gain stays fixed at the original full mix gain. Selecting a different mix reloads playback from the start. Solo takes precedence over mute; leaving solo restores the mute choices.

Both song and pitch pages use PDMediaPlayer. The published package uses the demo's adjacent canvas roll. To review the new integrated note lane before the upstream component release, check out the `feature/pdmediaplayer-notes` branch in sibling `../PanoramicData.Blazor` and run:

```powershell
dotnet run --project PanoramicData.MusicGeneration.Demo -p:UseLocalBlazor=true
```

This opt-in builds against the sibling project and passes the generated events as `PDMediaNote` items with `ShowNotes=true`. Normal builds use the pinned NuGet package and keep a working piano-roll fallback. Once the new component API is published, update the package and remove this temporary build switch and fallback. The note lane is separate from the spectrogram work in Blazor PR #250; no spectrogram overlay is claimed here.
