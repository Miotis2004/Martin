# Alpha Release-Candidate Validation

Milestone 17 freezes one reviewed `0.1.0-alpha` candidate commit and records whether the staged artifact set is approved, pending, or blocked. Candidate validation is local evidence capture only: it does not tag, publish NuGet packages, upload release assets, sign artifacts, or create a GitHub release.

## Inputs

Before running the candidate review, create the candidate artifacts and release-validation summary:

```bash
./scripts/validate-release.sh --version 0.1.0-alpha --configuration Release --platform x64
```

```powershell
.\scripts\validate-release.ps1 -Version 0.1.0-alpha -Configuration Release -Platform x64 -IncludeStudio
```

The review expects:

- `artifacts/release/0.1.0-alpha/validation/manifest.json`
- `artifacts/release/0.1.0-alpha/checksums/SHA256SUMS`
- A local release-validation `summary.json` from `artifacts/release-validation/0.1.0-alpha/<run>/summary.json`
- A Windows Studio validation summary when final approval requires Studio evidence

## Candidate review command

Unix-like systems:

```bash
./scripts/validate-alpha-candidate.sh \
  --version 0.1.0-alpha \
  --validation-summary artifacts/release-validation/0.1.0-alpha/<run>/summary.json \
  --studio-summary artifacts/release-validation/0.1.0-alpha/<windows-run>/summary.json \
  --require-studio \
  --approval pending
```

PowerShell:

```powershell
.\scripts\validate-alpha-candidate.ps1 `
  -Version 0.1.0-alpha `
  -ValidationSummary artifacts/release-validation/0.1.0-alpha/<run>/summary.json `
  -StudioSummary artifacts/release-validation/0.1.0-alpha/<windows-run>/summary.json `
  -RequireStudio `
  -Approval pending
```

The command writes `artifacts/release-candidate/0.1.0-alpha/<full-sha>/review.json`.

## Automated checks

The candidate review fails if:

- The candidate commit is not resolved to a full 40-character SHA.
- Tracked source files differ from `HEAD`, excluding ignored release evidence under `artifacts/`.
- The release manifest or checksum file is missing.
- The manifest version, tag plan, or commit does not match the candidate.
- Any artifact hash in `SHA256SUMS` no longer matches the staged file.
- Manifest artifacts are not listed in the checksum file.
- Supplied validation summaries do not refer to the candidate commit or did not pass.
- `--require-studio` is used without passed Windows Studio evidence.
- `--approval approved` is requested while blockers remain.
- `--approval blocked` is requested without at least one `--blocker` entry.

## Recording approval or blockers

Use `--approval pending` for a review that has not yet received human publication approval. Use `--approval blocked --blocker "<reason>"` to record a known blocker. Use `--approval approved` only after all checks pass and the release owner explicitly approves publication.

Even when the candidate is approved, Milestone 18 remains a separate external-state-changing step. The `v0.1.0-alpha` tag, GitHub prerelease, NuGet publication, public asset upload, and certificate use still require explicit publication approval.
