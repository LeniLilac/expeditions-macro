[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$repository = Split-Path -Parent $PSScriptRoot
$assetDirectory = Join-Path $repository 'assets'
$output = Join-Path $assetDirectory 'app.ico'
New-Item -ItemType Directory -Force -Path $assetDirectory | Out-Null

function New-RoundedRectanglePath {
    param([System.Drawing.RectangleF]$Rectangle, [float]$Radius)
    $diameter = $Radius * 2
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($Rectangle.Left, $Rectangle.Top, $diameter, $diameter, 180, 90)
    $path.AddArc($Rectangle.Right - $diameter, $Rectangle.Top, $diameter, $diameter, 270, 90)
    $path.AddArc($Rectangle.Right - $diameter, $Rectangle.Bottom - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($Rectangle.Left, $Rectangle.Bottom - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-IconPng {
    param([int]$Size)
    $bitmap = New-Object System.Drawing.Bitmap $Size, $Size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.Clear([System.Drawing.Color]::Transparent)

        $scale = $Size / 256.0
        $background = New-Object System.Drawing.RectangleF (12 * $scale), (12 * $scale), (232 * $scale), (232 * $scale)
        $backgroundPath = New-RoundedRectanglePath -Rectangle $background -Radius (54 * $scale)
        $backgroundBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#171819'))
        $accentPen = New-Object System.Drawing.Pen ([System.Drawing.ColorTranslator]::FromHtml('#7780FA')), ([Math]::Max(1.0, 12 * $scale))
        $whitePen = New-Object System.Drawing.Pen ([System.Drawing.ColorTranslator]::FromHtml('#F5F6F7')), ([Math]::Max(1.0, 15 * $scale))
        $accentBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#7780FA'))
        $whiteBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#F5F6F7'))
        $darkBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#171819'))
        $centerPen = New-Object System.Drawing.Pen ([System.Drawing.ColorTranslator]::FromHtml('#F5F6F7')), ([Math]::Max(1.0, 7 * $scale))
        try {
            $graphics.FillPath($backgroundBrush, $backgroundPath)
            $graphics.DrawPath($accentPen, $backgroundPath)
            $graphics.DrawEllipse($whitePen, 62 * $scale, 62 * $scale, 132 * $scale, 132 * $scale)

            $north = [System.Drawing.PointF[]]@(
                (New-Object System.Drawing.PointF (145 * $scale), (82 * $scale)),
                (New-Object System.Drawing.PointF (116 * $scale), (143 * $scale)),
                (New-Object System.Drawing.PointF (174 * $scale), (115 * $scale)))
            $south = [System.Drawing.PointF[]]@(
                (New-Object System.Drawing.PointF (111 * $scale), (174 * $scale)),
                (New-Object System.Drawing.PointF (140 * $scale), (113 * $scale)),
                (New-Object System.Drawing.PointF (82 * $scale), (141 * $scale)))
            $graphics.FillPolygon($accentBrush, $north)
            $graphics.FillPolygon($whiteBrush, $south)
            $graphics.FillEllipse($darkBrush, 116 * $scale, 116 * $scale, 24 * $scale, 24 * $scale)
            $graphics.DrawEllipse($centerPen, 116 * $scale, 116 * $scale, 24 * $scale, 24 * $scale)
        }
        finally {
            $backgroundPath.Dispose()
            $backgroundBrush.Dispose()
            $accentPen.Dispose()
            $whitePen.Dispose()
            $accentBrush.Dispose()
            $whiteBrush.Dispose()
            $darkBrush.Dispose()
            $centerPen.Dispose()
        }

        $stream = New-Object System.IO.MemoryStream
        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        return ,$stream.ToArray()
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
[System.Collections.Generic.List[byte[]]]$images = @()
foreach ($size in $sizes) {
    [byte[]]$png = New-IconPng -Size $size
    $images.Add($png)
}
[System.IO.File]::WriteAllBytes((Join-Path $assetDirectory 'app-preview.png'), $images[$images.Count - 1])
$stream = New-Object System.IO.FileStream $output, ([System.IO.FileMode]::Create), ([System.IO.FileAccess]::Write)
$writer = New-Object System.IO.BinaryWriter $stream
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($index = 0; $index -lt $sizes.Count; $index++) {
        $size = $sizes[$index]
        $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
        $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$images[$index].Length)
        $writer.Write([uint32]$offset)
        $offset += $images[$index].Length
    }
    foreach ($image in $images) { $writer.Write($image) }
}
finally {
    $writer.Dispose()
    $stream.Dispose()
}

Write-Host "Generated $output"
