[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = [System.IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\', '/')
$rootPrefix = $root + [System.IO.Path]::DirectorySeparatorChar
$configPath = Join-Path $root 'eng\repository-policy.json'
if (-not (Test-Path -LiteralPath $configPath -PathType Leaf)) {
    throw "Repository policy configuration is missing: $configPath"
}

$policy = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
$failures = [System.Collections.Generic.List[string]]::new()

function Add-PolicyFailure {
    param([string]$Message)
    $script:failures.Add($Message)
}

function Normalize-PolicyPath {
    param([string]$Path)
    return $Path.Replace('\', '/').TrimStart('/')
}

function ConvertTo-RepositoryPath {
    param([string]$FullPath)
    $absolute = [System.IO.Path]::GetFullPath($FullPath)
    if (-not $absolute.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Path escaped repository root: $absolute"
    }
    return Normalize-PolicyPath $absolute.Substring($rootPrefix.Length)
}

function Get-LineCount {
    param([string]$Path)
    return [System.IO.File]::ReadAllLines($Path).Length
}

function Get-SourceCategory {
    param([string]$RelativePath)
    if ($RelativePath.StartsWith('scripts/', [System.StringComparison]::OrdinalIgnoreCase)) {
        return 'script'
    }
    if ($RelativePath.StartsWith('tests/', [System.StringComparison]::OrdinalIgnoreCase) -or
        $RelativePath -match '^tools/[^/]+\.Tests/') {
        return 'test'
    }
    if ($RelativePath.StartsWith('src/', [System.StringComparison]::OrdinalIgnoreCase) -or
        $RelativePath.StartsWith('tools/', [System.StringComparison]::OrdinalIgnoreCase)) {
        return 'production'
    }
    return $null
}

function Get-LineLimit {
    param([string]$Category)
    $property = $policy.lineLimits.PSObject.Properties[$Category]
    if ($null -eq $property) {
        throw "Missing line limit for category '$Category'."
    }
    return [int]$property.Value
}

if ([int]$policy.version -ne 1) {
    Add-PolicyFailure "Unsupported repository policy version: $($policy.version)."
}

foreach ($required in @($policy.requiredDocs)) {
    $relative = Normalize-PolicyPath ([string]$required)
    if (-not (Test-Path -LiteralPath (Join-Path $root $relative) -PathType Leaf)) {
        Add-PolicyFailure "Required contributor document is missing: $relative"
    }
}

$agentsPath = Join-Path $root 'AGENTS.md'
if (-not (Test-Path -LiteralPath $agentsPath -PathType Leaf)) {
    Add-PolicyFailure 'Root AGENTS.md is missing.'
}
else {
    $agentsLines = Get-LineCount $agentsPath
    $agentsLimit = [int]$policy.rootAgentsMaxLines
    if ($agentsLines -gt $agentsLimit) {
        Add-PolicyFailure "AGENTS.md is $agentsLines lines; keep top-level instructions at or below $agentsLimit and move details into docs/."
    }
}

$debtByPath = @{}
foreach ($entry in @($policy.lineDebt)) {
    $relative = Normalize-PolicyPath ([string]$entry.path)
    if ($relative -match '[*?\[\]]') {
        Add-PolicyFailure "Line-debt entries must use exact paths, not patterns: $relative"
        continue
    }
    if ($debtByPath.ContainsKey($relative)) {
        Add-PolicyFailure "Duplicate line-debt entry: $relative"
        continue
    }
    if ([string]::IsNullOrWhiteSpace([string]$entry.reason)) {
        Add-PolicyFailure "Line-debt entry needs a concrete rationale: $relative"
    }
    $debtByPath[$relative] = $entry
}

$observedLines = @{}
$candidateRoots = @('src', 'tools', 'tests', 'scripts')
foreach ($candidateRoot in $candidateRoots) {
    $fullRoot = Join-Path $root $candidateRoot
    if (-not (Test-Path -LiteralPath $fullRoot -PathType Container)) {
        Add-PolicyFailure "Source root is missing: $candidateRoot"
        continue
    }

    foreach ($file in Get-ChildItem -LiteralPath $fullRoot -Recurse -File) {
        $relative = ConvertTo-RepositoryPath $file.FullName
        if ($relative -match '(^|/)(bin|obj|artifacts|TestResults)/') {
            continue
        }

        $category = Get-SourceCategory $relative
        if ($null -eq $category) {
            continue
        }

        $extension = $file.Extension.ToLowerInvariant()
        $isChecked = if ($category -eq 'script') {
            $extension -eq '.ps1'
        }
        else {
            $extension -in @('.cs', '.xaml')
        }
        if (-not $isChecked) {
            continue
        }

        $lineCount = Get-LineCount $file.FullName
        $observedLines[$relative] = $lineCount
        $limit = Get-LineLimit $category
        if ($lineCount -gt $limit -and -not $debtByPath.ContainsKey($relative)) {
            Add-PolicyFailure "$relative is $lineCount lines; split this $category file to $limit lines or fewer instead of adding new structural debt."
        }
    }
}

foreach ($relative in $debtByPath.Keys) {
    $entry = $debtByPath[$relative]
    if (-not $observedLines.ContainsKey($relative)) {
        Add-PolicyFailure "Line-debt entry points to a missing or unchecked file: $relative"
        continue
    }

    $category = Get-SourceCategory $relative
    $limit = Get-LineLimit $category
    $ceiling = [int]$entry.maxLines
    $actual = [int]$observedLines[$relative]
    if ($ceiling -le $limit) {
        Add-PolicyFailure "Line-debt ceiling for $relative must be above its normal $limit-line budget."
    }
    elseif ($actual -gt $ceiling) {
        Add-PolicyFailure "$relative grew from its $ceiling-line debt ceiling to $actual lines; split it rather than raising the ceiling."
    }
    elseif ($actual -le $limit) {
        Add-PolicyFailure "$relative is now within its $limit-line budget; remove the stale line-debt entry."
    }
    elseif ($actual -lt $ceiling) {
        Add-PolicyFailure "$relative shrank to $actual lines; lower its line-debt ceiling from $ceiling to preserve the improvement."
    }
}

$allowedDependencies = @{}
foreach ($projectProperty in $policy.projectDependencies.PSObject.Properties) {
    $project = Normalize-PolicyPath $projectProperty.Name
    $targets = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($target in @($projectProperty.Value)) {
        [void]$targets.Add((Normalize-PolicyPath ([string]$target)))
    }
    $allowedDependencies[$project] = $targets
}

$projectFiles = @(
    Get-ChildItem -LiteralPath (Join-Path $root 'src'), (Join-Path $root 'tests'), (Join-Path $root 'tools') -Filter '*.csproj' -Recurse -File |
        Where-Object { (ConvertTo-RepositoryPath $_.FullName) -notmatch '(^|/)(bin|obj)/' }
)
$discoveredProjects = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($projectFile in $projectFiles) {
    $project = ConvertTo-RepositoryPath $projectFile.FullName
    [void]$discoveredProjects.Add($project)
    if (-not $allowedDependencies.ContainsKey($project)) {
        Add-PolicyFailure "Project is not reviewed in eng/repository-policy.json: $project"
        continue
    }

    [xml]$projectXml = Get-Content -LiteralPath $projectFile.FullName -Raw
    foreach ($reference in @($projectXml.SelectNodes("//*[local-name()='ProjectReference']"))) {
        $targetFullPath = [System.IO.Path]::GetFullPath((Join-Path $projectFile.DirectoryName ([string]$reference.Include)))
        $target = ConvertTo-RepositoryPath $targetFullPath
        if (-not $allowedDependencies[$project].Contains($target)) {
            Add-PolicyFailure "Forbidden project dependency: $project -> $target"
        }
    }
}

foreach ($project in $allowedDependencies.Keys) {
    if (-not $discoveredProjects.Contains($project)) {
        Add-PolicyFailure "Project dependency policy points to a missing project: $project"
    }
    foreach ($target in $allowedDependencies[$project]) {
        if (-not (Test-Path -LiteralPath (Join-Path $root $target) -PathType Leaf)) {
            Add-PolicyFailure "Allowed project dependency does not exist: $project -> $target"
        }
    }
}

if (Test-Path -LiteralPath (Join-Path $root '.git')) {
    $trackedFiles = @(& git -C $root ls-files)
    if ($LASTEXITCODE -ne 0) {
        Add-PolicyFailure 'Unable to inspect tracked files with git ls-files.'
    }
    else {
        foreach ($trackedFile in $trackedFiles) {
            $relative = Normalize-PolicyPath $trackedFile
            if ($relative -match '(^|/)(bin|obj|artifacts|TestResults|\.vs|\.codex-tmp)/' -or $relative.EndsWith('.user')) {
                Add-PolicyFailure "Generated or machine-local file is tracked: $relative"
            }
        }
    }
}

if ($failures.Count -gt 0) {
    $message = "Repository policy check failed:`r`n - " + ($failures -join "`r`n - ")
    throw $message
}

Write-Host "Repository policy checks passed ($($observedLines.Count) source files, $($projectFiles.Count) projects)."
