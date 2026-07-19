# Martin alpha sample suite

The alpha sample suite contains standalone Martin projects for the supported language progression in Phase 16. Each sample can be copied outside the repository and built with the `martin` CLI because it contains its own `Martin.toml`, `Sources/main.martin`, README, and deterministic `expected.stdout` file.

## Samples

| Sample | Demonstrates |
|---|---|
| [HelloMartin](HelloMartin) | Entry point, strings, and `print` |
| [Functions](Functions) | Parameters, labels, returns, variables, arithmetic, and `while` |
| [StructsAndClasses](StructsAndClasses) | Stored properties, construction, initialization, methods, and nominal value/reference types |
| [Optionals](Optionals) | Optional types, `nil`, and `if let` conditional binding |
| [EnumsAndPatterns](EnumsAndPatterns) | Associated values, optional subpatterns, `nil` patterns, and exhaustive `switch` |
| [Protocols](Protocols) | Protocol requirements, explicit conformance, witnesses, and mutating requirements |
| [Generics](Generics) | Generic structs, functions, inference, constraints, and constructed types |
| [TypedErrors](TypedErrors) | Error enums, throwing methods, `try`, exhaustive `do`/`catch`, and payload preservation |

## Validate all samples

From the repository root:

```sh
./scripts/validate-samples.sh
```

On Windows PowerShell:

```powershell
.\scripts\validate-samples.ps1
```

The validation scripts copy every sample into an isolated temporary directory, remove generated output, build with the repository CLI project, run the copied project, and compare standard output with `expected.stdout`.

## Sample contract

Every sample must remain deterministic and self-contained:

- manifest schema `1` in `Martin.toml`;
- source rooted under `Sources/`;
- no absolute paths;
- no network access;
- no repository-internal project references;
- expected output recorded in `expected.stdout`.
