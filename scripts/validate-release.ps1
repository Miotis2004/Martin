param(
    [string]$Version = "0.1.0-alpha",
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [switch]$IncludeStudio,
    [switch]$SkipClean,
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

$repoRoot = Resolve-RepositoryRoot
Set-Location $repoRoot
$runId = [DateTimeOffset]::UtcNow.ToString("yyyyMMddHHmmss")
$evidenceRoot = Join-Path $repoRoot "artifacts/release-validation/$Version/$runId"
$logsDir = Join-Path $evidenceRoot "logs"
$outcomesPath = Join-Path $evidenceRoot "outcomes.jsonl"
$summaryPath = Join-Path $evidenceRoot "summary.json"
New-Item -ItemType Directory -Force -Path $logsDir | Out-Null

function Add-Outcome([string]$Gate, [string]$Status, [string]$Log, [string]$Message = "") {
    $relativeLog = [IO.Path]::GetRelativePath($repoRoot, $Log).Replace('\\','/')
    [pscustomobject]@{ gate=$Gate; status=$Status; log=$relativeLog; message=$Message } | ConvertTo-Json -Compress | Add-Content $outcomesPath
}
function Write-Summary([string]$Status, [string]$FailedGate = "") {
    $items = @()
    if (Test-Path $outcomesPath) { $items = Get-Content $outcomesPath | Where-Object { $_ } | ForEach-Object { $_ | ConvertFrom-Json } }
    $summary = [ordered]@{ status=$Status; version=$Version; configuration=$Configuration; platform=$Platform; dateUtc=[DateTimeOffset]::UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"); commit=(& git rev-parse HEAD).Trim(); repository=$(try { (& git config --get remote.origin.url).Trim() } catch { 'unknown' }); evidenceRoot=[IO.Path]::GetRelativePath($repoRoot, $evidenceRoot).Replace('\\','/'); outcomes=$items }
    if ($FailedGate) { $summary.failedGate = $FailedGate }
    $summary | ConvertTo-Json -Depth 8 | Set-Content $summaryPath
}
function Invoke-Gate([string]$Gate, [scriptblock]$Command) {
    $log = Join-Path $logsDir (($Gate -replace '[^A-Za-z0-9_.-]', '_') + ".log")
    Write-Host "==> $Gate"
    try {
        & $Command *> $log
        Add-Outcome $Gate "passed" $log
    } catch {
        Add-Outcome $Gate "failed" $log $_.Exception.Message
        Write-Summary "failed" $Gate
        Write-Error "Release validation failed at gate: $Gate. Log: $log"
        exit 1
    }
}
function Invoke-Native([string]$FilePath, [string[]]$Arguments) {
    if ($VerboseOutput) { Write-Host "> $FilePath $($Arguments -join ' ')" }
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Command failed with exit code $LASTEXITCODE: $FilePath $($Arguments -join ' ')" }
}

Invoke-Gate "repository identity and commit SHA" { Invoke-Native git @("rev-parse","--show-toplevel"); Invoke-Native git @("rev-parse","--verify","HEAD"); & git config --get remote.origin.url }
if (-not $SkipClean) { Invoke-Gate "clean tracked worktree" { Invoke-Native git @("diff","--quiet","--",".",":!artifacts"); Invoke-Native git @("diff","--cached","--quiet","--",".",":!artifacts") } } else { Add-Outcome "clean tracked worktree" "skipped" (Join-Path $logsDir "clean_tracked_worktree.log") "-SkipClean" }
Invoke-Gate "version consistency" { Invoke-Native pwsh @("-NoProfile","-ExecutionPolicy","Bypass","-File","scripts/validate-version-ownership.ps1") }
Invoke-Gate "required documentation and public files" { foreach ($p in @("README.md","CHANGELOG.md","LICENSE","SECURITY.md","CONTRIBUTING.md","Docs/releases/$Version.md","Docs/compatibility.md","Docs/known-limitations.md")) { if (-not (Test-Path $p)) { throw "Missing $p" } } }
Invoke-Gate "diagnostic catalog coverage" { Invoke-Native pwsh @("-NoProfile","-ExecutionPolicy","Bypass","-File","scripts/validate-diagnostics.ps1") }
Invoke-Gate "specification structure and links" { Invoke-Native pwsh @("-NoProfile","-ExecutionPolicy","Bypass","-File","scripts/validate-documentation.ps1") }
Invoke-Gate "restore" { Invoke-Native dotnet @("restore","Martin/Martin.slnx") }
Invoke-Gate "debug build" { Invoke-Native dotnet @("build","Martin/Martin.slnx","--configuration","Debug","--no-restore","-p:Platform=$Platform") }
Invoke-Gate "release build" { Invoke-Native dotnet @("build","Martin/Martin.slnx","--configuration",$Configuration,"--no-restore","-p:Platform=$Platform") }
Invoke-Gate "local test suite" { Invoke-Native dotnet @("test","Martin/Martin.slnx","--configuration",$Configuration,"--no-build","-p:Platform=$Platform","--logger","trx;LogFileName=release-validation.trx","--results-directory",(Join-Path $evidenceRoot "test-results")) }
Invoke-Gate "sample build and execution" { Invoke-Native pwsh @("-NoProfile","-ExecutionPolicy","Bypass","-File","scripts/validate-samples.ps1","-Configuration",$Configuration) }
Invoke-Gate "formatter check" { Get-ChildItem samples/*/Sources/*.martin | ForEach-Object { Invoke-Native dotnet @("run","--project","Martin/src/Martin.Cli/Martin.Cli.csproj","--","format",$_.FullName,"--check") } }
Invoke-Gate "tool packing" { Invoke-Native dotnet @("pack","Martin/src/Martin.Cli/Martin.Cli.csproj","--configuration",$Configuration,"--no-build","--output",(Join-Path $evidenceRoot "tool-packages"),"-p:Platform=$Platform") }
Invoke-Gate "runtime packing" { Invoke-Native dotnet @("pack","Martin/src/Martin.Runtime/Martin.Runtime.csproj","--configuration",$Configuration,"--no-build","--output",(Join-Path $evidenceRoot "runtime-packages"),"-p:Platform=$Platform") }
Invoke-Gate "package-content inspection and release manifest" { Invoke-Native pwsh @("-NoProfile","-ExecutionPolicy","Bypass","-File","scripts/package-alpha.ps1","-Version",$Version,"-Configuration",$Configuration,"-Platform",$Platform,"-SkipBuild") }
Invoke-Gate "isolated tool installation and CLI/compiler smoke workflows" { Invoke-Native pwsh @("-NoProfile","-ExecutionPolicy","Bypass","-File","scripts/smoke-test-tools.ps1","-Configuration",$Configuration,"-KeepArtifacts") }
if ($IncludeStudio) {
    if ($IsWindows) {
        Invoke-Gate "Studio publish on Windows" { Invoke-Native dotnet @("publish","Martin/src/Martin.Studio.WinUI/Martin.Studio.WinUI.csproj","--configuration",$Configuration,"-p:Platform=$Platform","-p:RuntimeIdentifier=win-$Platform") }
        Invoke-Gate "Studio manual-smoke record" { if (-not (Test-Path "Docs/studio-packaging.md")) { throw "Missing Studio manual smoke documentation" }; Write-Host "Manual Studio smoke must be recorded in release review." }
    } else {
        Add-Outcome "Studio publish on Windows" "incomplete" (Join-Path $logsDir "Studio_publish_on_Windows.log") "requires Windows $Platform"
        Add-Outcome "Studio manual-smoke record" "incomplete" (Join-Path $logsDir "Studio_manual-smoke_record.log") "requires Windows manual smoke"
    }
} else {
    Add-Outcome "Studio publish on Windows" "skipped" (Join-Path $logsDir "Studio_publish_on_Windows.log") "pass -IncludeStudio on Windows for complete alpha validation"
    Add-Outcome "Studio manual-smoke record" "skipped" (Join-Path $logsDir "Studio_manual-smoke_record.log") "pass -IncludeStudio on Windows for complete alpha validation"
}
Invoke-Gate "artifact checksums and release manifest" { if (-not (Test-Path "artifacts/release/$Version/checksums/SHA256SUMS")) { throw "Missing checksums" }; Get-Content "artifacts/release/$Version/validation/manifest.json" | ConvertFrom-Json | Out-Null }
$status = if ((Get-Content $outcomesPath | ConvertFrom-Json | Where-Object { $_.status -eq "incomplete" })) { "incomplete" } else { "passed" }
Write-Summary $status
Write-Host "Release validation completed. Summary: $summaryPath"
