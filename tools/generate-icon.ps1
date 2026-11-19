# Draws the application icon (a speech bubble on a dark rounded tile) and writes a multi-size .ico with PNG frames.
# Output: src/Cassandra.App/Assets/cassandra.ico (committed; rerun this script to change the design).
Add-Type -AssemblyName System.Drawing

$repo = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $repo 'src\Cassandra.App\Assets'
New-Item -ItemType Directory $outDir -Force | Out-Null

function New-Frame([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)
    $s = $size / 256.0

    function Round-Rect($x, $y, $w, $h, $r) {
        $p = New-Object System.Drawing.Drawing2D.GraphicsPath
        $d = 2 * $r
        $p.AddArc($x, $y, $d, $d, 180, 90); $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
        $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90); $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
        $p.CloseFigure(); return $p
    }

    # tile
    $tile = Round-Rect (8 * $s) (8 * $s) (240 * $s) (240 * $s) (52 * $s)
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.PointF]::new(0, 0)), ([System.Drawing.PointF]::new($size, $size)), ([System.Drawing.Color]::FromArgb(255, 30, 41, 66)), ([System.Drawing.Color]::FromArgb(255, 14, 18, 30))
    $g.FillPath($bg, $tile)

    # speech bubble with a tail
    $bubble = Round-Rect (46 * $s) (58 * $s) (164 * $s) (112 * $s) (28 * $s)
    $tail = New-Object System.Drawing.Drawing2D.GraphicsPath
    $tail.AddPolygon([System.Drawing.PointF[]]@([System.Drawing.PointF]::new(84 * $s, 164 * $s), [System.Drawing.PointF]::new(84 * $s, 208 * $s), [System.Drawing.PointF]::new(132 * $s, 166 * $s)))
    $accent = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 127, 209, 255))
    $g.FillPath($accent, $bubble)
    $g.FillPath($accent, $tail)

    # two text lines inside the bubble
    $ink = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 20, 28, 48))
    $g.FillPath($ink, (Round-Rect (74 * $s) (88 * $s) (108 * $s) (14 * $s) (7 * $s)))
    $g.FillPath($ink, (Round-Rect (74 * $s) (120 * $s) (76 * $s) (14 * $s) (7 * $s)))

    $g.Dispose()
    return $bmp
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$frames = foreach ($size in $sizes) {
    $bmp = New-Frame $size
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    [pscustomobject]@{ Size = $size; Bytes = $ms.ToArray() }
}

$stream = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $stream
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$frames.Count)
$offset = 6 + 16 * $frames.Count
foreach ($f in $frames) {
    $dim = if ($f.Size -ge 256) { 0 } else { $f.Size }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$f.Bytes.Length); $w.Write([uint32]$offset)
    $offset += $f.Bytes.Length
}
foreach ($f in $frames) { $w.Write($f.Bytes) }
[System.IO.File]::WriteAllBytes((Join-Path $outDir 'cassandra.ico'), $stream.ToArray())

# a PNG preview of the largest frame next to the icon (not committed; see .gitignore)
[System.IO.File]::WriteAllBytes((Join-Path $outDir 'cassandra-preview.png'), ($frames | Where-Object Size -eq 256).Bytes)
Write-Host "Wrote $outDir\cassandra.ico ($($frames.Count) sizes)"
