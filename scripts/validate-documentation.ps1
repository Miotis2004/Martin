#!/usr/bin/env pwsh
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $PSCommandPath
$validator = Join-Path $scriptDir "validate-documentation.sh"
if (-not (Get-Command bash -ErrorAction SilentlyContinue)) {
    throw "bash is required to run the documentation validator."
}
& bash $validator
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
