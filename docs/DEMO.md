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

The repository publishes the demo to the `gh-pages` branch with `.github/workflows/pages.yml` on every change to the library or the demo, at `https://panoramicdata.github.io/PanoramicData.MusicGeneration/`. The workflow rewrites the base address for a project page, copies `index.html` to `404.html` for deep links, and adds `.nojekyll`.

For a custom domain: set `SITE_BASE` to `/`, add `echo "your.domain" > site/wwwroot/CNAME` to the preparation step, create a DNS `CNAME` record to `panoramicdata.github.io`, and set the domain under **Settings, Pages**.

## Performance note

The library's bit-exact sine is slow in WebAssembly's interpreter, so the demo sets `SongStyle.UsePlatformSine` and says so on the page. Ten seconds of audio renders in about 0.3 s that way.
