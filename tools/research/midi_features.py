"""Summarise note lists from reference MIDIs and exported arrangements, retaining no notes."""

from collections import Counter, defaultdict

import numpy as np


def features(tracks, meta):
	notes = sorted(n for _, part in tracks for n in part)
	end = max(s + d for s, d, _, _ in notes)
	bar = meta["firstBarQuarterBeats"]
	return {
		**meta,
		**note_metrics(notes, end),
		"notes": len(notes),
		"pitchedTracks": len(tracks),
		"quarterBeats": round(end, 2),
		"densityCV": density_variation(notes, end, bar),
		"trackStats": [track_features(name, part, 2 * bar) for name, part in tracks],
	}


def density_variation(notes, end, bar):
	density = [
		sum(a <= s < a + 2 * bar for s, _, _, _ in notes) / (2 * bar)
		for a in np.arange(0, end - 2 * bar + 0.00001, 2 * bar)
	]
	return (
		round(float(np.std(density) / np.mean(density)), 3)
		if density and np.mean(density)
		else 0
	)


def note_metrics(notes, end):
	pitches = [p for _, _, p, _ in notes]
	durations = [d for _, d, _, _ in notes]
	return {
		"pitchMin": min(pitches),
		"pitchMax": max(pitches),
		"medianGateBeats": round(float(np.median(durations)), 3),
		"durationTypes": len({round(d * 48) for d in durations}),
		"velocities": len({v for _, _, _, v in notes}),
		"meanPolyphony": round(sum(durations) / end, 2),
	}


def track_features(name, notes, span):
	windows = defaultdict(list)
	for start, _, pitch, _ in sorted(notes):
		windows[int(start / span)].append((round((start % span) * 8), pitch))
	counts = Counter(tuple(window) for window in windows.values())
	return {
		"name": name,
		"notes": len(notes),
		"pitchMin": min(n[2] for n in notes),
		"pitchMax": max(n[2] for n in notes),
		"uniqueTwoBarPatterns": len(counts),
		"occupiedTwoBarWindows": len(windows),
		"mostRepeatedPattern": max(counts.values()),
		"velocities": len({n[3] for n in notes}),
	}
