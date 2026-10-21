"""Measure local reference MIDIs and exported arrangements; never publishes reference notes."""

import argparse
import hashlib
import json
import urllib.request
from collections import Counter, defaultdict, deque
from pathlib import Path

import mido
import numpy as np


def read_midi(path):
	file = mido.MidiFile(path)
	if file.type == 2:
		raise ValueError("Asynchronous type-2 tracks require separate timelines")
	tracks, tempos, meters, pedals = [], [], [], 0
	for index, track in enumerate(file.tracks):
		tick, active, parts, name = 0, defaultdict(deque), defaultdict(list), str(index)
		for message in track:
			tick += message.time
			if message.type == "track_name":
				name = message.name[:80]
			if message.type == "set_tempo":
				tempos.append((tick, message.tempo))
			if message.type == "time_signature":
				meters.append((tick, message.numerator, message.denominator))
			if message.type == "control_change" and message.control == 64:
				pedals += 1
			if message.type not in ("note_on", "note_off") or message.channel == 9:
				continue
			key = (message.channel, message.note)
			if message.type == "note_on" and message.velocity > 0:
				active[key].append((tick, message.velocity))
			elif active[key]:
				start, velocity = active[key].popleft()
				if tick > start:
					parts[message.channel].append(
						(
							start / file.ticks_per_beat,
							(tick - start) / file.ticks_per_beat,
							message.note,
							velocity,
						)
					)
		if any(active.values()):
			raise ValueError(f"Unclosed notes in {path}, track {index}")
		for channel, notes in parts.items():
			tracks.append((f"{name} / channel {channel + 1}", notes))
	meters.sort()
	bar = meters[0][1] * 4 / meters[0][2] if meters else 4
	return tracks, dict(
		tempoValues=len(set(t for _, t in tempos)) or 1,
		tempoEvents=len(tempos),
		meters=sorted({f"{n}/{d}" for _, n, d in meters}) or ["4/4"],
		pedalEvents=pedals,
		ppq=file.ticks_per_beat,
		firstBarQuarterBeats=bar,
	)


def features(tracks, meta):
	notes = sorted(n for _, part in tracks for n in part)
	end = max(s + d for s, d, _, _ in notes)
	pitches = [p for _, _, p, _ in notes]
	durations = [d for _, d, _, _ in notes]
	bar = meta["firstBarQuarterBeats"]
	density = [
		sum(a <= s < a + 2 * bar for s, _, _, _ in notes) / (2 * bar)
		for a in np.arange(0, end - 2 * bar + 0.00001, 2 * bar)
	]
	return dict(
		**meta,
		notes=len(notes),
		pitchedTracks=len(tracks),
		quarterBeats=round(end, 2),
		pitchMin=min(pitches),
		pitchMax=max(pitches),
		medianGateBeats=round(float(np.median(durations)), 3),
		durationTypes=len({round(d * 48) for d in durations}),
		velocities=len({v for _, _, _, v in notes}),
		meanPolyphony=round(sum(durations) / end, 2),
		densityCV=round(float(np.std(density) / np.mean(density)), 3)
		if density and np.mean(density)
		else 0,
		trackStats=[track_features(name, part, 2 * bar) for name, part in tracks],
	)


def track_features(name, notes, span):
	windows = defaultdict(list)
	for start, duration, pitch, velocity in sorted(notes):
		windows[int(start / span)].append((round((start % span) * 8), pitch))
	counts = Counter(tuple(window) for window in windows.values())
	return dict(
		name=name,
		notes=len(notes),
		pitchMin=min(n[2] for n in notes),
		pitchMax=max(n[2] for n in notes),
		uniqueTwoBarPatterns=len(counts),
		occupiedTwoBarWindows=len(windows),
		mostRepeatedPattern=max(counts.values()),
		velocities=len({n[3] for n in notes}),
	)


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
		dict(
			tempoValues=1,
			tempoEvents=1,
			meters=[f"{bar}/4"],
			pedalEvents=0,
			firstBarQuarterBeats=bar,
			composerVersion=score["ComposerVersion"],
			seed=settings["Seed"],
		),
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
		if args.download and not path.exists():
			path.write_bytes(urllib.request.urlopen(source["url"], timeout=90).read())
		if hashlib.sha256(path.read_bytes()).hexdigest() != source["sha256"]:
			raise ValueError(f"Reference changed: {path}")
		results[source["name"]] = features(*read_midi(path))
	for path in sorted(args.input.glob("generated-*.json")):
		results[path.stem] = generated(path)
	(args.output / "midi-metrics.json").write_text(
		json.dumps(results, indent="\t") + "\n", encoding="utf-8"
	)
	for name, r in results.items():
		print(
			f"{name:23} notes={r['notes']:5} tracks={r['pitchedTracks']:2} range={r['pitchMin']}-{r['pitchMax']} gate={r['medianGateBeats']:5} tempo-values={r['tempoValues']:3} density-CV={r['densityCV']}"
		)


if __name__ == "__main__":
	main()
