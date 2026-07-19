# Alpha Publication Runbook

Milestone 18 publishes one previously approved `0.1.0-alpha` release candidate. Publication changes external state, so this runbook and the helper scripts default to evidence generation only. Do not create the public tag, GitHub prerelease, asset uploads, or NuGet pushes until the release owner has explicitly approved publication and the approved candidate review is checked in or archived with the release evidence.

## Required inputs

Publication starts from the immutable candidate produced by Milestone 17:

- `artifacts/release-candidate/0.1.0-alpha/<commit>/review.json` with `approval` set to `approved` and `status` set to `passed`.
- `artifacts/release/0.1.0-alpha/validation/manifest.json` whose `commit` matches the approved candidate and whose `tag` is `v0.1.0-alpha`.
- `artifacts/release/0.1.0-alpha/checksums/SHA256SUMS` matching every staged release artifact.
- Release notes at `Docs/releases/0.1.0-alpha.md`.
- NuGet API credentials and GitHub release permissions held by the human publisher, not stored in the repository.

## Pre-publication verification

Generate the publication checklist before changing public state:

```bash
./scripts/publish-alpha.sh --review artifacts/release-candidate/0.1.0-alpha/<commit>/review.json
```

```powershell
.\scripts\publish-alpha.ps1 -Review artifacts/release-candidate/0.1.0-alpha/<commit>/review.json
```

The command writes `artifacts/publication/0.1.0-alpha/<commit>/publication-plan.json`. It fails if the worktree has tracked changes outside `artifacts/`, the review is not approved, the candidate commit is not `HEAD`, the manifest/checksum files are missing, a staged artifact hash changed, or a local tag named `v0.1.0-alpha` already points somewhere else.

## Publication order

After the pre-publication verification passes and approval is explicit, publish in this order:

1. Create the annotated tag at the approved commit:

   ```bash
   git tag -a v0.1.0-alpha <commit> -m "Martin 0.1.0-alpha"
   git push origin v0.1.0-alpha
   ```

2. Create a GitHub prerelease named `Martin 0.1.0-alpha` from tag `v0.1.0-alpha` using `Docs/releases/0.1.0-alpha.md` as the release body.
3. Upload every staged artifact listed in `artifacts/release/0.1.0-alpha/validation/manifest.json`, including `checksums/SHA256SUMS`.
4. Publish the approved NuGet packages from `artifacts/release/0.1.0-alpha/packages/`:

   ```bash
   dotnet nuget push artifacts/release/0.1.0-alpha/packages/Martin.Tool.0.1.0-alpha.nupkg --source https://api.nuget.org/v3/index.json --api-key <NUGET_API_KEY>
   dotnet nuget push artifacts/release/0.1.0-alpha/packages/Martin.Compiler.Tool.0.1.0-alpha.nupkg --source https://api.nuget.org/v3/index.json --api-key <NUGET_API_KEY>
   dotnet nuget push artifacts/release/0.1.0-alpha/packages/Martin.Runtime.0.1.0-alpha.nupkg --source https://api.nuget.org/v3/index.json --api-key <NUGET_API_KEY>
   ```

5. Verify public installation from NuGet and public artifact downloads from GitHub. Use a fresh tool path and a clean source checkout so cached local packages cannot mask a publication mistake.
6. Record publication results, URLs, package versions, download verification, and any exception in `artifacts/publication/0.1.0-alpha/<commit>/publication-results.json` and summarize exceptions in the GitHub release notes if they affect users.

## Publication exceptions

If a package upload, tag push, or asset upload fails after any public state changes, stop publication, preserve logs, and document the exact public state that exists. Do not retag a different commit with `v0.1.0-alpha`. If the approved candidate cannot be published safely, create a new candidate through Milestone 17 and use a new prerelease identifier.
