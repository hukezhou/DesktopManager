<#
.SYNOPSIS
    App icon generator: rounded square + flat green + white running-script glyph, multi-size .ico.

.DESCRIPTION
    The glyph is taken as vector geometry (FormattedText.BuildGeometry), not drawn as text, and is
    centred on its INK bounds (running-script glyphs have uneven side bearings).
    Every size is rendered at 4x supersampling and then downscaled with HighQuality.
    ICO layout: sizes <= 48 use uncompressed 32bpp BGRA DIB + AND mask (best compatibility),
    sizes >= 64 use PNG entries.

    NOTE: keep this file ASCII-only. Windows PowerShell 5.1 reads .ps1 as ANSI, and UTF-8 Chinese
    comment bytes decode into stray GBK characters (including braces/quotes) that break parsing.

.EXAMPLE
    powershell -STA -File tools\icon-gen\MakeIcon.ps1 -Preview
    powershell -STA -File tools\icon-gen\MakeIcon.ps1 -Out src\DesktopManager\Assets\app.ico
#>
param(
    [string]$Out = "",
    [string]$Png = "",                    # write one master PNG instead of / in addition to the ico
    [int]$PngSize = 256,
    [string]$Dump = "",                   # extract every ico entry of the generated file into this folder as PNG
    [switch]$Preview,
    [string]$PreviewPath = "",
    [string]$Char = "0x72D0",          # HU (fox)
    [string]$Font = "STXingkai",       # ST Xingkai (running script)
    [string]$Green = "#07C160",
    [string]$GlyphColor = "#FFFFFF",
    [double]$RadiusPct = 0.22,         # corner radius / side
    [string]$FitBy = "max",           # which ink dimension equals GlyphPct: height | width | max
    [double]$GlyphPct = 0.72,         # glyph ink size / side (which dimension: see FitBy)
    [double]$GlyphPctSmall = 0.82,    # bigger glyph for sizes <= SmallMax
    [double]$SmallStroke = 0.4,       # outline thickening at 16px, scales with size
    [int]$SmallMax = 24,
    [int]$Sup = 1,                      # supersampling factor (1 = rasterise directly, no resample ringing)
    [string]$ScaleMode = "HighQuality", # HighQuality | linear | nearest (only used when Sup > 1)
    [switch]$Probe,                     # print corner/centre alpha per size (catches resample ringing)
    [double]$NudgeX = 0,               # optical centring nudge, in 256-canvas pixels
    [double]$NudgeY = 0,
    [int[]]$Sizes = @(16, 20, 24, 32, 40, 48, 64, 96, 128, 256)
)

Add-Type -AssemblyName PresentationCore, WindowsBase, System.Drawing

$ch = [char][Convert]::ToInt32($Char, 16)
$greenBrush = New-Object System.Windows.Media.SolidColorBrush(
    [System.Windows.Media.ColorConverter]::ConvertFromString($Green))
$whiteBrush = New-Object System.Windows.Media.SolidColorBrush(
    [System.Windows.Media.ColorConverter]::ConvertFromString($GlyphColor))
$greenBrush.Freeze(); $whiteBrush.Freeze()

# resolve the font family object from the system list (CJK names often need this)
$script:FontFamilyObj = $null
function Resolve-FontFamily {
    if ($script:FontFamilyObj) { return $script:FontFamilyObj }
    foreach ($f in [System.Windows.Media.Fonts]::SystemFontFamilies) {
        if ($f.Source -ieq $Font) { $script:FontFamilyObj = $f; return $f }
    }
    foreach ($f in [System.Windows.Media.Fonts]::SystemFontFamilies) {
        foreach ($n in $f.FamilyNames.Values) { if ($n -ieq $Font) { $script:FontFamilyObj = $f; return $f } }
    }
    throw "font '$Font' not installed"
}

