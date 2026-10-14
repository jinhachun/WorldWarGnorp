# 임시 그림 굽기 — 글자 지도(art_*.txt)를 PNG로 굽고, .meta가 없으면 같은 종류 기존 그림의 .meta를 본떠 만든다.
# 지도 형식:
#   @ <Assets/Sprites 아래 경로.png> <배율> <WxH> <본뜰 meta(Assets/Sprites 아래)> [피벗 x,y]
#   그 아래 줄들이 그림. '.'=투명 '#'=흰색 'B'=#81ABFF(영주 색 자리) 'K'=검정 'g'=회색 #595959(층 벽 무늬 색)
# 이미 있는 파일은 이 스크립트가 만든 것(baked.list)일 때만 덮어쓴다 — 사용자 그림을 덮지 않는다.
#   -Stage 를 주면 Assets가 아니라 <이 폴더>\stage 에 굽는다(미리 보기용, meta 없음).
param([string[]]$Maps, [switch]$Stage)
Add-Type -AssemblyName System.Drawing
$src = 'C:\Users\jhc98\WorldWarGnorp\Assets\Sprites'
$root = $src
$listPath = Join-Path $PSScriptRoot 'baked.list'
if ($Stage) { $root = Join-Path $PSScriptRoot 'stage'; $listPath = Join-Path $PSScriptRoot 'stage.list' }
$baked = @{}
if (Test-Path $listPath) { foreach ($l in [IO.File]::ReadAllLines($listPath)) { if ($l) { $baked[$l] = 1 } } }
$errors = 0; $count = 0

function Bake($head, $rows) {
    $p = $head -split '\s+'
    $rel = $p[1]; $scale = [int]$p[2]; $wh = $p[3] -split 'x'; $tmpl = $p[4]; $pivot = $null
    if ($p.Count -gt 5) { $pivot = $p[5] }
    $w = [int]$wh[0]; $h = [int]$wh[1]
    if ($rows.Count -ne $h) { Write-Output "ERR $rel : rows $($rows.Count) != $h"; $script:errors++; return }
    for ($y = 0; $y -lt $h; $y++) { if ($rows[$y].Length -ne $w) { Write-Output "ERR $rel : row $y width $($rows[$y].Length) != $w  [$($rows[$y])]"; $script:errors++; return } }
    $out = Join-Path $root $rel
    if ((Test-Path $out) -and -not $baked.ContainsKey($rel)) { Write-Output "SKIP $rel : exists and not mine"; $script:errors++; return }
    $dir = Split-Path $out
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force $dir | Out-Null }
    $bmp = New-Object System.Drawing.Bitmap ($w * $scale), ($h * $scale), ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    for ($y = 0; $y -lt $h; $y++) {
        for ($x = 0; $x -lt $w; $x++) {
            $ch = $rows[$y][$x]
            if ($ch -eq '#') { $c = [System.Drawing.Color]::FromArgb(255, 255, 255, 255) }
            elseif ($ch -eq 'B') { $c = [System.Drawing.Color]::FromArgb(255, 0x81, 0xAB, 0xFF) }
            elseif ($ch -eq 'K') { $c = [System.Drawing.Color]::FromArgb(255, 0, 0, 0) }
            elseif ($ch -eq 'g') { $c = [System.Drawing.Color]::FromArgb(255, 89, 89, 89) }
            elseif ($ch -eq '.') { $c = [System.Drawing.Color]::FromArgb(0, 0, 0, 0) }
            else { Write-Output "ERR $rel : bad char '$ch' row $y"; $script:errors++; $bmp.Dispose(); return }
            for ($dy = 0; $dy -lt $scale; $dy++) { for ($dx = 0; $dx -lt $scale; $dx++) { $bmp.SetPixel($x * $scale + $dx, $y * $scale + $dy, $c) } }
        }
    }
    $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $baked[$rel] = 1
    $meta = "$out.meta"
    if (-not $Stage -and -not (Test-Path $meta)) {
        $t = [IO.File]::ReadAllText((Join-Path $src "$tmpl.meta"))
        $t = [regex]::Replace($t, '(?m)^guid: [0-9a-f]+', 'guid: ' + [guid]::NewGuid().ToString('N'))
        if ($pivot) {
            $xy = $pivot -split ','
            $t = [regex]::Replace($t, '(?m)^  alignment: \d+', '  alignment: 9')
            $t = [regex]::Replace($t, '(?m)^  spritePivot: \{[^}]*\}', "  spritePivot: {x: $($xy[0]), y: $($xy[1])}")
        } else {
            $t = [regex]::Replace($t, '(?m)^  alignment: \d+', '  alignment: 0')
            $t = [regex]::Replace($t, '(?m)^  spritePivot: \{[^}]*\}', '  spritePivot: {x: 0.5, y: 0.5}')
        }
        [IO.File]::WriteAllText($meta, $t)
    }
    $script:count++
}

foreach ($m in $Maps) {
    $head = $null; $rows = @()
    foreach ($line in [IO.File]::ReadAllLines((Join-Path $PSScriptRoot $m))) {
        $line = $line.TrimEnd()
        if ($line.StartsWith('@')) { if ($head) { Bake $head $rows }; $head = $line; $rows = @() }
        elseif ($line.StartsWith(';') -or $line -eq '') { }
        else { $rows += $line }
    }
    if ($head) { Bake $head $rows }
}
[IO.File]::WriteAllLines($listPath, [string[]]($baked.Keys | Sort-Object))
Write-Output "baked $count, errors $errors"
