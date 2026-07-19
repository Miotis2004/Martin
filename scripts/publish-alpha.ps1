param(
    [string]$Version = "0.1.0-alpha",
    [Parameter(Mandatory=$true)][string]$Review,
    [string]$Output
)
$argsList = @("$PSScriptRoot/publish-alpha.py", "--version", $Version, "--review", $Review)
if ($Output) { $argsList += @("--output", $Output) }
python @argsList
exit $LASTEXITCODE
