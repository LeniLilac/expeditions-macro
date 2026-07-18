[CmdletBinding()]
param([string]$ReleaseDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\release'))

$ErrorActionPreference = 'Stop'
$checksums = Join-Path $ReleaseDirectory 'SHA256SUMS.txt'
if (-not (Test-Path -LiteralPath $checksums)) { throw 'SHA256SUMS.txt is missing.' }

foreach ($line in [System.IO.File]::ReadAllLines($checksums)) {
    if ($line -notmatch '^([0-9a-f]{64})  (.+)$') { throw "Malformed checksum line: $line" }
    $path = Join-Path $ReleaseDirectory $Matches[2]
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing release asset: $($Matches[2])" }
    $actual = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $Matches[1]) { throw "Checksum mismatch: $($Matches[2])" }
}

Write-Host 'All release checksums are valid.'
