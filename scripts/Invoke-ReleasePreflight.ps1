[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+(?:-(?:[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*))?$')]
    [string]$Version,

    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),

    [switch]$Silent,

    [switch]$RequireCleanTree
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath($RepositoryRoot)

& (Join-Path $root 'scripts\Test-RepositoryPolicy.ps1') -RepositoryRoot $root
& (Join-Path $root 'scripts\Test-ReleaseMetadata.ps1') -Version $Version -RepositoryRoot $root -Silent:$Silent

if ($RequireCleanTree) {
    $status = @(& git -C $root status --porcelain --untracked-files=all)
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to inspect the Git working tree during release preflight.'
    }
    if ($status.Count -gt 0) {
        throw "Release preflight requires a clean working tree:`r`n$($status -join "`r`n")"
    }
}

Write-Host "Release preflight passed for $Version."
