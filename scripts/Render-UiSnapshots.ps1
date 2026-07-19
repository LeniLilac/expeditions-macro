[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [string]$OutputDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\ui-snapshots')
)

$ErrorActionPreference = 'Stop'
$repository = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$output = [System.IO.Path]::GetFullPath($OutputDirectory)
if (-not $output.StartsWith($repository, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'UI snapshot output must remain inside the repository.'
}

$executable = Join-Path $repository "src\ExpeditionsMacro.App\bin\$Configuration\net10.0-windows10.0.19041.0\win-x64\ExpeditionsMacro.exe"
if (-not (Test-Path -LiteralPath $executable)) { throw "Build the $Configuration app before rendering UI snapshots." }
if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Recurse -Force }

$process = Start-Process -FilePath $executable -ArgumentList @('--snapshot-ui', $output) -PassThru -Wait -WindowStyle Hidden
if ($process.ExitCode -ne 0) { throw "UI snapshot renderer exited with code $($process.ExitCode)." }
$files = @(Get-ChildItem -LiteralPath $output -File -Filter '*.png')
if ($files.Count -ne 12) { throw "Expected 12 dark/light UI snapshots, found $($files.Count)." }
$files | Sort-Object Name | Select-Object Name, Length