# ---- glyph geometry (em = 1000, scaled later by ink height) ----
$script:GlyphGeo = $null
function Get-GlyphGeometry {
    if ($script:GlyphGeo) { return $script:GlyphGeo }
    $family = Resolve-FontFamily
    $tf = New-Object System.Windows.Media.Typeface($family)
    $ft = New-Object System.Windows.Media.FormattedText(
        [string]$ch,
        [System.Globalization.CultureInfo]::InvariantCulture,
        [System.Windows.FlowDirection]::LeftToRight,
        $tf,
        1000.0,
        $whiteBrush)
    $geo = $ft.BuildGeometry((New-Object System.Windows.Point(0, 0)))
    if ($geo.Bounds.Width -lt 1 -or $geo.Bounds.Height -lt 1) { throw "glyph rendered empty in '$Font'" }
    $geo.Freeze()
    $script:GlyphGeo = $geo
    return $geo
}

# ---- build a size x size drawing ----
function New-IconDrawing([int]$size, [bool]$small) {
    $geo = Get-GlyphGeometry
    $b = $geo.Bounds

    $pct = if ($small -and $size -le $SmallMax) { $GlyphPctSmall } else { $GlyphPct }
    $fit = switch ($FitBy) {
        'max' { [Math]::Max($b.Width, $b.Height) }
        'width' { $b.Width }
        default { $b.Height }
    }
    $scale = ($size * $pct) / $fit
    $radius = $size * $RadiusPct

    $xform = New-Object System.Windows.Media.TransformGroup
    $xform.Children.Add((New-Object System.Windows.Media.TranslateTransform(-($b.X + $b.Width / 2), -($b.Y + $b.Height / 2))))
    $xform.Children.Add((New-Object System.Windows.Media.ScaleTransform($scale, $scale)))
    $xform.Children.Add((New-Object System.Windows.Media.TranslateTransform(
        ($size / 2 + $NudgeX * $size / 256), ($size / 2 + $NudgeY * $size / 256))))
    $xform.Freeze()

    $glyph = $geo.Clone()
    $glyph.Transform = $xform
    $glyph.Freeze()

    $pen = $null
    if ($small -and $size -le $SmallMax -and $SmallStroke -gt 0) {
        $pen = New-Object System.Windows.Media.Pen($whiteBrush, ($SmallStroke * $size / 16))
        $pen.Freeze()
    }

    $dg = New-Object System.Windows.Media.DrawingGroup
    $bg = New-Object System.Windows.Media.RectangleGeometry(
        (New-Object System.Windows.Rect(0, 0, $size, $size)), $radius, $radius)
    $bg.Freeze()
    $dg.Children.Add((New-Object System.Windows.Media.GeometryDrawing($greenBrush, $null, $bg)))
    $dg.Children.Add((New-Object System.Windows.Media.GeometryDrawing($whiteBrush, $pen, $glyph)))
    return $dg
}

