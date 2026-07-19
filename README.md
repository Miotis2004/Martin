# Martin Programming Language



Martin is an experimental, statically typed programming language for the .NET ecosystem with a compiler, CLI tools, runtime support, language services, project system, and the WinUI-based **Martin Studio** IDE.

> **Martin: Swift clarity. .NET reach.**

## Alpha status

**Current release:** `0.1.0-alpha`  
**Language version:** `0.1`  
**Manifest schema:** `1`  
**Generated project target framework:** `net8.0`

Martin has completed the implementation milestones through Phase 15, including pattern matching, protocols, generics, and typed errors within the documented alpha limits. Phase 16 prepares the first public alpha by tightening documentation, packaging, validation, release notes, and support paths.

> [!WARNING]
> Martin is pre-1.0 alpha software. Syntax, diagnostics, generated C#, package layout, runtime behavior, Studio behavior, and compatibility rules may change between alpha releases. Do not use Martin for production workloads yet.

## Supported platforms and prerequisites

| Surface | Supported alpha environment |
|---|---|
| Repository build | Windows 10/11 with .NET SDK `10.0.100` or a compatible latest-feature roll-forward SDK. WinUI and MSIX projects require Visual Studio, Windows App SDK tooling, MSIX packaging tools, and a Windows SDK. |
| Generated Martin projects | Executable projects targeting supported .NET TFMs (`net8.0`, `net9.0`, or `net10.0`); templates default to `net8.0`. |
| `martin` CLI tool | .NET tool package `Martin.Tool`, command shim `martin`. |
| `martinc` compiler tool | .NET tool package `Martin.Compiler.Tool`, command shim `martinc`; intended for lower-level compiler-driver scenarios. |
| Martin Studio | Windows x64 alpha package or developer launch through `Martin.Studio.WinUI`. |

The repository SDK requirement is separate from generated Martin project output: the repository builds with .NET 10 because `Martin.slnx` requires it, while generated projects may target the supported executable TFMs and default to .NET 8.

## Install the alpha tools

The alpha tools are packaged as .NET tools. For local validation, first create packages from the repository root:

```powershell
dotnet pack .\Martin\src\Martin.Cli\Martin.Cli.csproj --configuration Release -p:Platform=x64 -o .\artifacts\packages
dotnet pack .\Martin\src\Martin.Compiler.Cli\Martin.Compiler.Cli.csproj --configuration Release -p:Platform=x64 -o .\artifacts\packages
```

Install into an isolated tool path:

```powershell
$tools = Join-Path $PWD "artifacts\tool-install"
dotnet tool install Martin.Tool --tool-path $tools --add-source .\artifacts\packages
dotnet tool install Martin.Compiler.Tool --tool-path $tools --add-source .\artifacts\packages
& "$tools\martin" --version
& "$tools\martinc" --version
```

Optional global install for personal alpha use:

```powershell
dotnet tool install --global Martin.Tool --add-source .\artifacts\packages
dotnet tool install --global Martin.Compiler.Tool --add-source .\artifacts\packages
martin --version
martinc --version
```

For update, replacement, package inspection, and uninstall details, see [Tool installation](Docs/tool-installation.md).

## Quick start

Create, build, run, format, and clean a Martin executable project:

```powershell
martin new HelloMartin
cd HelloMartin
martin build
martin run --no-build
martin format --check
martin clean
```

A minimal `Sources/main.martin` looks like this:

```swift
func main() {
    print("Hello from Martin!")
}
```

