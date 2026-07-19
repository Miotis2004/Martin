# Runtime Package and Compiler Library Policy

This document records the Phase 16 alpha packaging policy for `Martin.Runtime` and compiler implementation libraries.

## Public alpha packages

The intentional public package set for `0.1.0-alpha` is:

| Package ID | Purpose | Policy |
|---|---|---|
| `Martin.Tool` | Project-oriented `martin` .NET tool | Public alpha tool package. |
| `Martin.Compiler.Tool` | File-oriented `martinc` .NET tool | Public alpha advanced compiler-driver tool package. |
| `Martin.Runtime` | Runtime support for generated Martin applications | Public alpha runtime dependency selected by the Martin toolchain. |

No other compiler, parser, semantic, code-generation, build, project-system, execution, language-service, or Studio library package is a supported public NuGet API for `0.1.0-alpha`.

## Martin.Runtime package policy

`Martin.Runtime` is packable because generated Martin applications depend on its runtime helpers and version identity. The package must inherit the repository product version from `Martin/Directory.Build.props`, target `net8.0`, include the repository license, include a package README, and generate XML documentation for supported public runtime entry points.

The package is a runtime support dependency for generated applications, not a stable general-purpose hosting SDK. Consumers should use the runtime selected, copied, and validated by the matching Martin toolchain. Direct references are acceptable only for inspecting generated output or local experiments.

## Supported runtime API surface

For `0.1.0-alpha`, the supported runtime surface is limited to the types generated code currently emits or the build pipeline validates:

- `Martin.Runtime.MartinConsole` implements console built-ins: argument setup, argument lookup, `readLine`, standard output printing, standard error writing, and process exit.
- `Martin.Runtime.MartinFileSystem`, `MartinFileReadResult`, `MartinFileErrorDescriptor`, and `MartinFileErrorKind` implement the checked `readFile` boundary used by generated code.
- `Martin.Runtime.Optional<T>` represents generated optional values.
- `Martin.Runtime.MartinThrownError` and `MartinThrownErrorValue` carry Martin typed errors through CLR exception boundaries.
- `Martin.Runtime.MartinRuntimeInfo` exposes runtime version and compatibility metadata used by build validation.

The runtime package must not contain compiler implementation assemblies, test assemblies, Studio assets, or temporary release artifacts.

## Compiler library policy

Compiler implementation libraries remain private for the alpha. `Martin.Compiler`, `Martin.CodeGeneration`, and `Martin.LanguageServices` are explicitly marked non-packable so accidental `dotnet pack` runs do not create supported library packages. These assemblies may still be included transitively inside tool packages or application outputs where required by the tools, but that inclusion does not create a public API compatibility promise.

Any future decision to publish compiler libraries must identify package IDs, supported APIs, XML documentation, compatibility guarantees, upgrade rules, and a deprecation policy before publication.

## Compatibility and runtime selection

The first alpha requires compiler and runtime packages with matching major and minor product versions. Generated applications reference the validated `Martin.Runtime.dll` staged by the build pipeline through an explicit assembly reference and hint path. The build pipeline rejects missing runtime assemblies, invalid runtime identity, missing compatibility metadata, and runtime compatibility ranges outside the compiler-supported range before publishing generated output.
