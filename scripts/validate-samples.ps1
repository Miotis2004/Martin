param(
    [string]$Configuration = $(if ($env:CONFIGURATION) { $env:CONFIGURATION } else { "Release" }),
    [string]$WorkRoot = $env:MARTIN_SAMPLE_WORK_ROOT,
    [switch]$KeepWork
)

$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
$SamplesRoot = Join-Path $Root "samples"
$CliProject = Join-Path $Root "Martin/src/Martin.Cli/Martin.Cli.csproj"
$Samples = @("HelloMartin", "Functions", "StructsAndClasses", "Optionals", "EnumsAndPatterns", "Protocols", "Generics", "TypedErrors")

if (-not (Test-Path $CliProject)) { throw "Martin CLI project not found: $CliProject" }

if ([string]::IsNullOrWhiteSpace($WorkRoot)) {
    $WorkRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("martin-samples." + [Guid]::NewGuid().ToString("N"))
} elseif (Test-Path $WorkRoot) {
    Remove-Item -Recurse -Force $WorkRoot
}
New-Item -ItemType Directory -Force -Path $WorkRoot | Out-Null

$Failures = 0
try {
    foreach ($Sample in $Samples) {
        $SourceDir = Join-Path $SamplesRoot $Sample
        $IsolatedDir = Join-Path $WorkRoot $Sample
        Copy-Item -Recurse $SourceDir $IsolatedDir
        foreach ($Generated in @("bin", "obj", ".martin")) {
            $GeneratedPath = Join-Path $IsolatedDir $Generated
            if (Test-Path $GeneratedPath) { Remove-Item -Recurse -Force $GeneratedPath }
        }

        Write-Host "==> $Sample: build"
        & dotnet run --project $CliProject -- build $IsolatedDir --configuration $Configuration --quiet
        if ($LASTEXITCODE -ne 0) { Write-Error "FAIL $Sample: build failed"; $Failures++; continue }

        Write-Host "==> $Sample: run"
        $Actual = Join-Path $WorkRoot "$Sample.stdout"
        & dotnet run --project $CliProject -- run $IsolatedDir --configuration $Configuration --quiet > $Actual
        if ($LASTEXITCODE -ne 0) { Write-Error "FAIL $Sample: run failed"; $Failures++; continue }

        $Expected = Join-Path $SourceDir "expected.stdout"
        if ((Get-Content -Raw $Expected) -cne (Get-Content -Raw $Actual)) {
            Write-Error "FAIL $Sample: output differed"
            $Failures++
            continue
        }

        Write-Host "PASS $Sample"
    }

    if ($Failures -ne 0) { throw "$Failures sample validation failure(s)." }
    Write-Host "All samples validated from isolated copies."
}
finally {
    if ($KeepWork -or $env:MARTIN_SAMPLE_KEEP_WORK -eq "1") {
        Write-Host "Kept sample validation workspace: $WorkRoot"
    } elseif (Test-Path $WorkRoot) {
        Remove-Item -Recurse -Force $WorkRoot
    }
}
