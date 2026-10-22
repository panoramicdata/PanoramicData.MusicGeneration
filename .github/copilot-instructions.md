# Copilot Repository Instructions

## Formatting
- Always read and follow the repository `.editorconfig` before editing files, including using tabs over spaces where required.
- Preserve the existing indentation style of each file exactly.
- Use tabs, not spaces, for C#, XML, MSBuild, JSON, and most repository files unless `.editorconfig` says otherwise.
- Use spaces only where `.editorconfig` explicitly requires them, such as YAML.
- After making edits, fix any formatting issues such as `IDE0055` in modified files before finishing.
- Do not reformat unrelated code.

## Validation
- When building to verify changes, build only the specific affected project rather than the full solution.

## Static analysis

- New or modified library methods must satisfy Codacy's limits: at most 8 parameters, cyclomatic complexity 8, and 50 lines of code. Split responsibilities into helpers while preserving random-number draw order and both golden hashes.
- Before merging, inspect Codacy's complete pull-request delta against its target branch. A clean head-commit delta only compares that commit and can miss findings introduced by earlier commits in the same PR.

## Composition design

- Before changing composition behaviour, read `docs/COMPOSITION_THESIS.md` and the canonical `docs/composition-rules.json`. Reference affected rule IDs in the change description.
- Keep the rules contextual: document stylistic exceptions and distinguish implemented behaviour from proposed controls. Update rule status and the catalogue's version/history when its content changes; never reuse a rule ID.
- For changes to musical behaviour, document the composer-version/replay impact and provide an appropriate audible comparison or explain the remaining listening validation. Event tests do not prove aesthetic success.
- Artist/period demonstrations must name their work/recording references and evidence, use original generated examples, and distinguish analysis from measured parameter values. Do not invent artist-wide slider percentages.

## Demo control bindings

- Bind view selectors to named string values, such as `piano` and `spectrogram`. A native select bound to a boolean with lowercase `true`/`false` options produced an empty selection in browser verification. Verify both switching directions and playback continuity in the browser.