# ---- render: optional supersampling + downscale; Sup=1 rasterises the vector at the target size ----
function Render-Icon([int]$size, [bool]$small) {
    if ($Sup -le 1) {
        $dv = New-Object System.Windows.Media.DrawingVisual
        $dc = $dv.RenderOpen()
        $dc.DrawDrawing((New-IconDrawing $size $small))
        $dc.Close()
        $rtb = New-Object System.Windows.Media.Imaging.RenderTargetBitmap($size, $size, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
        $rtb.Render($dv)
        return $rtb
    }

    $dv = New-Object System.Windows.Media.DrawingVisual
    $dc = $dv.RenderOpen()
    $dc.DrawDrawing((New-IconDrawing ($size * $Sup) $small))
    $dc.Close()
    $big = New-Object System.Windows.Media.Imaging.RenderTargetBitmap(
        ($size * $Sup), ($size * $Sup), 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $big.Render($dv)

    $mode = [System.Windows.Media.BitmapScalingMode]::HighQuality
    if ($ScaleMode -ieq "linear") { $mode = [System.Windows.Media.BitmapScalingMode]::Linear }
    if ($ScaleMode -ieq "nearest") { $mode = [System.Windows.Media.BitmapScalingMode]::NearestNeighbor }

    $dv2 = New-Object System.Windows.Media.DrawingVisual
    [System.Windows.Media.RenderOptions]::SetBitmapScalingMode($dv2, $mode)
    $dc2 = $dv2.RenderOpen()
    $dc2.DrawImage($big, (New-Object System.Windows.Rect(0, 0, $size, $size)))
    $dc2.Close()
    $rtb = New-Object System.Windows.Media.Imaging.RenderTargetBitmap($size, $size, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $rtb.Render($dv2)
    return $rtb
}

function Get-PngBytes($src) {
    $enc = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $enc.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($src))
    $ms = New-Object System.IO.MemoryStream
    $enc.Save($ms)
    return $ms.ToArray()
}

# Reads pixels through GDI+ (BitmapSource.CopyPixels mis-reports the required buffer size here).
# Returns @{ Bytes; Stride; Width; Height }, top-down BGRA.
function Get-PixelBuffer($src) {
    $ms = New-Object System.IO.MemoryStream(,(Get-PngBytes $src))
    $tmp = New-Object System.Drawing.Bitmap($ms)
    $bmp = New-Object System.Drawing.Bitmap($tmp)
    $tmp.Dispose(); $ms.Dispose()
    $rect = New-Object System.Drawing.Rectangle(0, 0, $bmp.Width, $bmp.Height)
    $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
                          [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $buf = New-Object byte[] ($data.Stride * $bmp.Height)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $buf, 0, $buf.Length)
    $bmp.UnlockBits($data)
    $bmp.Dispose()
    return @{ Bytes = $buf; Stride = $data.Stride; Width = $rect.Width; Height = $rect.Height }
}

# ---- ICO container ----
function New-DibEntry([int]$size, $px) {
    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter($ms)
    $bw.Write([uint32]40)                 # BITMAPINFOHEADER (must be exactly 40 bytes)
    $bw.Write([int32]$size)               # biWidth
    $bw.Write([int32]($size * 2))         # biHeight = XOR + AND
    $bw.Write([uint16]1)                  # biPlanes
    $bw.Write([uint16]32)                 # biBitCount
    $bw.Write([uint32]0)                  # biCompression = BI_RGB
    $bw.Write([uint32]0)                  # biSizeImage
    $bw.Write([uint32]0)                  # biXPelsPerMeter
    $bw.Write([uint32]0)                  # biYPelsPerMeter
    $bw.Write([uint32]0)                  # biClrUsed
    $bw.Write([uint32]0)                  # biClrImportant
    $rowBytes = $size * 4
    for ($y = $size - 1; $y -ge 0; $y--) {
        $row = New-Object byte[] $rowBytes
        [Array]::Copy($px.Bytes, ($y * $px.Stride), $row, 0, $rowBytes)
        $ms.Write($row, 0, $rowBytes)     # bottom-up BGRA (Stream.Write, not BinaryWriter.Write: PS binds byte[] to Write(byte))
    }
    $maskStride = [int]([Math]::Ceiling($size / 32.0) * 4)
    $mask = New-Object byte[] ($maskStride * $size)
    $ms.Write($mask, 0, $mask.Length)     # AND mask all zero, alpha is enough
    $bw.Flush()
    return $ms.ToArray()
}

function Write-Ico([string]$path, $entries) {
    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter($ms)
    $bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$entries.Count)
    $offset = 6 + 16 * $entries.Count
    foreach ($e in $entries) {
        $dim = if ($e.size -ge 256) { 0 } else { $e.size }
        $bw.Write([byte]$dim); $bw.Write([byte]$dim)
        $bw.Write([byte]0); $bw.Write([byte]0)          # bReserved + bBytesInRes(palette), entry must be 16 bytes
        $bw.Write([uint16]1); $bw.Write([uint16]32)
        $bw.Write([uint32]$e.bytes.Length); $bw.Write([uint32]$offset)
        $offset += $e.bytes.Length
    }
    foreach ($e in $entries) { $ms.Write($e.bytes, 0, $e.bytes.Length) }
    $bw.Flush()
    $dir = Split-Path -Parent $path
    if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    [System.IO.File]::WriteAllBytes($path, $ms.ToArray())
}

# ---- preview montage ----
function New-Preview([string]$path) {
    $cell = 300
    $show = @(128, 64, 48, 32, 24, 20, 16)
    $cvW = $cell * $show.Count
    $cvH = $cell * 3
    $bmp = New-Object System.Drawing.Bitmap $cvW, $cvH
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::FromArgb(255, 128, 128, 128))
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $font = New-Object System.Drawing.Font("Segoe UI", 9)
    $label = [System.Drawing.Brushes]::White

    # row 0 = 1x actual, row 1 = 4x zoom (plain), row 2 = 4x zoom (small-size boost)
    for ($i = 0; $i -lt $show.Count; $i++) {
        $s = $show[$i]
        $png = Get-PngBytes (Render-Icon $s $false)
        $ms = New-Object System.IO.MemoryStream(,$png)
        $img = [System.Drawing.Image]::FromStream($ms)
        $x = $i * $cell + [int](($cell - $s) / 2)
        $g.DrawImage($img, $x, [int](($cell - $s) / 2), $s, $s)
        $g.DrawImage($img, ($i * $cell + [int](($cell - $s * 4) / 2)), ($cell + [int](($cell - $s * 4) / 2)), ($s * 4), ($s * 4))
        $g.DrawString(("$s px"), $font, $label, ($i * $cell + 8), ($cell * 2 + 6))
        $img.Dispose(); $ms.Dispose()

        $png2 = Get-PngBytes (Render-Icon $s $true)
        $ms2 = New-Object System.IO.MemoryStream(,$png2)
        $img2 = [System.Drawing.Image]::FromStream($ms2)
        $g.DrawImage($img2, ($i * $cell + [int](($cell - $s * 4) / 2)), ($cell * 2 + [int](($cell - $s * 4) / 2)), ($s * 4), ($s * 4))
        $img2.Dispose(); $ms2.Dispose()
    }
    $g.DrawString("row1 = 1x actual   row2 = 4x zoom (plain)   row3 = 4x zoom (small boost: bigger glyph + stroke)", $font, $label, 8, ($cvH - 22))
    $g.Dispose()
    $dir = Split-Path -Parent $path
    if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Output "preview: $path"
}