A Martin project manifest is named `Martin.toml` and must use manifest schema `1` with an executable target:

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
```

See the [getting-started guide](Docs/getting-started.md), [project manifest reference](Docs/project-manifest.md), [CLI reference](Docs/cli-reference.md), and [sample suite](samples/README.md) for complete examples.

## Martin Studio

Martin Studio is the Windows WinUI IDE for Martin. The alpha supports the x64 Windows package path documented in [Studio packaging](Docs/studio-packaging.md). Developers can also launch the IDE from Visual Studio by opening `Martin\Martin.slnx`, selecting `Debug` and `x64`, setting `Martin.Studio.WinUI` as the startup project, and pressing `F5`.

Studio opens single Martin projects, edits `.martin` files with compiler-backed language services, builds, runs, reports diagnostics, preserves settings and session state, and exposes the documented alpha project workflow. Deferred Studio features are listed in [Known limitations](Docs/known-limitations.md).

## Implemented alpha features

| Area | Implemented in `0.1.0-alpha` |
|---|---|
| Front end | Handwritten lexer, parser, syntax tree, recovery-oriented diagnostics, source spans, and syntax formatting support. |
| Semantic analysis | Name binding, scopes, built-in types, type checking, local inference, control-flow checks, and stable diagnostic reporting. |
| Code generation | C# backend for executable projects and .NET build orchestration. |
| CLI and project system | `new`, `build`, `run`, `clean`, `format`, version reporting, manifest loading, source globs, generated build state, and JSON/text diagnostics. |
| Core language | `let`, `var`, primitive literals, arithmetic/comparison/logical operators, functions, labels, returns, `if`/`else`, `while`, local scopes, and `print`. |
| Nominal types | Implemented alpha subsets for structs, classes, stored properties, initializers, methods, and value/reference semantics. |
| Optionals | Optional types, `nil`, optional binding, and optional patterns within documented limits. |
| Enums and patterns | Associated values, recursive enum/optional/Boolean/literal/wildcard/binding patterns, exhaustive `switch`, and pattern diagnostics. |
| Protocols | Explicit conformances, exact requirement matching, witness maps, constraints, and C# interface lowering. |
| Generics | Generic functions and nominal types, constructed type identity, recursive substitution, inference, constraints, emission, formatting, and language-service support. |
| Typed errors | Throwing functions and methods, error enums, `throws(Type)`, `try`, typed `do`/`catch`, exhaustiveness checking, and payload preservation. |
| Language services | Compiler-backed completion, hover, go-to-definition, find references, signature help, semantic classification, diagnostics, and safe formatting. |
| Samples | Standalone samples for Hello Martin, functions, structs/classes, optionals, enums/patterns, protocols, generics, and typed errors. |

## Unsupported or deferred features

| Area | Alpha limitation |
|---|---|
| Project output | `target.kind = "library"` is rejected; only executable projects are supported. |
| Package management | Martin project dependency resolution and package publishing are deferred. |
| Collections | Array and dictionary language/library support is not part of the supported alpha subset unless a future specification chapter promotes it. |
| Advanced typed errors | Error unions, `try?`, `try!`, `rethrows`, and async typed errors are deferred. |
| Language features | Closures, extensions, modules, access control, async/await, direct .NET interoperability, direct IL generation, and debugger integration are deferred. |
| Binary compatibility | No stable ABI, library output contract, or cross-version generated-output compatibility is promised for alpha releases. |
| Studio | Multi-project solutions, integrated debugging, full terminal emulation, source-control integration, plugins, automatic updates, rename, and code actions are deferred. |
| Distribution | Public NuGet upload, GitHub release publication, code signing, Microsoft Store publication, and final release tagging require explicit release approval. |

See [Known limitations](Docs/known-limitations.md) and [Implementation limits](Docs/specification/implementation-limits.md) for the authoritative user-facing limits.

## Documentation entry points

- [Getting started](Docs/getting-started.md)
- [Language specification](Docs/specification/README.md)
- [Diagnostic catalog](Docs/diagnostics.md)
- [Diagnostic formats](Docs/diagnostic-formats.md)
- [CLI reference](Docs/cli-reference.md)
- [Project manifest](Docs/project-manifest.md)
- [Tool installation](Docs/tool-installation.md)
- [Runtime compatibility](Docs/runtime-compatibility.md)
- [Alpha compatibility](Docs/compatibility.md)
- [Sample suite](samples/README.md)
- [Known limitations](Docs/known-limitations.md)
- [Changelog](CHANGELOG.md)
- [0.1.0-alpha release notes](Docs/releases/0.1.0-alpha.md)
- [Support](SUPPORT.md)
- [Security policy](SECURITY.md)
- [Contributing](CONTRIBUTING.md)

## Repository structure

```text
Martin_Programming_Language/
├── Docs/                         # Specification, diagnostics, architecture, compatibility, and release docs
├── Martin/
│   ├── Martin.slnx               # Repository solution
│   ├── src/                      # Compiler, tools, runtime, language services, Studio, and build libraries
│   └── tests/                    # Unit, integration, performance, CLI, runtime, and Studio tests
├── samples/                      # Standalone alpha sample projects
├── scripts/                      # Local validation and packaging scripts
├── Directory.Build.props
├── Directory.Packages.props
├── global.json
└── README.md
```

## Build the repository

From the repository root:

```powershell
dotnet restore .\Martin\Martin.slnx
```

```powershell
dotnet build .\Martin\Martin.slnx --configuration Debug -p:Platform=x64
```

```powershell
dotnet build .\Martin\Martin.slnx --configuration Release -p:Platform=x64
```

```powershell
dotnet test .\Martin\Martin.slnx --configuration Debug -p:Platform=x64
```

Run the CLI from source:

```powershell
dotnet run --project .\Martin\src\Martin.Cli\Martin.Cli.csproj -- --help
```

Run the lower-level compiler CLI from source:

```powershell
dotnet run --project .\Martin\src\Martin.Compiler.Cli\Martin.Compiler.Cli.csproj -- --help
```

Validate alpha samples:

```powershell
.\scripts\validate-samples.ps1
```

```bash
./scripts/validate-samples.sh
```

## Diagnostics

Martin diagnostics use stable codes, severities, source locations, spans, optional related information, and text or JSON output. JSON diagnostics use an envelope:

```json
{
  "diagnostics": [
    {
      "code": "MRT2004",
      "severity": "error",
      "message": "Name 'missing' does not exist.",
      "location": {
        "filePath": "Sources/main.martin",
        "startLine": 4,
        "startColumn": 11,
        "endLine": 4,
        "endColumn": 18
      },
      "relatedLocations": []
    }
  ]
}
```

See [Diagnostic formats](Docs/diagnostic-formats.md) and the [diagnostic catalog](Docs/diagnostics.md).

## Design boundaries

Contributors should keep compiler and project-system libraries independent from Studio UI types, preserve accurate source locations through every compiler phase, report multiple diagnostics where possible, keep generated output deterministic, and update tests and documentation for every user-visible behavior change.

Large language changes, compatibility changes, public packaging changes, and release publication steps require design discussion before implementation. See [Contributing](CONTRIBUTING.md) for the contribution workflow.

## License

Martin is distributed under the [MIT license](LICENSE).

## Acknowledgments

Martin is inspired by Swift's emphasis on clarity, safety, expressive syntax, value semantics, optionals, protocols, and modern language design.

Martin is an independent language and is not affiliated with or endorsed by Apple, Microsoft, the Swift project, or the .NET Foundation.
