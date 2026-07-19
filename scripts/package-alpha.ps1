param(
    [string]$Version = "0.1.0-alpha",
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [switch]$SkipBuild,
    [switch]$VerboseOutput
)

$ErrorActionPreference = "Stop"

function Resolve-RepositoryRoot {
    $directory = $PSScriptRoot
    while ($directory) {
        if (Test-Path (Join-Path $directory "Martin/Martin.slnx")) { return (Resolve-Path $directory).Path }
        $parent = Split-Path -Parent $directory
        if ($parent -eq $directory) { break }
        $directory = $parent
    }
    throw "Unable to resolve repository root from '$PSScriptRoot'."
}

function Invoke-LoggedCommand {
    param([Parameter(Mandatory=$true)][string]$FilePath, [Parameter(ValueFromRemainingArguments=$true)][string[]]$Arguments)
    if ($VerboseOutput) { Write-Host "> $FilePath $($Arguments -join ' ')" }
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Command failed with exit code $LASTEXITCODE: $FilePath $($Arguments -join ' ')" }
}

$repoRoot = Resolve-RepositoryRoot
Push-Location $repoRoot
try {
    $dirty = & git diff --name-only -- . ':!artifacts'
    if ($dirty) { throw "Tracked source changes are present. Commit or stash changes before staging a release." }
    $commit = (& git rev-parse HEAD).Trim()
    $tag = "v$Version"
    $sdk = (Get-Content global.json -Raw | ConvertFrom-Json).sdk.version
    $schemaMatch = Select-String -Path "Martin/src/**/*.cs" -Pattern "SupportedManifestVersion\s*=\s*([0-9]+)" | Select-Object -First 1
    $manifestSchema = if ($schemaMatch) { [int]$schemaMatch.Matches[0].Groups[1].Value } else { 1 }

    $releaseRoot = Join-Path $repoRoot "artifacts/release/$Version"
    $packagesDir = Join-Path $releaseRoot "packages"
    $studioDir = Join-Path $releaseRoot "studio"
    $docsDir = Join-Path $releaseRoot "docs"
    $checksumsDir = Join-Path $releaseRoot "checksums"
    $validationDir = Join-Path $releaseRoot "validation"
    $inspectionDir = Join-Path $validationDir "inspection"
    Remove-Item -Recurse -Force $releaseRoot -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $packagesDir,$studioDir,$docsDir,$checksumsDir,$inspectionDir | Out-Null

    if (-not $SkipBuild) {
        Invoke-LoggedCommand dotnet restore "Martin/Martin.slnx"
        Invoke-LoggedCommand dotnet build "Martin/Martin.slnx" --configuration $Configuration --no-restore -p:Platform=$Platform
    }

    $projects = @("Martin/src/Martin.Cli/Martin.Cli.csproj", "Martin/src/Martin.Compiler.Cli/Martin.Compiler.Cli.csproj", "Martin/src/Martin.Runtime/Martin.Runtime.csproj")
    foreach ($project in $projects) { Invoke-LoggedCommand dotnet pack $project --configuration $Configuration --no-build --output $packagesDir -p:Platform=$Platform }

    Copy-Item "Docs/releases/$Version.md" (Join-Path $releaseRoot "release-notes.md")
    Copy-Item README.md,CHANGELOG.md,LICENSE,SECURITY.md,CONTRIBUTING.md $docsDir
    Copy-Item -Recurse "Docs/specification" (Join-Path $docsDir "specification")
    Copy-Item Docs/diagnostics.md,Docs/compatibility.md,Docs/known-limitations.md,Docs/tool-installation.md,Docs/runtime-package-and-library-policy.md,Docs/studio-packaging.md $docsDir

    $studioPublish = "Martin/src/Martin.Studio.WinUI/bin/$Platform/$Configuration/net8.0-windows10.0.19041.0/win-$Platform/AppPackages"
    if (Test-Path $studioPublish) {
        Get-ChildItem $studioPublish -Recurse -File -Include *.msix,*.msixbundle,*.appinstaller,*.cer | Copy-Item -Destination $studioDir
    }
    Set-Content (Join-Path $studioDir "README.txt") "Martin Studio artifacts are staged here when a Windows MSIX publish has already produced packages.`nRun the Windows release validation path to create and inspect the signed or sideloadable Studio package."

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    foreach ($package in Get-ChildItem $packagesDir -Filter *.nupkg) {
        $archive = [System.IO.Compression.ZipFile]::OpenRead($package.FullName)
        try {
            $entries = foreach ($entry in $archive.Entries | Sort-Object FullName) {
                if ($entry.FullName -match '(^|/)(\.git|bin|obj|TestResults|artifacts|node_modules|\.vs|.*\.(user|suo|orig|rej|tmp|temp|bak|cache))(/|$)') { throw "Package contains development residue: $($package.FullName) entry $($entry.FullName)" }
                [pscustomobject]@{ name=$entry.FullName; size=$entry.Length; compressedSize=$entry.CompressedLength }
            }
            $entries | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $inspectionDir "$($package.Name).contents.json")
        } finally { $archive.Dispose() }
    }

    $shaFile = Join-Path $checksumsDir "SHA256SUMS"
    Set-Content $shaFile ""
    Get-ChildItem $releaseRoot -Recurse -File | Sort-Object FullName | ForEach-Object {
        $rel = [IO.Path]::GetRelativePath($releaseRoot, $_.FullName).Replace('\\','/')
        if ($rel -ne "checksums/SHA256SUMS") {
            $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            Add-Content $shaFile "$hash  $rel"
        }
    }

    $artifacts = Get-ChildItem $releaseRoot -Recurse -File | Sort-Object FullName | Where-Object { [IO.Path]::GetRelativePath($releaseRoot, $_.FullName).Replace('\\','/') -ne "validation/manifest.json" } | ForEach-Object {
        $rel = [IO.Path]::GetRelativePath($releaseRoot, $_.FullName).Replace('\\','/')
        [pscustomobject]@{ filename=$rel; kind=($rel -split '/')[0]; size=$_.Length; sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(); producingCommand=$(if ($_.Extension -eq '.nupkg') { 'dotnet pack' } elseif ($rel -eq 'checksums/SHA256SUMS') { 'Get-FileHash' } elseif (($rel -split '/')[0] -eq 'studio') { 'Studio publish staging' } else { 'release staging' }) }
    }
    [pscustomobject]@{ productVersion=$Version; tag=$tag; commit=$commit; repositorySdk=$sdk; generatedFramework='net8.0'; manifestSchema=$manifestSchema; configuration=$Configuration; platform=$Platform; releaseCommand='scripts/package-alpha.ps1'; artifacts=$artifacts } | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $validationDir "manifest.json")

    Write-Host "Release candidate staged at $releaseRoot"
    Write-Host "Manifest: $(Join-Path $validationDir 'manifest.json')"
    Write-Host "Checksums: $shaFile"
}
finally { Pop-Location }