# ---- main ----
$geo = Get-GlyphGeometry
$b = $geo.Bounds
$fit = switch ($FitBy) {
    'max' { [Math]::Max($b.Width, $b.Height) }
    'width' { $b.Width }
    default { $b.Height }
}
$k = 256 * $GlyphPct / $fit
Write-Output ("font = " + $Font + "  char = U+" + ([int]$ch).ToString("X4") +
              "  ink(em=1000) = " + [math]::Round($b.Width, 1) + " x " + [math]::Round($b.Height, 1) +
              "  aspect(w/h) = " + [math]::Round($b.Width / $b.Height, 3) +
              "  fitBy = " + $FitBy)
Write-Output ("at 256 canvas: ink = " + [math]::Round($b.Width * $k, 1) + " x " + [math]::Round($b.Height * $k, 1) +
              "  radius = " + [math]::Round(256 * $RadiusPct, 1))

if ($Preview) {
    $pv = if ($PreviewPath) { $PreviewPath } else { "$PSScriptRoot\icon-preview.png" }
    New-Preview $pv
}

if ($Png -ne "") {
    $src = Render-Icon $PngSize $false
    $dir = Split-Path -Parent $Png
    if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    [System.IO.File]::WriteAllBytes($Png, (Get-PngBytes $src))
    Write-Output ("png: $Png ($PngSize px)")
}

