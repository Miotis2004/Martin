param(
    [string]$Version = "0.1.0-alpha",
    [string]$Manifest,
    [string]$Checksums,
    [string]$ValidationSummary,
    [string]$StudioSummary,
    [switch]$RequireStudio,
    [ValidateSet("pending","approved","blocked")][string]$Approval = "pending",
    [string[]]$Blocker = @(),
    [string]$Output
)
$ErrorActionPreference = "Stop"
$argsList = @("$PSScriptRoot/validate-alpha-candidate.py", "--version", $Version, "--approval", $Approval)
if ($Manifest) { $argsList += @("--manifest", $Manifest) }
if ($Checksums) { $argsList += @("--checksums", $Checksums) }
if ($ValidationSummary) { $argsList += @("--validation-summary", $ValidationSummary) }
if ($StudioSummary) { $argsList += @("--studio-summary", $StudioSummary) }
if ($RequireStudio) { $argsList += "--require-studio" }
foreach ($b in $Blocker) { $argsList += @("--blocker", $b) }
if ($Output) { $argsList += @("--output", $Output) }
python @argsList
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
