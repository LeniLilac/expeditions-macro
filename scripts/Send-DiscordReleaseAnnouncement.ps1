[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$WebhookUrl,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$')]
    [string]$Repository,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^v\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$')]
    [string]$Tag,

    [ValidatePattern('^\d{17,20}$')]
    [string]$RoleId = '1528250880304873643',

    [string]$ReleaseNotesPath,

    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($WebhookUrl -notmatch '^https://(?:(?:canary|ptb)\.)?(?:discord\.com|discordapp\.com)/api/webhooks/\d+/[A-Za-z0-9._-]+/?$') {
    throw 'The Discord release webhook URL is invalid.'
}

function Get-ReleaseHighlights {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path -PathType Leaf)) { return @() }

    $highlights = [System.Collections.Generic.List[string]]::new()
    $insideChanges = $false
    foreach ($line in [System.IO.File]::ReadAllLines([System.IO.Path]::GetFullPath($Path))) {
        if ($line -eq '## Changes') {
            $insideChanges = $true
            continue
        }
        if ($insideChanges -and $line.StartsWith('## ', [System.StringComparison]::Ordinal)) { break }
        if ($insideChanges -and $line.StartsWith('- ', [System.StringComparison]::Ordinal)) {
            $highlights.Add($line)
            if ($highlights.Count -eq 5) { break }
        }
    }
    return @($highlights)
}

$version = $Tag.TrimStart('v')
$releaseUrl = "https://github.com/$Repository/releases/tag/$Tag"
$assetRoot = "https://github.com/$Repository/releases/download/$Tag"
$installerUrl = "$assetRoot/ExpeditionsMacro-$version-win-x64-setup.exe"
$portableUrl = "$assetRoot/ExpeditionsMacro-$version-win-x64.zip"
$highlights = @(Get-ReleaseHighlights -Path $ReleaseNotesPath)

$details = 'A new Windows release is available.'
if ($highlights.Count -gt 0) {
    $details += "`n`n### Highlights`n" + ($highlights -join "`n")
}

$payload = [ordered]@{
    flags = 1 -shl 15
    allowed_mentions = [ordered]@{
        roles = @($RoleId)
    }
    components = @(
        [ordered]@{
            type = 17
            components = @(
                [ordered]@{
                    type = 10
                    content = "# Expeditions Macro $Tag`n<@&$RoleId>"
                },
                [ordered]@{
                    type = 14
                    divider = $true
                    spacing = 1
                },
                [ordered]@{
                    type = 10
                    content = $details
                },
                [ordered]@{
                    type = 10
                    content = "[Release notes]($releaseUrl) · [Windows installer]($installerUrl) · [Portable ZIP]($portableUrl)`n-# SHA-256 checksums are included on the release page."
                }
            )
        }
    )
}

$json = $payload | ConvertTo-Json -Depth 10 -Compress
if ($DryRun) {
    Write-Output $json
    return
}

$separator = if ($WebhookUrl.Contains('?')) { '&' } else { '?' }
$endpoint = "$WebhookUrl${separator}wait=true&with_components=true"
for ($attempt = 1; $attempt -le 3; $attempt++) {
    try {
        $null = Invoke-WebRequest -Method Post -Uri $endpoint -ContentType 'application/json' -Body $json -Headers @{ 'User-Agent' = 'ExpeditionsMacroReleaseWorkflow/1.0' }
        Write-Host "Discord release announcement sent for $Tag."
        return
    }
    catch {
        $statusCode = $null
        if ($null -ne $_.Exception.Response) {
            try { $statusCode = [int]$_.Exception.Response.StatusCode } catch { }
        }
        if ($attempt -eq 3) {
            $suffix = if ($null -eq $statusCode) { '' } else { " (HTTP $statusCode)" }
            throw "Discord release announcement failed after $attempt attempts$suffix."
        }
        Start-Sleep -Seconds ([Math]::Pow(2, $attempt - 1))
    }
}