function Test-Ico([string]$path) {
    # 1) container directory
    $bytes = [System.IO.File]::ReadAllBytes($path)
    $ms = New-Object System.IO.MemoryStream(,$bytes)
    $br = New-Object System.IO.BinaryReader($ms)
    $null = $br.ReadUInt16()
    $type = $br.ReadUInt16(); $count = $br.ReadUInt16()
    Write-Output ("verify: reserved=0 type=" + $type + " count=" + $count)
    $ents = @()
    for ($i = 0; $i -lt $count; $i++) {
        $w = [int]$br.ReadByte(); $h = [int]$br.ReadByte()
        $null = $br.ReadByte(); $null = $br.ReadByte()
        $null = $br.ReadUInt16(); $bpp = [int]$br.ReadUInt16()
        $sz = [int]$br.ReadUInt32(); $off = [int]$br.ReadUInt32()
        if ($w -eq 0) { $w = 256 }
        if ($h -eq 0) { $h = 256 }
        $ents += @{ w = $w; h = $h; bpp = $bpp; size = $sz; off = $off }
    }
    foreach ($e in $ents) {
        $ms.Position = $e.off
        $kind = if ($ms.ReadByte() -eq 0x89) { "PNG" } else { "DIB" }
        $end = $e.off + $e.size
        Write-Output ("  " + $e.w + "x" + $e.h + " " + $kind + " bpp=" + $e.bpp + " " + $e.size + "B @ " + $e.off +
                      " fits=" + ($end -le $bytes.Length))
        if ($kind -eq "DIB") {
            # header must be exactly 40 bytes and the entry size must match XOR + AND mask
            $biSize = [BitConverter]::ToUInt32($bytes, $e.off)
            $biW = [BitConverter]::ToInt32($bytes, $e.off + 4)
            $biH = [BitConverter]::ToInt32($bytes, $e.off + 8)
            $maskStride = [int]([Math]::Ceiling($e.w / 32.0) * 4)
            $expect = 40 + ($e.w * 4 * $e.h) + ($maskStride * $e.h)
            $ok = ($biSize -eq 40) -and ($biW -eq $e.w) -and ($biH -eq (2 * $e.h)) -and ($expect -eq $e.size)
            Write-Output ("      dib hdr=" + $biSize + " w=" + $biW + " h=" + $biH +
                          " entry=" + $e.size + " expected=" + $expect + " layout=" + $ok)
            # wrap the DIB in a BMP header (height set to single) so GDI+ can prove the pixels are laid out right
            $dib = New-Object byte[] $e.size
            [Array]::Copy($bytes, $e.off, $dib, 0, $e.size)
            [Array]::Copy([BitConverter]::GetBytes([int32]$e.h), 0, $dib, 8, 4)   # biHeight lives at offset 8
            $hdr = New-Object byte[] 14
            $hdr[0] = 0x42; $hdr[1] = 0x4D
            [Array]::Copy([BitConverter]::GetBytes([int32](14 + $e.size)), 0, $hdr, 2, 4)
            [Array]::Copy([BitConverter]::GetBytes([int32]14), 0, $hdr, 10, 4)
            $ms2 = New-Object System.IO.MemoryStream(,($hdr + $dib))
            try {
                $t = New-Object System.Drawing.Bitmap($ms2)
                $bmp = New-Object System.Drawing.Bitmap($t)
                $t.Dispose()
                Write-Output ("      dib-decode " + $bmp.Width + "x" + $bmp.Height)
                $bmp.Dispose()
            } catch { Write-Output ("      dib-decode FAILED " + $_.Exception.Message) }
            $ms2.Dispose()
        }
    }

    # 2) WPF must be able to decode it (this is what Window.Icon uses)
    try {
        $dec = New-Object System.Windows.Media.Imaging.IconBitmapDecoder(
            (New-Object System.Uri($path)),
            [System.Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat,
            [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad)
        $dims = @()
        foreach ($f in $dec.Frames) { $dims += ("" + $f.PixelWidth + "x" + $f.PixelHeight) }
        Write-Output ("verify wpf: frames=" + $dec.Frames.Count + " [" + ($dims -join " ") + "]")
    } catch { Write-Output ("verify wpf: FAILED " + $_.Exception.Message) }

    # 3) GDI+ must be able to decode it (this is what the shell / taskbar use)
    try {
        $ico = New-Object System.Drawing.Icon($path)
        Write-Output ("verify gdi: " + $ico.Width + "x" + $ico.Height)
        $ico.Dispose()
    } catch { Write-Output ("verify gdi: FAILED " + $_.Exception.Message) }
}

if ($Probe) {
    foreach ($s in ($Sizes | Sort-Object)) {
        $px = Get-PixelBuffer (Render-Icon $s $true)
        $st = $px.Stride
        $a00 = [int]$px.Bytes[3]
        $a11 = [int]$px.Bytes[$st + 7]
        $ac = [int]$px.Bytes[([int]($s / 2) * $st) + ([int]($s / 2) * 4) + 3]
        Write-Output ("probe " + $s + "px sup=" + $Sup + " mode=" + $ScaleMode +
                      " corner(0,0)=" + $a00 + " corner(1,1)=" + $a11 + " centre=" + $ac)
    }
}

function Save-IcoEntries([string]$path, [string]$dir) {
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    $bytes = [System.IO.File]::ReadAllBytes($path)
    $ms = New-Object System.IO.MemoryStream(,$bytes)
    $br = New-Object System.IO.BinaryReader($ms)
    $null = $br.ReadUInt16()
    $null = $br.ReadUInt16(); $count = [int]$br.ReadUInt16()
    for ($i = 0; $i -lt $count; $i++) {
        $w = [int]$br.ReadByte(); $h = [int]$br.ReadByte()
        $null = $br.ReadByte(); $null = $br.ReadByte()
        $null = $br.ReadUInt16(); $null = $br.ReadUInt16()
        $sz = [int]$br.ReadUInt32(); $off = [int]$br.ReadUInt32()
        if ($w -eq 0) { $w = 256 }
        if ($h -eq 0) { $h = 256 }
        $payload = New-Object byte[] $sz
        [Array]::Copy($bytes, $off, $payload, 0, $sz)
        $out = Join-Path $dir ($w.ToString("000") + ".png")
        if ($payload[0] -eq 0x89) {
            [System.IO.File]::WriteAllBytes($out, $payload)
        } else {
            # decode the DIB by hand: bottom-up BGRA -> top-down, alpha preserved
            $stride = $w * 4
            $px = New-Object byte[] ($stride * $h)
            for ($y = 0; $y -lt $h; $y++) {
                [Array]::Copy($payload, (40 + ((($h - 1) - $y) * $stride)), $px, ($y * $stride), $stride)
            }
            $bs = [System.Windows.Media.Imaging.BitmapSource]::Create(
                $w, $h, 96, 96, [System.Windows.Media.PixelFormats]::Bgra32, $null, $px, $stride)
            $enc = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
            $enc.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bs))
            $fs = [System.IO.File]::Create($out)
            $enc.Save($fs); $fs.Close()
            # alpha sanity: corner must be transparent, centre must be opaque
            $aCorner = [int]$px[3]
            $aCentre = [int]$px[([int]($h / 2) * $stride) + ([int]($w / 2) * 4) + 3]
            Write-Output ("      alpha corner=" + $aCorner + " centre=" + $aCentre)
        }
        Write-Output ("  dumped " + $w + "x" + $h + " -> " + $out)
    }
}

if ($Out -ne "") {
    $entries = @()
    foreach ($s in ($Sizes | Sort-Object)) {
        # $true enables the <= SmallMax treatment; the function itself gates on the size
        $src = Render-Icon $s $true
        if ($s -le 48) {
            $bytes = New-DibEntry $s (Get-PixelBuffer $src)
            $kind = "DIB"
        } else {
            $bytes = Get-PngBytes $src
            $kind = "PNG"
        }
        $entries += @{ size = $s; bytes = $bytes }
        Write-Output ("  entry " + $s + "x" + $s + " " + $kind + " " + $bytes.Length + " bytes")
    }
    Write-Ico $Out $entries
    Write-Output ("ico: " + $Out + "  (" + (Get-Item $Out).Length + " bytes)")
    Test-Ico ([System.IO.Path]::GetFullPath($Out))
    if ($Dump -ne "") { Save-IcoEntries ([System.IO.Path]::GetFullPath($Out)) ([System.IO.Path]::GetFullPath($Dump)) }
}
