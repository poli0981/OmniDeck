<#
.SYNOPSIS
    Draws OmniDeck's application icon: the monogram "O" on a blue rounded square, as the tray icon
    draws it (src/OmniDeck.App/Shell/OdTrayIcon.cs), at the sizes Windows asks for.

.DESCRIPTION
    Writes src/OmniDeck.App/Assets/OmniDeck.ico, which the executable, its windows and Velopack's
    Setup use. It stands in until a designed icon exists: replacing that file is enough, and this
    script need not run again. WPF draws the images, so run it in an STA thread, as pwsh does.

.EXAMPLE
    pwsh scripts/make-icon.ps1
#>
[CmdletBinding()]
param(
    [string]$OutFile = (Join-Path $PSScriptRoot '..\src\OmniDeck.App\Assets\OmniDeck.ico')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase

# Windows' default accent, which the tray icon also falls back to.
$background = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(0x00, 0x67, 0xC0))
$foreground = [System.Windows.Media.Brushes]::White
$sizes = @(16, 20, 24, 32, 40, 48, 64, 256)

function New-IconImage([int]$Size) {
    $visual = [System.Windows.Media.DrawingVisual]::new()
    $context = $visual.RenderOpen()
    $corner = $Size / 8.0
    $context.DrawRoundedRectangle($background, $null, [System.Windows.Rect]::new(0, 0, $Size, $Size), $corner, $corner)

    $typeface = [System.Windows.Media.Typeface]::new(
        [System.Windows.SystemFonts]::MessageFontFamily,
        [System.Windows.FontStyles]::Normal,
        [System.Windows.FontWeights]::Bold,
        [System.Windows.FontStretches]::Normal)
    $text = [System.Windows.Media.FormattedText]::new(
        'O',
        [Globalization.CultureInfo]::InvariantCulture,
        [System.Windows.FlowDirection]::LeftToRight,
        $typeface,
        $Size * 0.8,
        $foreground,
        1.0)
    $glyph = $text.BuildGeometry([System.Windows.Point]::new(0, 0))
    $bounds = $glyph.Bounds
    $context.PushTransform([System.Windows.Media.TranslateTransform]::new((($Size - $bounds.Width) / 2) - $bounds.X, (($Size - $bounds.Height) / 2) - $bounds.Y))
    $context.DrawGeometry($foreground, $null, $glyph)
    $context.Pop()
    $context.Close()

    $bitmap = [System.Windows.Media.Imaging.RenderTargetBitmap]::new($Size, $Size, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $png = [IO.MemoryStream]::new()
    $encoder.Save($png)
    return , $png.ToArray()
}

$images = foreach ($size in $sizes) { , (New-IconImage $size) }

# ICO: the directory, one entry per image, then the images, PNG-compressed (Windows reads those).
$ico = [IO.MemoryStream]::new()
$writer = [IO.BinaryWriter]::new($ico)
$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$sizes.Count)
$offset = 6 + (16 * $sizes.Count)
for ($i = 0; $i -lt $sizes.Count; $i++) {
    # Width and height are one byte each: 0 means 256.
    $dimension = [byte]($sizes[$i] % 256)
    $writer.Write($dimension)
    $writer.Write($dimension)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]$images[$i].Length)
    $writer.Write([uint32]$offset)
    $offset += $images[$i].Length
}

foreach ($image in $images) {
    $writer.Write([byte[]]$image)
}

$writer.Flush()
$OutFile = [IO.Path]::GetFullPath($OutFile)
New-Item -ItemType Directory -Force -Path (Split-Path $OutFile) | Out-Null
[IO.File]::WriteAllBytes($OutFile, $ico.ToArray())
Write-Host "Wrote $OutFile ($($sizes -join ', ') px)"
