<#
.SYNOPSIS
Renders a timestamped contact sheet from an Expeditions Macro deep-debug archive.

.EXAMPLE
./scripts/New-DiagnosticContactSheet.ps1 diagnostics.zip -StartFrame 220 -EndFrame 280

.EXAMPLE
./scripts/New-DiagnosticContactSheet.ps1 diagnostics.zip -FrameNumbers 231,232,234,262,269,273

.EXAMPLE
./scripts/New-DiagnosticContactSheet.ps1 diagnostics.zip -StartTimeUtc '2026-07-22T17:18:30Z' -EndTimeUtc '2026-07-22T17:19:31Z'
#>
[CmdletBinding(DefaultParameterSetName = 'Range')]
param(
    [Parameter(Mandatory, Position = 0)]
    [string]$InputPath,

    [Parameter(ParameterSetName = 'Frames')]
    [int[]]$FrameNumbers,

    [Parameter(ParameterSetName = 'Range')]
    [int]$StartFrame = 1,

    [Parameter(ParameterSetName = 'Range')]
    [int]$EndFrame = [int]::MaxValue,

    [Parameter(Mandatory, ParameterSetName = 'Time')]
    [DateTimeOffset]$StartTimeUtc,

    [Parameter(ParameterSetName = 'Time')]
    [DateTimeOffset]$EndTimeUtc = [DateTimeOffset]::MaxValue,

    [ValidateRange(1, 200)]
    [int]$MaximumFrames = 24,

    [ValidateRange(1, 10)]
    [int]$Columns = 4,

    [ValidateRange(240, 808)]
    [int]$CellWidth = 404,

    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'

function Get-PropertyValue {
    param([object]$Value, [string]$Name)

    if ($null -eq $Value) { return $null }
    $property = $Value.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

function Get-DetectorSummary {
    param([object]$Event)

    if ($Event.category -ne 'detector' -or $Event.action.EndsWith('_scores', [System.StringComparison]::OrdinalIgnoreCase)) {
        return $null
    }

    $state = Get-PropertyValue $Event.data 'state'
    if ([string]::IsNullOrWhiteSpace([string]$state) -or $state -eq 'scored') { return $null }
    $confidenceValue = Get-PropertyValue $Event.data 'confidence'
    $confidence = if ($null -ne $confidenceValue) { ' {0:P0}' -f [double]$confidenceValue } else { '' }
    return "$($Event.action): $state$confidence"
}

function Get-ActionSummary {
    param([object]$Event)

    if ($Event.category -eq 'automation' -and $Event.action.EndsWith('_requested', [System.StringComparison]::OrdinalIgnoreCase)) {
        $x = Get-PropertyValue $Event.data 'x'
        $y = Get-PropertyValue $Event.data 'y'
        if ($null -ne $x -and $null -ne $y) { return "$($Event.action.Replace('_requested', '')) ($x,$y)" }

        $key = Get-PropertyValue $Event.data 'key'
        if ($null -eq $key) { $key = Get-PropertyValue $Event.data 'letter' }
        if ($null -ne $key) { return "$($Event.action.Replace('_requested', '')) $key" }

        $deltaX = Get-PropertyValue $Event.data 'delta_x'
        $deltaY = Get-PropertyValue $Event.data 'delta_y'
        if ($null -ne $deltaX -or $null -ne $deltaY) { return "$($Event.action.Replace('_requested', '')) ($deltaX,$deltaY)" }
        return $Event.action.Replace('_requested', '')
    }

    if ($Event.category -eq 'workflow' -and $Event.action -eq 'progress') {
        $message = [string](Get-PropertyValue $Event.data 'message')
        if (-not [string]::IsNullOrWhiteSpace($message)) { return $message }
    }

    return $null
}

function Limit-Text {
    param([string]$Value, [int]$MaximumLength)

    if ([string]::IsNullOrWhiteSpace($Value)) { return '' }
    if ($Value.Length -le $MaximumLength) { return $Value }
    return $Value.Substring(0, [Math]::Max(0, $MaximumLength - 1)) + [char]0x2026
}

function Select-Evenly {
    param([object[]]$Items, [int]$Maximum)

    if ($Items.Count -le $Maximum) { return @($Items) }
    if ($Maximum -eq 1) { return @($Items[0]) }

    $selected = [System.Collections.Generic.List[object]]::new()
    for ($index = 0; $index -lt $Maximum; $index++) {
        $sourceIndex = [int][Math]::Round($index * ($Items.Count - 1) / ($Maximum - 1))
        $selected.Add($Items[$sourceIndex])
    }
    return @($selected)
}

$resolvedInput = [System.IO.Path]::GetFullPath($InputPath)
if (-not (Test-Path -LiteralPath $resolvedInput)) { throw "Diagnostic input does not exist: $resolvedInput" }

$temporaryDirectory = $null
$diagnosticDirectory = $resolvedInput
try {
    if ([System.IO.Path]::GetExtension($resolvedInput).Equals('.zip', [System.StringComparison]::OrdinalIgnoreCase)) {
        $temporaryDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("ExpeditionsMacro-contact-sheet-" + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $temporaryDirectory | Out-Null
        Expand-Archive -LiteralPath $resolvedInput -DestinationPath $temporaryDirectory
        $diagnosticDirectory = $temporaryDirectory
    }

    $eventPath = Join-Path $diagnosticDirectory 'events.jsonl'
    $frameDirectory = Join-Path $diagnosticDirectory 'frames'
    if (-not (Test-Path -LiteralPath $eventPath)) { throw "The diagnostic does not contain events.jsonl: $resolvedInput" }
    if (-not (Test-Path -LiteralPath $frameDirectory)) { throw "The diagnostic does not contain a frames directory: $resolvedInput" }

    $records = [System.Collections.Generic.List[object]]::new()
    $current = $null
    foreach ($line in [System.IO.File]::ReadLines($eventPath)) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $event = $line | ConvertFrom-Json
        if ($event.category -eq 'frame' -and $event.action -eq 'capture_client' -and $event.frame) {
            if ($null -ne $current) { $records.Add($current) }
            $leaf = [System.IO.Path]::GetFileName([string]$event.frame)
            $numberMatch = [regex]::Match($leaf, '(\d+)')
            $current = [pscustomobject]@{
                Number = if ($numberMatch.Success) { [int]$numberMatch.Value } else { $records.Count + 1 }
                Timestamp = [DateTimeOffset]::Parse([string]$event.timestamp_utc, [Globalization.CultureInfo]::InvariantCulture)
                Path = Join-Path $frameDirectory $leaf
                Detector = ''
                Action = ''
            }
            continue
        }

        if ($null -eq $current) { continue }
        $detector = Get-DetectorSummary $event
        if (-not [string]::IsNullOrWhiteSpace($detector)) { $current.Detector = $detector }
        $action = Get-ActionSummary $event
        if (-not [string]::IsNullOrWhiteSpace($action)) { $current.Action = $action }
    }
    if ($null -ne $current) { $records.Add($current) }

    $selected = switch ($PSCmdlet.ParameterSetName) {
        'Frames' {
            $wanted = [System.Collections.Generic.HashSet[int]]::new($FrameNumbers)
            @($records | Where-Object { $wanted.Contains($_.Number) })
            break
        }
        'Time' {
            $startUtc = $StartTimeUtc.ToUniversalTime()
            $endUtc = $EndTimeUtc.ToUniversalTime()
            @($records | Where-Object { $_.Timestamp -ge $startUtc -and $_.Timestamp -le $endUtc })
            break
        }
        default {
            @($records | Where-Object { $_.Number -ge $StartFrame -and $_.Number -le $EndFrame })
        }
    }

    if ($selected.Count -eq 0) { throw 'No captured frames matched the requested selection.' }
    $selected = @(Select-Evenly $selected $MaximumFrames)

    if ([string]::IsNullOrWhiteSpace($OutputPath)) {
        $repository = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
        $outputDirectory = Join-Path $repository 'artifacts\diagnostic-contact-sheets'
        $sourceName = [System.IO.Path]::GetFileNameWithoutExtension($resolvedInput)
        $OutputPath = Join-Path $outputDirectory "$sourceName-$($selected[0].Number)-$($selected[-1].Number).png"
    }
    $resolvedOutput = [System.IO.Path]::GetFullPath($OutputPath)
    $resolvedOutputDirectory = Split-Path -Parent $resolvedOutput
    New-Item -ItemType Directory -Path $resolvedOutputDirectory -Force | Out-Null

    Add-Type -AssemblyName System.Drawing
    $sourceWidth = 808
    $sourceHeight = 611
    $labelHeight = 62
    $imageHeight = [int][Math]::Round($CellWidth * $sourceHeight / $sourceWidth)
    $cellHeight = $imageHeight + $labelHeight
    $rows = [int][Math]::Ceiling($selected.Count / $Columns)
    $sheet = [System.Drawing.Bitmap]::new($Columns * $CellWidth, $rows * $cellHeight)
    $graphics = [System.Drawing.Graphics]::FromImage($sheet)
    $graphics.Clear([System.Drawing.Color]::FromArgb(16, 17, 20))
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $font = [System.Drawing.Font]::new('Consolas', 9, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
    $titleBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
    $detailBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(190, 198, 214))
    try {
        for ($index = 0; $index -lt $selected.Count; $index++) {
            $record = $selected[$index]
            if (-not (Test-Path -LiteralPath $record.Path)) { throw "Captured frame is missing: $($record.Path)" }
            $column = $index % $Columns
            $row = [int][Math]::Floor($index / $Columns)
            $left = $column * $CellWidth
            $top = $row * $cellHeight
            $frame = [System.Drawing.Image]::FromFile($record.Path)
            try {
                $graphics.DrawImage($frame, $left, $top, $CellWidth, $imageHeight)
            }
            finally {
                $frame.Dispose()
            }

            $localTime = $record.Timestamp.ToLocalTime().ToString('HH:mm:ss.fff')
            $graphics.DrawString(("#{0:D6}  {1}" -f $record.Number, $localTime), $font, $titleBrush, $left + 6, $top + $imageHeight + 5)
            $graphics.DrawString((Limit-Text $record.Detector 62), $font, $detailBrush, $left + 6, $top + $imageHeight + 23)
            $graphics.DrawString((Limit-Text $record.Action 62), $font, $detailBrush, $left + 6, $top + $imageHeight + 41)
        }
        $sheet.Save($resolvedOutput, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $font.Dispose()
        $titleBrush.Dispose()
        $detailBrush.Dispose()
        $graphics.Dispose()
        $sheet.Dispose()
    }

    [pscustomobject]@{
        Output = $resolvedOutput
        Frames = $selected.Count
        FirstFrame = $selected[0].Number
        LastFrame = $selected[-1].Number
    }
}
finally {
    if ($null -ne $temporaryDirectory -and (Test-Path -LiteralPath $temporaryDirectory)) {
        $resolvedTemporary = [System.IO.Path]::GetFullPath($temporaryDirectory)
        $systemTemporary = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
        if (-not $resolvedTemporary.StartsWith($systemTemporary, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to remove unexpected temporary directory: $resolvedTemporary"
        }
        Remove-Item -LiteralPath $resolvedTemporary -Recurse -Force
    }
}
