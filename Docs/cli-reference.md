# Martin CLI reference

Martin provides two command-line tools:

- `martin`: project-oriented CLI.
- `martinc`: low-level source-file compiler driver.

Successful command output and child-process stdout are written to stdout. Diagnostics, warnings, build errors, execution errors, child-process stderr, and infrastructure failures are written to stderr. JSON diagnostic mode emits machine-readable diagnostics without decorative text.

JSON diagnostics are emitted as one envelope object on stderr, not as human text and not as a flat array. The envelope shape is `{"diagnostics":[...]}`; each diagnostic contains `code`, `severity`, `message`, an optional nested `location`, an optional `path`, and `relatedLocations`. See [Diagnostic formats](diagnostic-formats.md) for the full contract.

## Exit codes

| Code | Name | Meaning |
|---:|---|---|
| 0 | `Success` | Command completed successfully. |
| 1 | `CompilationFailed` | Martin compilation or format-check failure. |
| 2 | `InvalidArguments` | Parser, option, input-file, or unsupported option error. |
| 3 | `ProjectNotFound` | No usable `Martin.toml` was found. |
| 4 | `ManifestInvalid` | Manifest syntax, schema, path, or clean-safety validation failed. |
| 5 | `BuildFailed` | Generated .NET build or build infrastructure failed. |
| 6 | `ExecutionFailed` | Run startup failed or `--no-build` output is stale. |
| 7 | `TestsUnavailableOrFailed` | `martin test` is unavailable in this language version. |
| 8 | `Cancelled` | Operation was cancelled. |
| 70 | `InternalError` | Unexpected tool failure. |

## `martin`

```text
Usage: martin <command> [options]
```

Global aliases:

- `martin --help`, `martin -h`, or `martin help` show help.
- `martin --version`, `martin -v`, or `martin version` show version information.

### `martin new`

```text
martin new <name> [--path <directory>] [--force] [--no-git] [--framework <tfm>]
```

Creates a project directory named `<name>` under `--path` or the current directory. The name must match `^[A-Za-z_][A-Za-z0-9_.-]*$`. Creation is staged and moved into place only after the generated project validates. Non-empty destinations are rejected; `--force` is accepted by the CLI contract but does not permit destructive replacement of non-empty directories. `--no-git` suppresses `.gitignore` creation.

### `martin build`

```text
martin build [project]
    [--manifest-path <path>]
    [--configuration <debug|release>]
    [--release]
    [--debug]
    [--output <directory>]
    [--keep-generated]
    [--no-app-host]
    [--no-pdb]
    [--diagnostic-format <human|json>]
    [--color <auto|always|never>]
    [--quiet]
    [--verbose]
```

Builds the resolved project. `--release` aliases `--configuration release`; `--debug` aliases `--configuration debug`; using both is invalid. `--output` overrides the manifest output directory. `--keep-generated` keeps generated C# files. `--no-app-host` disables native app-host generation and `--no-pdb` disables portable PDB generation for the build.

### `martin run`

```text
martin run [project]
    [--manifest-path <path>]
    [--configuration <debug|release>]
    [--release]
    [--debug]
    [--no-build]
    [--working-directory <path>]
    [--diagnostic-format <human|json>]
    [--color <auto|always|never>]
    [--quiet]
    [--verbose]
    [-- <program-arguments...>]
```

Builds, then runs the project. Arguments after `--` are forwarded exactly to the child program. `--no-build` skips the build only when the build-state file and artifacts are fresh; otherwise it fails with `MRT4805`. `--working-directory` sets the child process working directory.

### `martin clean`

```text
martin clean [project]
    [--manifest-path <path>]
    [--configuration <debug|release|all>]
    [--dry-run]
    [--verbose]
```

`--configuration debug` removes the Debug output and matching intermediate directories; `--configuration release` removes Release directories; `--configuration all` and the default remove the complete manifest-configured output and intermediate roots. Clean planning rejects deletion of the project root, filesystem roots, paths outside the project root, and paths that cross reparse points. `--dry-run` prints planned directories without deleting them. `--verbose` prints removed or planned paths.

### `martin format`

```text
martin format [files...] [--project <path>] [--manifest-path <path>] [--check] [--stdout]
```

Formats explicit files, or loads the project and formats discovered source files when no files are provided. `--check` returns nonzero when changes would be required. `--stdout` requires exactly one source file and writes formatted text to stdout without modifying the file.

### `martin test`

```text
martin test [project] [--manifest-path <path>]
```

Loads the project, then reports `MRT4901: Martin test execution is not implemented in this language version.` It returns exit code `7` and must not be interpreted as a test run.

### `martin version`

```text
martin version
```

Prints the CLI, compiler, runtime, and target-framework versions.

## `martinc`

```text
martinc <source-files...> [options]
```

Options:

| Option | Meaning |
|---|---|
| `--output <directory>` | Build output directory. Defaults to `bin`. |
| `--name <assembly-name>` | Assembly name. Defaults to `Program`. |
| `--configuration <debug|release>` | Build configuration. Defaults to `debug`. |
| `--target-framework <tfm>` | Target framework. Defaults to `net8.0`. |
| `--keep-generated` | Keep generated C# files. |
| `--emit-csharp` | Alias for `--keep-generated`. |
| `--diagnostic-format <human|json>` | Diagnostic output format. Defaults to `human`. |
| `--color <auto|always|never>` | Color mode for human diagnostics; JSON diagnostics are never colored. |
| `--quiet` | Suppress success output. |
| `--verbose` | Normal verbosity; mutually exclusive with `--quiet`. |
| `-h`, `--help` | Show help. |
| `-v`, `--version` | Show version. |

Input files must exist, must not be directories, must have the `.martin` extension, and must not be supplied more than once.
