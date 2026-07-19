# Support

Martin is experimental pre-1.0 alpha software. Support is community and maintainer best-effort; there is no guaranteed response time or production support commitment.

## Before asking for help

1. Read the [README](README.md) for prerequisites, installation, quick start, and feature status.
2. Check the [language specification](Docs/specification/README.md) for supported syntax and semantics.
3. Check [Known limitations](Docs/known-limitations.md) to confirm the behavior is not intentionally deferred.
4. Check the [diagnostic catalog](Docs/diagnostics.md) and [diagnostic formats](Docs/diagnostic-formats.md) for reported errors.
5. Compare your project with the [alpha sample suite](samples/README.md).

## Where to get help

- Use GitHub issues for reproducible bugs, documentation problems, sample failures, and packaging problems.
- Use discussions, if enabled, for exploratory usage questions and design questions.
- Do not file public issues for suspected vulnerabilities; follow the [security policy](SECURITY.md).

## What to include in a bug report

Include enough information for another person to reproduce the issue:

- Operating system and version.
- `dotnet --info` output.
- Martin tool versions from `martin --version` and, when relevant, `martinc --version`.
- Whether you installed from local packages, global tools, source, or a Studio package.
- The exact command or Studio action you ran.
- The smallest Martin project or source file that reproduces the issue.
- Expected behavior and actual behavior.
- Full diagnostic output, logs, or screenshots when relevant.

## Prioritization

Maintainers generally prioritize:

1. Security reports and data-loss risks.
2. Regressions in documented alpha behavior.
3. Package installation, build, and sample validation failures.
4. Diagnostics that are missing, misleading, or undocumented.
5. Documentation corrections.
6. New feature requests after design discussion.

## Alpha expectations

Martin `0.1.0-alpha` is intended for experimentation, feedback, and evaluation. Unsupported features should fail clearly or be documented as limitations, but the project does not yet promise source compatibility across future alpha releases.
