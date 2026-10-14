# PNG를 글자 지도로 찍는다. '.'=투명 '#'=흰색 'B'=#81ABFF 'K'=불투명 검정 '?'=그 밖(끝에 색 목록)
param([string[]]$Files)
Add-Type -AssemblyName System.Drawing
foreach ($f in $Files) {
    $b = [System.Drawing.Bitmap]::FromFile((Resolve-Path $f))
    Write-Output "== $f $($b.Width)x$($b.Height)"
    $other = @{}
    for ($y = 0; $y -lt $b.Height; $y++) {
        $line = ''
        for ($x = 0; $x -lt $b.Width; $x++) {
            $c = $b.GetPixel($x, $y)
            if ($c.A -eq 0) { $line += '.' }
            elseif ($c.R -eq 255 -and $c.G -eq 255 -and $c.B -eq 255) { $line += '#' }
            elseif ($c.R -eq 0x81 -and $c.G -eq 0xAB -and $c.B -eq 0xFF) { $line += 'B' }
            elseif ($c.R -eq 0 -and $c.G -eq 0 -and $c.B -eq 0) { $line += 'K' }
            else { $line += '?'; $other["$($c.A),$($c.R),$($c.G),$($c.B)"] = 1 }
        }
        Write-Output $line
    }
    if ($other.Count -gt 0) { Write-Output ("other: " + ($other.Keys -join ' | ')) }
    $b.Dispose()
}
