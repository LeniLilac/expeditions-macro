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

$process = Start-Process -FilePath $executable -ArgumentList @('--snapshot-ui', $output) -PassThru -WindowStyle Hidden
if (-not $process.WaitForExit(180000)) {
    Stop-Process -Id $process.Id -Force
    $progress = Join-Path $output 'snapshot-progress.txt'
    $lastProgress = if (Test-Path -LiteralPath $progress) {
        Get-Content -Raw -LiteralPath $progress
    }
    else {
        'The renderer did not write a progress record.'
    }
    throw "UI snapshot renderer did not finish within three minutes. Last progress: $lastProgress"
}
$process.WaitForExit()
if ($process.ExitCode -ne 0) {
    $errorFile = Join-Path $output 'snapshot-error.txt'
    $details = if (Test-Path -LiteralPath $errorFile) {
        Get-Content -Raw -LiteralPath $errorFile
    }
    else {
        'No snapshot error record was written.'
    }
    throw "UI snapshot renderer exited with code $($process.ExitCode). $details"
}
$files = @(Get-ChildItem -LiteralPath $output -File -Filter '*.png')
if ($files.Count -ne 90) { throw "Expected 90 dark/light UI snapshots, found $($files.Count)." }
$files | Sort-Object Name | Select-Object Name, Length
