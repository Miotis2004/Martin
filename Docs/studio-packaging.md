# Martin Studio Packaging

Martin Studio alpha packaging is limited to a Windows x64 MSIX artifact for `0.1.0-alpha`.
The package identity is stable across alpha upgrades:

- Identity name: `MartinLanguage.MartinStudio`
- Publisher subject: `CN=Martin Programming Language`
- Publisher display name: `Martin Programming Language`
- Product display name: `Martin Studio`
- MSIX version: `0.1.0.0`
- Architecture: `x64`

## Distribution and signing policy

The alpha repository stages an unsigned x64 MSIX package. This keeps private signing material out of the repository and avoids implying that the package is broadly installable before an approved publisher certificate exists.

Release owners may sign the staged package outside the repository with a self-signed or organization certificate whose subject exactly matches `CN=Martin Programming Language`. Do not commit `.pfx`, `.pvk`, private keys, passwords, or machine-specific certificate exports.

If a self-signed sideloading package is produced, publish these operator steps with the release artifact:

1. Create or obtain a code-signing certificate with subject `CN=Martin Programming Language`.
2. Keep the private key in a secure user or organization certificate store.
3. Sign the MSIX after content inspection.
4. Export only the public `.cer` file for sideloading trust.
5. Tell users the certificate expiration date and rotation plan.
6. Instruct users to remove the trusted certificate after uninstalling if they no longer need Martin Studio.

## Build and package commands

Use a Windows developer environment with Visual Studio WinUI/MSIX tooling installed.

```powershell
dotnet restore .\Martin\Martin.slnx
dotnet publish .\Martin\src\Martin.Studio.WinUI\Martin.Studio.WinUI.csproj `
  --configuration Release `
  -p:Platform=x64 `
  -p:RuntimeIdentifier=win-x64 `
  -p:AppxPackageSigningEnabled=false
```

Do not publish x86 or ARM64 artifacts until matching project declarations, publish profiles, package inspection, and smoke validation are added.

## Package inspection

Before signing or distributing, inspect the package contents and manifest.
The artifact must contain the Studio executable, Monaco/editor assets, and required Martin compiler, build, execution, project-system, language-service, and runtime assemblies.
It must not contain test assemblies, local user settings, session state, recent-project data, recovery snapshots, development-only logging configuration, certificates, private keys, or signing passwords.

Recommended checks:

```powershell
MakeAppx.exe unpack /p <path-to-msix> /d <inspection-directory>
Get-ChildItem <inspection-directory> -Recurse | Select-Object FullName
Get-Content <inspection-directory>\AppxManifest.xml
```

Verify that `AppxManifest.xml` keeps the identity name, publisher, display name, version, and x64-only architecture expected for the release.

## Installation and trust

Unsigned MSIX artifacts are for staging validation only. Install a signed MSIX after trusting the publisher certificate:

```powershell
Import-Certificate -FilePath .\MartinStudio.cer -CertStoreLocation Cert:\CurrentUser\TrustedPeople
Add-AppxPackage .\MartinStudio.msix
```

If the release is distributed as an unsigned staging artifact, do not ask users to install it directly. Use it only for release-owner inspection and signing.

## Upgrade behavior and user data

MSIX upgrades rely on the stable package identity `MartinLanguage.MartinStudio` and publisher subject `CN=Martin Programming Language`. Settings, recent projects, session state, logs, and recovery snapshots are stored as user data and are expected to survive package upgrades that keep this identity.

Uninstall removes the app package but may leave user data and any manually trusted certificate. To fully clean up after testing, uninstall Martin Studio from Windows Settings, remove Martin Studio user data from the package local-app-data location, and remove the trusted `CN=Martin Programming Language` certificate if it was installed only for Martin Studio.

## Studio smoke workflow

Run this workflow on a supported Windows x64 machine before publishing an alpha package:

1. Install the signed x64 MSIX through the documented sideloading path.
2. Launch Martin Studio and confirm it opens as `Martin Studio` and reports version `0.1.0-alpha` / MSIX `0.1.0.0`.
3. Open a folder or `Martin.toml` project.
4. Edit a Martin source file without saving and confirm diagnostics update.
5. Build the project from Studio.
6. Run the project from Studio and verify program output.
7. Change settings, close Studio, reopen it, and confirm settings persist.
8. Reopen the project and confirm session restoration works.
9. Uninstall Martin Studio.
10. Reinstall or upgrade with the same identity and confirm user data behavior matches the release notes.
