# Tool installation

`martin` and `martinc` are packaged as .NET tools for the `0.1.0-alpha` release. The package IDs are intentionally different from the command shim names:

| Command | Package ID |
| --- | --- |
| `martin` | `Martin.Tool` |
| `martinc` | `Martin.Compiler.Tool` |

Use an isolated `--tool-path` for release validation so smoke tests do not modify a developer's global tool state. Global installation is acceptable for personal alpha use after validating the package source.

## Pack

From the repository root:

```powershell
dotnet pack .\Martin\src\Martin.Cli\Martin.Cli.csproj --configuration Release -p:Platform=x64 -o .\artifacts\packages
dotnet pack .\Martin\src\Martin.Compiler.Cli\Martin.Compiler.Cli.csproj --configuration Release -p:Platform=x64 -o .\artifacts\packages
```

## Install from a local package source

Isolated validation install:

```powershell
$tools = Join-Path $PWD "artifacts\tool-install"
dotnet tool install Martin.Tool --tool-path $tools --add-source .\artifacts\packages
dotnet tool install Martin.Compiler.Tool --tool-path $tools --add-source .\artifacts\packages
& "$tools\martin" --version
& "$tools\martinc" --version
```

Optional global alpha install:

```powershell
dotnet tool install --global Martin.Tool --add-source .\artifacts\packages
dotnet tool install --global Martin.Compiler.Tool --add-source .\artifacts\packages
martin --version
martinc --version
```

The expected command shims are `martin` and `martinc`. Both commands must report `0.1.0-alpha` for the product surfaces they own before a release candidate is accepted.

## Smoke test

Run the project workflow outside the repository:

```powershell
mkdir $env:TEMP\martin-smoke
cd $env:TEMP\martin-smoke
martin new HelloMartin
cd HelloMartin
martin build
martin run --no-build
martin format --check
martin clean
```

Run the compiler-driver workflow against an explicit source file:

```powershell
'func main() { print("hello from martinc") }' | Set-Content .\main.martin -NoNewline
martinc .\main.martin --output .\out --name HelloMartinc --quiet
```

The repository smoke scripts automate pack, package-content inspection, isolated install, version checks, project create/build/run/format/clean, direct `martinc` compilation, and isolated uninstall:

```powershell
.\scripts\smoke-test-tools.ps1 -VerboseOutput
```

```bash
./scripts/smoke-test-tools.sh --verbose-output
```

## Inspect package contents

Before installing from a candidate source, inspect the generated `.nupkg` files. Package contents must not include repository build caches or development-only temporary files such as `.git`, `.vs`, `bin`, `obj`, `TestResults`, `artifacts`, `node_modules`, `*.user`, `*.suo`, `*.orig`, `*.rej`, `*.tmp`, `*.temp`, `*.bak`, or `*.cache` entries.

## Update behavior

Alpha packages are updated only from an explicit package source. To replace an installed local build with another package of the same version, uninstall first and then install from the new source. To move to a later version, use `dotnet tool update` with the same package IDs and an explicit `--add-source` path.

Isolated same-version replacement:

```powershell
dotnet tool uninstall Martin.Tool --tool-path $tools
dotnet tool uninstall Martin.Compiler.Tool --tool-path $tools
dotnet tool install Martin.Tool --tool-path $tools --add-source .\artifacts\packages
dotnet tool install Martin.Compiler.Tool --tool-path $tools --add-source .\artifacts\packages
```

Later-version update:

```powershell
dotnet tool update Martin.Tool --tool-path $tools --add-source .\artifacts\packages
dotnet tool update Martin.Compiler.Tool --tool-path $tools --add-source .\artifacts\packages
```

## Uninstall

Isolated validation uninstall:

```powershell
dotnet tool uninstall Martin.Tool --tool-path $tools
dotnet tool uninstall Martin.Compiler.Tool --tool-path $tools
```

Global alpha uninstall:

```powershell
dotnet tool uninstall --global Martin.Tool
dotnet tool uninstall --global Martin.Compiler.Tool
```
