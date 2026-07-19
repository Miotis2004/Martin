# Getting Started with Martin `0.1.0-alpha`

Martin is an experimental, statically typed language for the .NET ecosystem. This guide is the shortest supported path from a clean checkout to an installed alpha tool and a runnable Martin project.

> [!WARNING]
> Martin `0.1.0-alpha` is pre-1.0 software for evaluation and feedback. Do not rely on alpha syntax, diagnostics, generated C#, runtime APIs, package layout, Studio behavior, or compatibility rules for production workloads.

## 1. Choose a supported setup

| What you want to do | Supported alpha setup |
|---|---|
| Build this repository | Windows 10/11 with the .NET SDK from `global.json` (`10.0.100`) or a compatible latest-feature roll-forward SDK. |
| Create and run Martin projects | The `martin` .NET tool and a supported generated-project target framework (`net8.0`, `net9.0`, or `net10.0`; templates default to `net8.0`). |
| Use the lower-level compiler driver | The `martinc` .NET tool for explicit source-file compilation scenarios. |
| Use Martin Studio | Windows x64 with the alpha MSIX/sideloading path or developer launch from `Martin.Studio.WinUI`. |

WinUI, MSIX, and packaged Studio work additionally require Visual Studio, Windows App SDK tooling, MSIX packaging tools, and a Windows SDK.

## 2. Pack the alpha tools

Until public package publication is explicitly approved, install the tools from locally produced packages. From the repository root:

```powershell
dotnet pack .\Martin\src\Martin.Cli\Martin.Cli.csproj --configuration Release -p:Platform=x64 -o .\artifacts\packages
dotnet pack .\Martin\src\Martin.Compiler.Cli\Martin.Compiler.Cli.csproj --configuration Release -p:Platform=x64 -o .\artifacts\packages
```

## 3. Install into an isolated tool path

The NuGet package IDs are `Martin.Tool` and `Martin.Compiler.Tool`; the command shims are `martin` and `martinc`.

```powershell
$tools = Join-Path $PWD "artifacts\tool-install"
dotnet tool install Martin.Tool --tool-path $tools --add-source .\artifacts\packages
dotnet tool install Martin.Compiler.Tool --tool-path $tools --add-source .\artifacts\packages
& "$tools\martin" --version
& "$tools\martinc" --version
```

For personal alpha use you may install globally from the same explicit package source:

```powershell
dotnet tool install --global Martin.Tool --add-source .\artifacts\packages
dotnet tool install --global Martin.Compiler.Tool --add-source .\artifacts\packages
martin --version
martinc --version
```

## 4. Create, build, run, format, and clean

```powershell
martin new HelloMartin
cd HelloMartin
martin build
martin run --no-build
martin format --check
martin clean
```

The generated project contains a `Martin.toml` manifest and source under `Sources/`. A minimal source file is:

```swift
func main() {
    print("Hello from Martin!")
}
```

A supported executable manifest uses schema `1` and an executable target:

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

## 5. Learn from the sample suite

The `samples/` directory contains standalone projects for the supported alpha feature progression:

- Hello Martin
- Functions
- Structs and classes
- Optionals
- Enums and patterns
- Protocols
- Generics
- Typed errors

Each sample has its own manifest, README, source, and expected output. Validate all samples from the repository root with:

```powershell
.\scripts\validate-samples.ps1
```

or on Unix-like shells:

```bash
./scripts/validate-samples.sh
```

## 6. Know what is supported before filing issues

Start with these user-facing references:

- [Language specification](specification/README.md) for supported syntax and semantics.
- [CLI reference](cli-reference.md) for command behavior and exit codes.
- [Project manifest](project-manifest.md) for manifest schema rules.
- [Diagnostic catalog](diagnostics.md) and [diagnostic formats](diagnostic-formats.md) for errors and JSON output.
- [Tool installation](tool-installation.md) for update, replacement, inspection, and uninstall commands.
- [Known limitations](known-limitations.md) for intentionally unsupported language, project, runtime, platform, Studio, and distribution behavior.
- [Support](../SUPPORT.md) for bug-report and help expectations.

## 7. Remember the alpha boundary

The alpha includes the completed Phase 15 language surface within documented limits: core statements and expressions, functions, nominal types, optionals, enums and patterns, protocols, generics, typed errors, CLI/project workflows, language services, runtime helpers, samples, and Martin Studio. Deferred features such as library output, package dependency resolution, closures, extensions, modules, access control, arrays/dictionaries as a supported collection contract, advanced typed-error conveniences, direct .NET interop, direct IL generation, integrated debugging, Studio multi-project solutions, automatic updates, public package upload, and final release tagging remain outside the supported alpha unless a later document explicitly promotes them.
