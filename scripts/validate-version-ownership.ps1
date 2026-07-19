$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Push-Location $repoRoot
try {
    $props = Get-Content "Martin/Directory.Build.props" -Raw
    $productVersion = [regex]::Match($props, '<Version>([^<]+)</Version>').Groups[1].Value
    $assemblyVersion = [regex]::Match($props, '<AssemblyVersion>([^<]+)</AssemblyVersion>').Groups[1].Value
    $fileVersion = [regex]::Match($props, '<FileVersion>([^<]+)</FileVersion>').Groups[1].Value
    $languageVersion = [regex]::Match((Get-Content "Martin/src/Martin.Compiler/LanguageVersion.cs" -Raw), 'public const string Current = "([^"]+)";').Groups[1].Value
    $manifestVersion = [regex]::Match((Get-Content "Martin/src/Martin.ProjectSystem/ManifestParser.cs" -Raw), 'SupportedManifestVersion = ([0-9]+);').Groups[1].Value
    $msixVersion = [regex]::Match((Get-Content "Martin/src/Martin.Studio.WinUI/Package.appxmanifest" -Raw), 'Version="([^"]+)"').Groups[1].Value
    $expectedMsix = ($productVersion -replace '-.*$','') + '.0'
    $fail = $false
    function Check($Name, $Actual, $Expected) {
        if ($Actual -ne $Expected) { Write-Error "Version mismatch: $Name expected $Expected but found $Actual" -ErrorAction Continue; $script:fail = $true }
    }
    Check product $productVersion "0.1.0-alpha"
    Check assembly $assemblyVersion "0.1.0.0"
    Check file $fileVersion "0.1.0.0"
    Check language $languageVersion "0.1"
    Check manifest $manifestVersion "1"
    Check msix $msixVersion $expectedMsix
    if (-not (Select-String -Path "Martin/src/Martin.Cli/CommandHandlers.cs" -Pattern 'Language version: \{versions\.LanguageVersion\}' -Quiet)) { Write-Error "Version mismatch: martin version output does not include language version." -ErrorAction Continue; $fail = $true }
    if (-not (Select-String -Path "Martin/src/Martin.Compiler.Cli/Program.cs" -Pattern 'Language version: \{Versions\.LanguageVersion\}' -Quiet)) { Write-Error "Version mismatch: martinc version output does not include language version." -ErrorAction Continue; $fail = $true }
    $compat = Get-Content "Docs/compatibility.md" -Raw
    if ($compat -notmatch '`0\.1\.0-alpha`' -or $compat -notmatch '`0\.1`') { Write-Error "Version mismatch: Docs/compatibility.md does not document product and language versions." -ErrorAction Continue; $fail = $true }
    if ($fail) { exit 1 }
    Write-Host "Version ownership validation passed: product=$productVersion language=$languageVersion manifest=$manifestVersion msix=$msixVersion"
}
finally { Pop-Location }
