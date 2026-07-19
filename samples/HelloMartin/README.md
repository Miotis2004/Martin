# HelloMartin

This standalone Martin alpha sample demonstrates the executable entry point, string literals, and `print`.

## Files

- `Martin.toml` declares manifest schema `1`, an executable target, and project-local source globs.
- `Sources/main.martin` contains the complete sample program.
- `expected.stdout` records deterministic standard output used by sample validation.

## Build and run

From this directory, or from a copy of this directory outside the repository:

```sh
martin build . --configuration release
martin run . --configuration release
```

During repository validation, use:

```sh
./scripts/validate-samples.sh
```

## Expected output

```text
Hello, Martin alpha!
```

## Known constraints

It intentionally avoids arguments, input, and filesystem APIs. The sample has no external dependencies, performs no network access, and does not rely on repository-relative paths.
