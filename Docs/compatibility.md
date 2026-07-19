# Martin Alpha Compatibility

Martin alpha `0.1.0-alpha` is an experimental release for evaluation and feedback. Compatibility is intentionally narrow: supported combinations are the combinations produced by one release build unless this document explicitly says otherwise.

## Version ownership

| Version family | Alpha value | Owner | Rule |
|---|---:|---|---|
| Product and NuGet package | `0.1.0-alpha` | `Martin/Directory.Build.props` | CLI tools, packages, compiler assemblies, runtime assemblies, and release metadata must use the shared product version. |
| Assembly version | `0.1.0.0` | `Martin/Directory.Build.props` | Assemblies use the numeric version derived from the alpha product version. |
| File version | `0.1.0.0` | `Martin/Directory.Build.props` | File-version metadata follows the assembly-version value for alpha. |
| Informational version | `0.1.0-alpha` or `0.1.0-alpha+<commit>` | release build | Local builds may report the base informational version; staged release builds may append commit metadata. |
| Language version | `0.1` | `MartinLanguageVersion.Current` | The compiler supports exactly this built-in alpha language version. Source files cannot select another language version. |
| Manifest schema | `1` | `ManifestParser.SupportedManifestVersion` | Martin project manifests must use schema version `1`. |
| Studio MSIX version | `0.1.0.0` | `Package.appxmanifest`, validated against product version | MSIX requires four numeric components, so `0.1.0-alpha` maps to `0.1.0.0`. |
| Generated target framework | `net8.0` | project template and manifest defaults | Generated projects default to `net8.0`; other frameworks are unsupported unless validation explicitly accepts them. |

## Command-line version output

`martin --version`, `martin -v`, and `martin version` report the Martin CLI, compiler, runtime, language, manifest schema, and target framework versions.

`martinc --version`, `martinc -v`, and `martinc version` report the compiler, language, manifest schema, and target framework versions.

## Runtime compatibility

For `0.1.0-alpha`, the compiler and `Martin.Runtime` must come from the same Martin release line:

- The runtime compatibility major value must match the compiler compatibility major value.
- The runtime compatibility minor value must be in the compiler-supported range.
- Release staging must use the runtime built from the same commit as the compiler.
- Runtime discovery and artifact validation must reject missing, invalid, too-old, too-new, or identity-mismatched runtime assemblies before generated output is published.
- No stable binary compatibility is promised between different alpha minor versions.

## CLI and compiler compatibility

The `Martin.Tool` CLI and `Martin.Compiler.Tool` compiler tool are versioned together. Mixing a `martin` executable from one alpha release with a `martinc`, compiler assembly, build assembly, or runtime assembly from another alpha release is unsupported unless a later compatibility table explicitly permits the combination.

## Studio compatibility

Martin Studio alpha is validated as an x64 Windows package for this release. Studio must use compiler and language-service components from the same product version as the package. Mixing Studio binaries with independently upgraded compiler, CLI, or runtime assemblies is unsupported.

## Manifest compatibility

Manifest schema version `1` is the only supported public schema for `0.1.0-alpha`. Missing, malformed, or unsupported `manifest-version` values fail project loading with a stable diagnostic. Unknown keys retain their documented warning behavior and are not compatibility guarantees for future releases.

## Generated framework compatibility

New projects and generated build outputs default to `net8.0`. The build pipeline may reject unsupported target-framework values. The repository SDK used to build Martin itself is separate from the generated project target framework.

## Alpha upgrade policy

Alpha releases do not promise automatic forward or backward compatibility. Before upgrading between alpha releases:

1. Read the release notes and this compatibility document for the target release.
2. Rebuild projects from source rather than reusing generated output.
3. Reinstall CLI tools from the same package source and version.
4. Reinstall or replace Studio with the package from the same release.
5. Treat diagnostics, manifest behavior, generated C# shape, and runtime binary details as subject to change unless explicitly frozen.
