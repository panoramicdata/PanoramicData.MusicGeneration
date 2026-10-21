"""Fit harmonic amplitudes at C5/E5/G5/B5; D5 remains an untrained interpolation check."""

import json
from pathlib import Path

import numpy as np
import soundfile as sf
from compare_timbres import harmonics, segments
from research_io import load_reference

root = Path("temp/score-research")
sources = json.loads(
	Path(__file__).with_name("instrument-sources.json").read_text(encoding="utf-8")
)
rows = []
for name in ["flute", "violin", "trumpet"]:
	path = root / (name + ".aiff")
	load_reference(path, next(source for source in sources if source["name"] == name))
	x, rate = sf.read(path)
	bounds = segments(x, rate)
	anchors = []
	for index in [0, 4, 7, 11]:
		start, end = bounds[index]
		crop = x[int(start * rate) : int(end * rate)]
		# The helper measures .3–1.3 s: padding places the 30–140 ms attack there.
		short = crop[int(0.03 * rate) : int(0.14 * rate)]
		padded = np.r_[np.zeros(int(0.3 * rate)), short]
		attack, _ = harmonics(padded, rate, 72 + index)
		sustain, f0 = harmonics(crop, rate, 72 + index)
		anchors.append(
			{
				"midi": 72 + index,
				"attack": attack.round(6).tolist(),
				"sustain": sustain.round(6).tolist(),
				"frequencyHz": f0,
				"sourceStart": start,
				"sourceEnd": end,
			}
		)
	rows.append({"name": name, "anchors": anchors})
Path(__file__).with_name("fitted-profiles.json").write_text(
	json.dumps(rows, indent="\t") + "\n", encoding="utf-8"
)

text = """namespace PanoramicData.MusicGeneration;

/// <summary>Experimental Iowa MIS spectral fits. No recorded PCM is included; see docs/SCORE_AND_TIMBRE_STUDY.md.</summary>
public static class OrchestralTimbres
{
"""
for row in rows:
	name = row["name"]
	first = row["anchors"][0]
	text += f"\t/// <summary>{name.title()} spectral anchors C5/E5/G5/B5; pitches outside this range clamp to the nearest spectrum.</summary>\n"
	text += f'\tpublic static OrchestralTimbre {name.title()} {{ get; }} = new()\n\t{{\n\t\tName = "Experimental {name} v1",\n'
	for field, key in [("AttackSpectrum", "attack"), ("SustainSpectrum", "sustain")]:
		text += (
			"\t\t"
			+ field
			+ " = Array.AsReadOnly<double>(["
			+ ", ".join(str(x) for x in first[key])
			+ "]),\n"
		)
	text += "\t\tRegisterSpectra = Array.AsReadOnly<OrchestralRegister>(\n\t\t[\n"
	for anchor in row["anchors"]:
		text += f"\t\t\tnew({anchor['midi']},\n"
		text += (
			"\t\t\t\tArray.AsReadOnly<double>(["
			+ ", ".join(str(x) for x in anchor["attack"])
			+ "]),\n"
		)
		text += (
			"\t\t\t\tArray.AsReadOnly<double>(["
			+ ", ".join(str(x) for x in anchor["sustain"])
			+ "])),\n"
		)
	text += "\t\t]),\n"
	text += (
		"\t\tAttackSeconds = "
		+ str({"flute": 0.18, "violin": 0.37, "trumpet": 0.28}[name])
		+ ",\n"
	)
	text += (
		"\t\tNoiseLevel = "
		+ str({"flute": 0.025, "violin": 0.04, "trumpet": 0.015}[name])
		+ ",\n"
	)
	text += (
		"\t\tVibratoCents = "
		+ str({"flute": 5, "violin": 12, "trumpet": 5}[name])
		+ "\n\t};\n\n"
	)
text += "}\n"
with Path("PanoramicData.MusicGeneration/OrchestralTimbres.cs").open(
	"w", encoding="utf-8", newline="\r\n"
) as output:
	output.write(text)
print("Wrote three profiles; D5 excluded from fitting.")
