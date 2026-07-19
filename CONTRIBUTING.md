# Contributing to Martin

Thank you for your interest in contributing to Martin. Martin is an experimental programming language for the .NET ecosystem, so stability, clear tests, and documentation are especially important.

## Before you start

- Read `README.md` for project scope and current feature status.
- Check the relevant phase guide under `Docs/` before changing compiler, runtime, Studio, or language behavior.
- Open an issue for large design changes before investing in an implementation.

## Development workflow

1. Create a topic branch from the current development branch.
2. Keep changes focused on one bug fix, feature, or documentation update.
3. Add or update tests for behavior changes.
4. Run the relevant validation commands before opening a pull request.
5. Update documentation when user-visible behavior changes.

Recommended baseline validation:

```bash
dotnet test Martin/Martin.slnx
```

If a platform-specific project cannot run in your environment, call that out in the pull request and include the commands you did run.

## Coding expectations

- Prefer small, readable changes over broad rewrites.
- Preserve existing public behavior unless the change is intentional and documented.
- Keep generated output deterministic where possible.
- Do not commit user-specific IDE state, rejected patches, backup files, build artifacts, package outputs, or local caches.

## Pull requests

Use the pull request template. Include:

- A concise summary of the change.
- The tests or checks you ran.
- Any known limitations, follow-up work, or platform constraints.

By contributing, you agree that your contribution will be licensed under the repository's MIT license.
