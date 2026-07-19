# Release Staging and Integrity Checks

Martin alpha release candidates are staged locally with `scripts/package-alpha.sh` on Unix-like systems or `scripts/package-alpha.ps1` on Windows/PowerShell. The scripts create `artifacts/release/0.1.0-alpha/` and never publish packages, tags, GitHub releases, or Studio installers externally.

## Command

```bash
./scripts/package-alpha.sh --version 0.1.0-alpha --configuration Release --platform x64
```

```powershell
./scripts/package-alpha.ps1 -Version 0.1.0-alpha -Configuration Release -Platform x64
```

## Gates performed during staging

The staging command fails if tracked source files differ from `HEAD`, excluding release output under `artifacts/`. It restores and builds the solution in Release mode unless `--skip-build` / `-SkipBuild` is supplied for a prebuilt clean output tree.

The script packs:

- `Martin.Tool`
- `Martin.Compiler.Tool`
- `Martin.Runtime`

It also copies alpha release notes, public documentation, the normative specification, diagnostic documentation, compatibility guidance, tool installation instructions, runtime/library policy, and Studio packaging guidance into the staged documentation folder.

## Integrity outputs

Every staged file is hashed with SHA-256. The checksum list is written to `checksums/SHA256SUMS`, and the machine-readable release manifest is written to `validation/manifest.json`.

The manifest records product version, expected tag, source commit, repository SDK, generated target framework, manifest schema version, configuration, platform, the staging command, and one entry per artifact containing filename, kind, byte size, SHA-256 hash, and producing command.

NuGet packages are inspected as ZIP files during staging. Their entry lists are written under `validation/inspection/`, and staging fails if package contents include common development residue such as `.git`, `bin`, `obj`, `.vs`, temporary files, rejected patches, or build caches.

## Studio artifacts

On Windows, run the Studio packaging workflow before staging if MSIX artifacts should be included. The staging scripts copy existing `.msix`, `.msixbundle`, `.appinstaller`, and `.cer` files from the audited WinUI package output location into `studio/`. If no Studio package is present, `studio/README.txt` records the required Windows follow-up instead of publishing or fabricating an installer.
