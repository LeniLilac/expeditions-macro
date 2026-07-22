[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+(?:-(?:[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*))?$')]
    [string]$Version,

    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),

    [switch]$Silent
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath($RepositoryRoot)
$failures = [System.Collections.Generic.List[string]]::new()

function Add-ReleaseFailure {
    param([string]$Message)
    $script:failures.Add($Message)
}

$propsPath = Join-Path $root 'Directory.Build.props'
[xml]$props = Get-Content -LiteralPath $propsPath -Raw
$prefixNode = $props.SelectSingleNode("//*[local-name()='VersionPrefix']")
$suffixNode = $props.SelectSingleNode("//*[local-name()='VersionSuffix']")
if ($null -eq $prefixNode) {
    Add-ReleaseFailure 'Directory.Build.props does not define VersionPrefix.'
}
else {
    $sourceVersion = [string]$prefixNode.InnerText
    if ($null -ne $suffixNode -and -not [string]::IsNullOrWhiteSpace([string]$suffixNode.InnerText)) {
        $sourceVersion += '-' + [string]$suffixNode.InnerText
    }
    if ($sourceVersion -ne $Version) {
        Add-ReleaseFailure "Directory.Build.props resolves to $sourceVersion, not requested release $Version."
    }
}

$escapedVersion = [System.Text.RegularExpressions.Regex]::Escape($Version)
$changelogPath = Join-Path $root 'CHANGELOG.md'
$changelog = Get-Content -LiteralPath $changelogPath -Raw
if ($changelog -notmatch "(?m)^## \[$escapedVersion\] - \d{4}-\d{2}-\d{2}\s*$") {
    Add-ReleaseFailure "CHANGELOG.md needs a dated '## [$Version] - YYYY-MM-DD' section."
}

$notesPath = Join-Path $root "docs\release-notes\$Version.md"
if (-not (Test-Path -LiteralPath $notesPath -PathType Leaf)) {
    Add-ReleaseFailure "Release notes are missing: docs/release-notes/$Version.md"
}
else {
    $notes = Get-Content -LiteralPath $notesPath -Raw
    if ($notes -notmatch "(?m)^# Expeditions Macro v$escapedVersion\s*$") {
        Add-ReleaseFailure "Release notes must start with '# Expeditions Macro v$Version'."
    }
    foreach ($heading in @('Validation', 'Assets')) {
        if ($notes -notmatch "(?m)^## $heading\s*$") {
            Add-ReleaseFailure "Release notes need a '## $heading' section."
        }
    }
    foreach ($asset in @(
        "ExpeditionsMacro-$Version-win-x64-setup.exe",
        "ExpeditionsMacro-$Version-win-x64.zip",
        'dependencies.json',
        'SHA256SUMS.txt'
    )) {
        if (-not $notes.Contains($asset)) {
            Add-ReleaseFailure "Release notes do not list required asset: $asset"
        }
    }
    if ($notes -match '(?i)\bTODO\b|<version>|<summary>|<date>') {
        Add-ReleaseFailure 'Release notes still contain template placeholders.'
    }
    if ($Silent -and $notes -notmatch '(?i)(does not|without).{0,30}Discord release announcement') {
        Add-ReleaseFailure 'Silent prerelease notes must state that no Discord release announcement is sent.'
    }
}

if ($Silent -and -not $Version.Contains('-')) {
    Add-ReleaseFailure 'The silent workflow is reserved for prerelease versions.'
}

if ($failures.Count -gt 0) {
    throw "Release metadata check failed:`r`n - $($failures -join "`r`n - ")"
}

Write-Host "Release metadata checks passed for $Version."
