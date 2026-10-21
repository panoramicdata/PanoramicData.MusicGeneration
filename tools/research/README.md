# Reproducible MIDI and timbre comparisons

Run from the repository root with .NET 10 and Python 3.12. Python dependencies are for these research tools only, not the NuGets or browser app. Use a virtual environment and install `tools/research/requirements.txt`.

```powershell
python -m venv temp/research-env
temp/research-env/Scripts/python -m pip install -r tools/research/requirements.txt
dotnet run --project tools/research/ResearchSamples.csproj -c Release
temp/research-env/Scripts/python tools/research/compare_scores.py --download
temp/research-env/Scripts/python tools/research/compare_timbres.py --download
```

The .NET exporter writes eight original arrangements plus generated, geometric-baseline and dry GeneralUser C5/D5 WAVs into `temp/score-research`. It needs the demo's existing GeneralUser bank; it does not download it. Pass an output directory after `--` if needed. Python scripts default to that input directory and `docs/research` output. They download only when explicitly passed `--download`; hashes must match the checked-in manifests. Raw reference MIDI and AIFF files stay in ignored `temp`; do not commit them. Public source arrangements are not original session data and their availability does not grant redistribution rights.

The scripts do not interpret MIDI as human performance data. MIDI notes are grouped by track/channel, percussion channel 10 is excluded, note-on velocity zero is a note-off, overlapping equal pitches use FIFO pairing, pedal extensions are ignored. Type-2 files, invalid data bytes and unclosed notes fail rather than being silently repaired. Density windows use the initial meter even if later meter events change. Two-bar onset/pitch fingerprints use 1/8-quarter quantisation and ignore gates/velocities. These limitations are part of the report, not musicality judgements.

To deliberately regenerate numeric profile coefficients and the C# preset source after examining/fetching instrument inputs:

```powershell
temp/research-env/Scripts/python tools/research/fit_profiles.py
```

Review changes to the profile source, rerun the .NET exporter and timbre comparison, then run synthesis tests. `fit_profiles.py` fits C5/E5/G5/B5 from the first, fifth, eighth and twelfth isolated notes in each named reference scale file; it excludes D5. Attack/release/modulation/noise defaults remain documented design parameters. Do not treat repeatedly inspected diagnostic notes as an untouched validation set. New reference files, performers and articulations require a new provenance/evaluation entry.

Generated WAVs can be heard directly, for example `temp/score-research/violin-74-generated.wav`. In the demo use **SoundFonts → Generate an instrument**. The [study](../../docs/SCORE_AND_TIMBRE_STUDY.md) distinguishes implemented features, diagnostic failures and remaining listening work.
