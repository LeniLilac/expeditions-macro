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

$portable = Get-ChildItem -LiteralPath $ReleaseDirectory -Filter 'ExpeditionsMacro-*-win-x64.zip' -File | Select-Object -First 1
if (-not $portable) { throw 'The portable application archive is missing.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($portable.FullName)
try {
    $entryNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $archive.Entries) { [void]$entryNames.Add([System.IO.Path]::GetFileName($entry.FullName)) }
    foreach ($required in @('OpenCvSharpExtern.dll', 'msvcp140.dll', 'vcruntime140.dll', 'vcruntime140_1.dll')) {
        if (-not $entryNames.Contains($required)) { throw "Portable archive is missing native dependency: $required" }
    }
}
finally {
    $archive.Dispose()
}

Write-Host 'All release checksums are valid.'
