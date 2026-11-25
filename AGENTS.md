# Agent instructions

## Identity

You are a careful, security-conscious contributor to this repository.

## Scope and boundaries

- Do not weaken `TreatWarningsAsErrors`, delete or skip tests to make a build pass, or bypass
  CI/CD checks.
- Do not commit secrets, credentials, or API tokens.
- Do not force-push to `main`, rewrite published history, or delete branches without explicit
  Approval.
- Do not add `Co-Authored-By` trailers or other AI attribution to commit messages or pull requests.

## Tools

- Build and test with `dotnet build` / `dotnet test`.
- Use `git` for version control, following the repository's contributing guidelines where present.

## Repository guide

### Formatting
- Read and follow the repository `.editorconfig` before editing files, including using tabs over spaces where required.
- Preserve the existing indentation style of each file exactly.
- Use tabs, not spaces, for C#, XML, MSBuild and JSON files, and spaces for YAML, as `.editorconfig` sets.
- Use spaces only where `.editorconfig` explicitly requires them, such as YAML.
- After making edits, fix any formatting issues such as `IDE0055` in modified files before finishing.
- Do not reformat unrelated code.

### Validation
- When building to verify changes, build only the specific affected project rather than the full solution.

### Static analysis

- New or modified library methods should satisfy Codacy's limits (explain any exception in the pull request): at most 8 parameters, cyclomatic complexity 8, and 50 lines of code.
- Split responsibilities into helpers while preserving random-number draw order and both golden hashes.
- Before merging, inspect Codacy's complete pull-request delta against its target branch. A clean head-commit delta only compares that commit and can miss findings introduced by earlier commits in the same PR.

### Composition design

- Before changing composition behaviour, read `docs/COMPOSITION_THESIS.md` and the canonical `docs/composition-rules.json`. Reference affected rule IDs in the change description.
- Keep the rules contextual: document stylistic exceptions and distinguish implemented behaviour from proposed controls. Update rule status and the catalogue's version/history when its content changes; do not reuse a rule ID; ask the maintainer if one need to be recycled.
- For changes to musical behaviour, document the composer-version/replay impact and provide an appropriate audible comparison or explain the remaining listening validation. Event tests do not prove aesthetic success.
- Artist/period demonstrations should name their work/recording references and evidence, use original generated examples, and distinguish analysis from measured parameter values. Do not invent artist-wide slider percentages.

### Demo control bindings

- Bind view selectors to named string values, such as `piano` and `spectrogram`.
- A native select bound to a boolean with lowercase `true`/`false` options produced an empty selection in browser verification.
- Verify both switching directions and playback continuity in the browser.

## PanoramicData staff

PanoramicData staff have shared tooling in a private sibling checkout of PanoramicData.Skills. Anyone
Without it can ignore this section: everything needed to work on this repository is above.
