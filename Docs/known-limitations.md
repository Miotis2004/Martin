# Martin Alpha Known Limitations

**Product version:** `0.1.0-alpha`  
**Language version:** `0.1`  
**Audience:** Users evaluating the public alpha

Martin is pre-1.0 software. This document is the user-facing summary of unsupported behavior, deferred features, and compatibility boundaries for the alpha release. The formal specification remains authoritative for supported language behavior.

## Unsupported syntax and language features

The following features are not part of the supported alpha language:

- Closures and lambda expressions.
- Extensions and retroactive member declarations.
- Modules and explicit access control.
- Async/await and concurrency syntax.
- Direct .NET interoperability syntax.
- Direct IL generation controls.
- Advanced typed-error conveniences: error unions, `try?`, `try!`, `rethrows`, and async typed errors.
- Collection literals and collection standard-library guarantees for arrays and dictionaries.

## Unsupported semantic features

The alpha does not promise:

- Stable binary compatibility between generated outputs from different Martin versions.
- A public ABI or library-output contract.
- Complete Swift compatibility.
- Production-grade optimizer behavior.
- Stable generated C# shape beyond the documented build and runtime contract.
- Cross-version mixing of CLI, compiler, runtime, language-service, or Studio binaries.

## Project-system limitations

- `target.kind = "executable"` is the only supported public output kind.
- `target.kind = "library"` is intentionally rejected until public visibility, interop, packaging, and binary compatibility rules are designed.
- Manifest schema version `1` is the only supported public schema.
- Generated projects default to `net8.0`; other target frameworks are unsupported unless a later compatibility document explicitly allows them.
- Martin package dependency resolution is deferred.
- `martin test` intentionally reports that Martin tests are unavailable instead of running tests.

## Runtime and standard-library boundary

- `Martin.Runtime` must come from the same alpha release line as the compiler and tools.
- Runtime package layout and helper APIs may change between alpha releases.
- The alpha runtime is not a general-purpose stable standard library.
- Direct use of runtime internals by user projects is unsupported unless a public API is documented.

## Platform limitations

- Repository builds require the .NET SDK specified by `global.json` or a compatible latest-feature roll-forward SDK.
- WinUI and MSIX work require Windows, Visual Studio workloads, Windows App SDK tooling, MSIX packaging tools, and a Windows SDK.
- Martin Studio alpha validation is scoped to Windows x64.
- Hosted GitHub Actions may be manually triggered when capacity is available, but local validation is the required alpha gate.

## Martin Studio limitations

Martin Studio is an alpha single-project IDE. Deferred features include:

- Multi-project solutions.
- Integrated debugger support.
- Full terminal emulation.
- Source-control integration.
- Plugins.
- Automatic updater.
- Microsoft Store distribution.
- Rename and code-action workflows.

## Performance limitations

- Language services are bounded and cancellable, but large-project performance is still characterized as alpha quality.
- Compiler and formatter performance limits are documented per phase and may change as the implementation is hardened.
- No production service-level objective is promised.

## Distribution limitations

- Publishing NuGet packages, uploading a GitHub release, signing with a real certificate, creating the final public tag, or submitting to the Microsoft Store requires explicit release approval.
- Local packages produced during validation are candidate artifacts, not public release artifacts.
- Checksums and artifact manifests are required for staged release artifacts before publication.

## Compatibility warning

Alpha releases may change syntax, diagnostics, manifests, generated C#, runtime behavior, package contents, and Studio behavior. Before upgrading, read the release notes and rebuild Martin projects from source rather than reusing generated output.
