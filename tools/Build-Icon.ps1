param(
    [string]$Source = "$PSScriptRoot\..\assets\scrapbook.png",
    [string]$Destination = "$PSScriptRoot\..\assets\scrapbook.ico"
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$original = [System.Drawing.Bitmap]::new((Resolve-Path $Source).Path)
try {
    $sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
    $frames = foreach ($size in $sizes) {
        $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $memory = [System.IO.MemoryStream]::new()
        try {
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.DrawImage($original, [System.Drawing.Rectangle]::new(0, 0, $size, $size))
            $bitmap.Save($memory, [System.Drawing.Imaging.ImageFormat]::Png)
            [pscustomobject]@{ Size = $size; Bytes = $memory.ToArray() }
        } finally { $memory.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
    }
    $file = [System.IO.File]::Create([System.IO.Path]::GetFullPath($Destination))
    $writer = [System.IO.BinaryWriter]::new($file)
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
    } finally { $writer.Dispose() }
    Write-Output "Created ICO sizes: $($sizes -join ', ')"
    Write-Output "Source corner alpha: $($original.GetPixel(0,0).A)"
} finally { $original.Dispose() }
