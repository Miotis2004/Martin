# Build state and freshness

Martin writes a build-state document into the build output directory so `martin run --no-build` can decide whether an existing build is safe to execute.

## State contents

The build-state schema version is `1`. The document records:

- Project root and manifest path.
- SHA-256 hash of `Martin.toml`.
- Source-file relative paths, SHA-256 hashes, lengths, and last-write timestamps.
- Compiler version.
- Runtime version and runtime hash.
- Build configuration.
- Target framework.
- Assembly name.
- Entry-point artifact path.
- Produced artifact paths, kinds, hashes, and lengths.

Paths inside the state document are relative to the project or output root where practical and use forward slashes for stable serialization.

## Freshness checks

`martin run --no-build` is accepted only when all freshness checks pass. The output is considered stale when any of these changes or goes missing:

- Build-state file is missing, invalid, or has an unsupported schema.
- Manifest path, manifest contents, or project root changes.
- Source file set changes.
- Any source file contents change.
- Compiler version changes.
- Runtime version or runtime artifact changes.
- Build configuration changes.
- Target framework changes.
- Assembly name changes.
- Entry-point or other recorded artifacts are missing or changed.

When stale output is detected, `martin run --no-build` reports `MRT4805` and returns `ExecutionFailed` instead of running an unknown artifact.
