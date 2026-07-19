param(
    [string]$Configuration = "Release",
    [switch]$KeepArtifacts,
    [switch]$VerboseOutput
)

$ErrorActionPreference = "Stop"

function Resolve-RepositoryRoot {
    $directory = $PSScriptRoot
    while ($directory) {
        if (Test-Path (Join-Path $directory "Martin/Martin.slnx")) {
            return (Resolve-Path $directory).Path
        }

        $parent = Split-Path -Parent $directory
        if ($parent -eq $directory) { break }
        $directory = $parent
    }

    throw "Unable to resolve repository root from '$PSScriptRoot'."
}

function Invoke-LoggedCommand {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments
    )

    if ($VerboseOutput) {
        Write-Host "> $FilePath $($Arguments -join ' ')"
    }

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE: $FilePath $($Arguments -join ' ')"
    }
}

$repoRoot = Resolve-RepositoryRoot
$artifactsRoot = Join-Path $repoRoot "artifacts/tool-smoke-tests"
$runRoot = Join-Path $artifactsRoot ([DateTimeOffset]::UtcNow.ToString("yyyyMMddHHmmssfff"))
$packageRoot = Join-Path $runRoot "packages"
$toolRoot = Join-Path $runRoot "tools"
$workspaceRoot = Join-Path $runRoot "workspace"
$projectRoot = Join-Path $workspaceRoot "SmokeProject"
$compilerRoot = Join-Path $workspaceRoot "compiler"
$compilerSource = Join-Path $compilerRoot "main.martin"
$compilerOutput = Join-Path $compilerRoot "out"
$packageListing = Join-Path $runRoot "package-contents.txt"

try {
    New-Item -ItemType Directory -Force -Path $packageRoot, $toolRoot, $workspaceRoot, $compilerRoot | Out-Null

    Invoke-LoggedCommand dotnet restore (Join-Path $repoRoot "Martin/Martin.slnx")
    Invoke-LoggedCommand dotnet build (Join-Path $repoRoot "Martin/Martin.slnx") --configuration $Configuration --no-restore -p:Platform=x64
    Invoke-LoggedCommand dotnet pack (Join-Path $repoRoot "Martin/src/Martin.Cli/Martin.Cli.csproj") --configuration $Configuration --no-build --output $packageRoot -p:Platform=x64
    Invoke-LoggedCommand dotnet pack (Join-Path $repoRoot "Martin/src/Martin.Compiler.Cli/Martin.Compiler.Cli.csproj") --configuration $Configuration --no-build --output $packageRoot -p:Platform=x64

    Set-Content -Path $packageListing -Value ""
    foreach ($package in Get-ChildItem -Path $packageRoot -Filter "*.nupkg") {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $archive = [System.IO.Compression.ZipFile]::OpenRead($package.FullName)
        try {
            foreach ($entry in $archive.Entries) {
                Add-Content -Path $packageListing -Value $entry.FullName
                if ($entry.FullName -match '(^|/)(\.git|bin|obj|TestResults|artifacts|node_modules|\.vs|.*\.(user|suo|orig|rej|tmp|temp|bak|cache))(/|$)') {
                    throw "Package contains development residue: $($package.FullName) entry $($entry.FullName)"
                }
            }
        }
        finally {
            $archive.Dispose()
        }
    }

    Invoke-LoggedCommand dotnet tool install Martin.Tool --tool-path $toolRoot --add-source $packageRoot
    Invoke-LoggedCommand dotnet tool install Martin.Compiler.Tool --tool-path $toolRoot --add-source $packageRoot

    $martin = Join-Path $toolRoot (if ($IsWindows) { "martin.exe" } else { "martin" })
    $martinc = Join-Path $toolRoot (if ($IsWindows) { "martinc.exe" } else { "martinc" })

    Push-Location $workspaceRoot
    try {
        Invoke-LoggedCommand $martin --version
        Invoke-LoggedCommand $martinc --version
        Invoke-LoggedCommand $martin new SmokeProject --path $workspaceRoot --no-git --force
    }
    finally {
        Pop-Location
    }

    Push-Location $projectRoot
    try {
        Invoke-LoggedCommand $martin build
        Invoke-LoggedCommand $martin run
        Invoke-LoggedCommand $martin run --no-build
        Invoke-LoggedCommand $martin clean --dry-run
        Invoke-LoggedCommand $martin clean
    }
    finally {
        Pop-Location
    }

    Set-Content -Path $compilerSource -Value 'func main() { print("hello from martinc smoke test") }' -NoNewline
    Push-Location $compilerRoot
    try {
        Invoke-LoggedCommand $martinc $compilerSource --output $compilerOutput --name SmokeCompiler --quiet
    }
    finally {
        Pop-Location
    }

    Invoke-LoggedCommand dotnet tool uninstall Martin.Tool --tool-path $toolRoot
    Invoke-LoggedCommand dotnet tool uninstall Martin.Compiler.Tool --tool-path $toolRoot

    Write-Host "Packaging smoke tests completed successfully."
    Write-Host "Artifacts: $runRoot"
}
finally {
    if (-not $KeepArtifacts -and (Test-Path $runRoot)) {
        Remove-Item -Recurse -Force $runRoot
    }
}
