$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assets = Join-Path (Split-Path $PSScriptRoot -Parent) 'VideoGrabber/Assets'
[void][IO.Directory]::CreateDirectory($assets)
$frames = @()
foreach ($size in @(16, 24, 32, 48, 64, 128, 256)) {
    $bitmap = [Drawing.Bitmap]::new(256, 256)
    $g = [Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([Drawing.Color]::Transparent)
    $path = [Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddArc(8, 8, 72, 72, 180, 90)
    $path.AddArc(176, 8, 72, 72, 270, 90)
    $path.AddArc(176, 176, 72, 72, 0, 90)
    $path.AddArc(8, 176, 72, 72, 90, 90)
    $path.CloseFigure()
    $blue = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#2168E8'))
    $g.FillPath($blue, $path)
    # Play symbol and download arrow remain legible at small icon sizes.
    $g.FillPolygon([Drawing.Brushes]::White, [Drawing.Point[]]@(
        [Drawing.Point]::new(64, 55), [Drawing.Point]::new(64, 165), [Drawing.Point]::new(145, 110)))
    $mint = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#A7F3D0'))
    $g.FillPolygon($mint, [Drawing.Point[]]@(
        [Drawing.Point]::new(170, 91), [Drawing.Point]::new(194, 91),
        [Drawing.Point]::new(194, 151), [Drawing.Point]::new(216, 151),
        [Drawing.Point]::new(182, 188), [Drawing.Point]::new(148, 151),
        [Drawing.Point]::new(170, 151)))
    $g.FillRectangle([Drawing.Brushes]::White, 57, 202, 152, 12)
    $scaled = [Drawing.Bitmap]::new($size, $size)
    $sg = [Drawing.Graphics]::FromImage($scaled)
    $sg.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $sg.DrawImage($bitmap, 0, 0, $size, $size)
    $stream = [IO.MemoryStream]::new()
    $scaled.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
    $frames += [pscustomobject]@{ Size = $size; Bytes = $stream.ToArray() }
    if ($size -eq 256) { $scaled.Save((Join-Path $assets 'VideoGrabber.png'), [Drawing.Imaging.ImageFormat]::Png) }
    $stream.Dispose(); $sg.Dispose(); $scaled.Dispose()
    $mint.Dispose(); $blue.Dispose(); $path.Dispose(); $g.Dispose(); $bitmap.Dispose()
}
$file = [IO.File]::Create((Join-Path $assets 'VideoGrabber.ico'))
$writer = [IO.BinaryWriter]::new($file)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    foreach ($frame in $frames) {
        $dimension = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$frame.Bytes.Length); $writer.Write([uint32]$offset)
        $offset += $frame.Bytes.Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Bytes) }
} finally { $writer.Dispose(); $file.Dispose() }
