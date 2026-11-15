"""Measure local reference MIDIs and exported arrangements; never publishes reference notes."""

import argparse
import json
from pathlib import Path

from midi_features import features, read_midi
from research_io import load_reference


def generated(path):
	score = json.loads(path.read_text(encoding="utf-8"))
	settings = score["Settings"]
	beat = 60 / settings["Tempo"]
	tracks = []
	for track in score["Tracks"]:
		layer = track["Instrument"]
		if layer["Pattern"] in (5, 6, 7):
			continue
		part = [
			(
				n["StartSeconds"] / beat,
				n["DurationSeconds"] / beat,
				n["MidiNote"],
				n["Velocity"],
			)
			for n in track["Notes"]
		]
		if part:
			tracks.append((layer["Name"], part))
	bar = 3 if settings["Beat"] == 6 else 4
	return features(
		tracks,
		{
			"tempoValues": 1,
			"tempoEvents": 1,
			"meters": [f"{bar}/4"],
			"pedalEvents": 0,
			"firstBarQuarterBeats": bar,
			"composerVersion": score["ComposerVersion"],
			"seed": settings["Seed"],
		},
	)


def main():
	parser = argparse.ArgumentParser(description=__doc__)
	parser.add_argument("--input", type=Path, default=Path("temp/score-research"))
	parser.add_argument("--output", type=Path, default=Path("docs/research"))
	parser.add_argument(
		"--download",
		action="store_true",
		help="Fetch the exact public URLs in the manifest; raw files stay local",
	)
	args = parser.parse_args()
	args.input.mkdir(parents=True, exist_ok=True)
	args.output.mkdir(parents=True, exist_ok=True)
	manifest = json.loads(
		Path(__file__).with_name("midi-sources.json").read_text(encoding="utf-8")
	)
	results = {}
	for source in manifest:
		path = args.input / (source["name"] + ".mid")
		load_reference(path, source, args.download)
		results[source["name"]] = features(*read_midi(path))
	for path in sorted(args.input.glob("generated-*.json")):
		results[path.stem] = generated(path)
	(args.output / "midi-metrics.json").write_text(
		json.dumps(results, indent="\t") + "\n", encoding="utf-8"
	)
	for name, r in results.items():
		print(
			f"{name:23} notes={r['notes']:5} tracks={r['pitchedTracks']:2} "
			f"range={r['pitchMin']}-{r['pitchMax']} gate={r['medianGateBeats']:5} "
			f"tempo-values={r['tempoValues']:3} density-CV={r['densityCV']}"
		)


if __name__ == "__main__":
	main()
