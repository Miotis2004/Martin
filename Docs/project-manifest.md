# Martin project manifest

Martin projects are described by a `Martin.toml` file at the project root. The current public manifest schema is version `1`.

## Example

```toml
manifest-version = 1

[package]
name = "HelloMartin"
version = "0.1.0"

[target]
kind = "executable"
framework = "net8.0"
entry = "main"

[sources]
include = ["Sources/**/*.martin"]
exclude = ["bin/**", "obj/**", ".martin/**"]

[build]
output = "bin"
intermediate = "obj"

[tests]
include = ["Tests/**/*.martin"]
```

## Schema

| Key | Required | Type | Default | Notes |
|---|---:|---|---|---|
| `manifest-version` | Yes | integer | none | Must be `1`. |
| `package.name` | Yes | string | none | Must match `^[A-Za-z_][A-Za-z0-9_.-]*$`. |
| `package.version` | Yes | string | none | Semantic-version-like `major.minor.patch`, with optional prerelease/build suffix. |
| `target.kind` | Yes | string | none | Only `executable` is supported. Library output is explicitly unsupported. |
| `target.framework` | Yes | string | none | Supported target frameworks are `net8.0`, `net9.0`, and `net10.0`. The generated alpha templates use `net8.0`. |
| `target.entry` | Yes | string | none | Entry function name; dotted identifiers are accepted. |
| `sources.include` | No | string array | `["Sources/**/*.martin"]` | Project-root-relative source include patterns. |
| `sources.exclude` | No | string array | `["bin/**", "obj/**", ".martin/**"]` | Project-root-relative source exclude patterns. |
| `build.output` | No | string | `bin` | Build output directory, relative to the project root. |
| `build.intermediate` | No | string | `obj` | Intermediate directory, relative to the project root. |
| `tests.include` | No | string array | `["Tests/**/*.martin"]` | Test-file discovery patterns. Test execution is not implemented yet. |

## TOML and validation policy

The parser accepts standard TOML strings, arrays, comments, whitespace, and duplicate-key detection through the TOML parser. Manifest validation is separate from TOML syntax parsing:

1. TOML syntax must parse successfully.
2. Required Martin keys and sections must exist.
3. Values must have the expected type.
4. Paths and glob patterns must be safe and project-root-relative.
5. Unsupported target kinds, unsupported manifest versions, and unsupported frameworks fail validation.

Unknown top-level keys, unknown sections, and unknown keys inside known sections are warnings (`MRT4010`), not fatal errors. Wrong value types are errors (`MRT4009`). Missing required keys or sections are errors (`MRT4003`).

## Glob syntax

Source and test discovery supports these wildcard forms:

| Pattern | Meaning |
|---|---|
| `*` | Any characters in a single path segment. |
| `?` | One character in a single path segment. |
| `**` | Recursive directory matching. |

Examples:

```text
Sources/*.martin
Sources/**/*.martin
Sources/Module?/main.martin
**/*.martin
```

Patterns are evaluated relative to the project root. Absolute patterns and patterns that escape the root are rejected. Include patterns are evaluated first, exclude patterns are applied after inclusion, duplicates are removed, and the final list is sorted deterministically by normalized relative path. Build output, intermediate output, and `.martin` state are excluded by the default manifest template.

## Project discovery

Project-loading commands resolve a manifest in this order:

1. `--manifest-path <path>` when the command accepts it.
2. A project argument that points directly to `Martin.toml`.
3. A project argument that points to a directory containing `Martin.toml`.
4. The current directory and its parents.
5. Failure with `MRT4001` if no manifest can be found.

A project argument and `--manifest-path` are mutually exclusive.
