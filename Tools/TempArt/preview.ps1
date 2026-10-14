# 그림 여러 장을 검은 바탕에 확대해서 한 장으로 붙인다(눈으로 확인용).
# 사용: preview.ps1 -Out sheet.png -Scale 6 -Cols 8 -Files a.png,b.png
param([string]$Out, [int]$Scale = 6, [int]$Cols = 8, [string[]]$Files, [int]$Cell = 0)
Add-Type -AssemblyName System.Drawing
$imgs = @()
foreach ($f in $Files) { $imgs += [System.Drawing.Bitmap]::FromFile((Resolve-Path $f)) }
if ($Cell -eq 0) { foreach ($i in $imgs) { if ($i.Width -gt $Cell) { $Cell = $i.Width }; if ($i.Height -gt $Cell) { $Cell = $i.Height } } }
$pad = 2
$cw = ($Cell + $pad) * $Scale
$rows = [math]::Ceiling($imgs.Count / $Cols)
$bmp = New-Object System.Drawing.Bitmap ([int]($cw * $Cols)), ([int]($cw * $rows))
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::FromArgb(255, 40, 40, 40))
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$black = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::Black)
for ($n = 0; $n -lt $imgs.Count; $n++) {
    $i = $imgs[$n]
    $x = ($n % $Cols) * $cw + $Scale
    $y = [math]::Floor($n / $Cols) * $cw + $Scale
    $g.FillRectangle($black, $x, $y, $i.Width * $Scale, $i.Height * $Scale)
    $g.DrawImage($i, $x, $y, $i.Width * $Scale, $i.Height * $Scale)
}
$g.Dispose()
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
foreach ($i in $imgs) { $i.Dispose() }
$bmp.Dispose()
Write-Output "saved $Out ($($imgs.Count) images, cell $Cell, scale $Scale)"
