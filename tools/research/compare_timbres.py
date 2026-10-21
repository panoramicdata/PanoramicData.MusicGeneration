"""Compare measured orchestral notes with generated and sampled notes, on one scale."""

import argparse
import hashlib
import json
import urllib.request
from pathlib import Path

import matplotlib
import numpy as np
import soundfile as sf
from scipy import signal
from scipy.ndimage import binary_closing

matplotlib.use("Agg")
import matplotlib.pyplot as plt


def segments(x, rate):
	block = rate // 100
	rms = np.array(
		[
			np.sqrt(np.mean(x[i : i + block] ** 2))
			for i in range(0, len(x) - block, block)
		]
	)
	mask = binary_closing(rms > rms.max() * 0.025, iterations=4)
	edges = np.diff(np.r_[False, mask, False].astype(int))
	return [
		(float(a / 100), float(b / 100))
		for a, b in zip(np.where(edges == 1)[0], np.where(edges == -1)[0])
		if b - a > 30
	]


def harmonics(x, rate, midi):
	part = x[int(0.3 * rate) : int(1.3 * rate)]
	f = np.fft.rfftfreq(len(part), 1 / rate)
	p = np.abs(np.fft.rfft(part * np.hanning(len(part)))) ** 2
	expected = 440 * 2 ** ((midi - 69) / 12)
	mask = np.abs(f - expected) < expected * 0.04
	fundamental = f[mask][np.argmax(p[mask])]
	amps = np.array(
		[np.sqrt(p[np.abs(f - h * fundamental) < 18].sum()) for h in range(1, 25)]
	)
	return amps / amps.sum(), float(fundamental)


def metrics(x, rate, midi):
	amps, f0 = harmonics(x, rate, midi)
	f, t, z = signal.stft(x, fs=rate, nperseg=2048, noverlap=1792, boundary=None)
	p = np.abs(z) ** 2
	centroid = (f[:, None] * p).sum(axis=0) / np.maximum(p.sum(axis=0), 1e-20)
	return dict(
		frequencyHz=round(f0, 2),
		centroidHz=round(float(np.median(centroid[(t > 0.3) & (t < 1.3)])), 1),
		harmonicAmplitudes=amps.round(6).tolist(),
	)


def normalise(x):
	return x * (0.1 / np.sqrt(np.mean(x * x)))


def main():
	parser = argparse.ArgumentParser(description=__doc__)
	parser.add_argument("--input", type=Path, default=Path("temp/score-research"))
	parser.add_argument("--output", type=Path, default=Path("docs/research"))
	parser.add_argument("--download", action="store_true")
	args = parser.parse_args()
	args.input.mkdir(exist_ok=True, parents=True)
	args.output.mkdir(exist_ok=True, parents=True)
	sources = json.loads(
		Path(__file__).with_name("instrument-sources.json").read_text(encoding="utf-8")
	)
	results = {}
	for midi, segment in [(72, 0), (74, 2)]:
		fig, axes = plt.subplots(
			3, 4, figsize=(16, 9), layout="constrained", sharex=True, sharey=True
		)
		for row, source in enumerate(sources):
			name = source["name"]
			path = args.input / (name + ".aiff")
			if args.download and not path.exists():
				path.write_bytes(
					urllib.request.urlopen(source["url"], timeout=90).read()
				)
			if hashlib.sha256(path.read_bytes()).hexdigest() != source["sha256"]:
				raise ValueError(f"Reference changed: {path}")
			x, rate = sf.read(path)
			start, end = segments(x, rate)[segment]
			reference = x[int(start * rate) : int((start + 2) * rate)]
			key = f"{name}-{midi}"
			results[key] = {"referenceStart": start, "referenceEnd": end}
			refamps, _ = harmonics(reference, rate, midi)
			for col, kind in enumerate(
				["reference", "baseline", "generated", "soundfont"]
			):
				if kind == "reference":
					wave = reference
				else:
					wave, sample_rate = sf.read(args.input / f"{key}-{kind}.wav")
					assert sample_rate == rate
					wave = wave[: 2 * rate]
				values = metrics(wave, rate, midi)
				values["harmonicL1Distance"] = round(
					float(
						np.abs(np.array(values["harmonicAmplitudes"]) - refamps).sum()
					),
					4,
				)
				results[key][kind] = values
				f, t, z = signal.stft(
					normalise(wave), fs=rate, nperseg=2048, noverlap=1792, boundary=None
				)
				ax = axes[row, col]
				plot = ax.pcolormesh(
					t,
					f / 1000,
					20 * np.log10(np.maximum(np.abs(z), 1e-6)),
					vmin=-90,
					vmax=-20,
					cmap="magma",
					shading="auto",
					rasterized=True,
				)
				ax.set_ylim(0, 8)
				ax.set_xlim(0, 2)
				ax.set_title(f"{name.title()} · {kind}")
				if col == 0:
					ax.set_ylabel("Frequency (kHz)")
				if row == 2:
					ax.set_xlabel("Seconds from detected onset")
			print(
				key,
				[
					(k, results[key][k]["harmonicL1Distance"])
					for k in ["baseline", "generated", "soundfont"]
				],
			)
		fig.suptitle(
			f"{'C5 fit' if midi == 72 else 'D5 held-out pitch'} · each 2 s note RMS = 0.1 · 44.1 kHz · Hann 2048 / hop 256 · dry"
		)
		fig.colorbar(
			plot, ax=axes, label="STFT amplitude (dBFS after RMS matching)", shrink=0.75
		)
		fig.savefig(args.output / f"spectrogram-{midi}.png", dpi=120)
		plt.close(fig)
	(args.output / "timbre-metrics.json").write_text(
		json.dumps(results, indent="\t") + "\n", encoding="utf-8"
	)


if __name__ == "__main__":
	main()
