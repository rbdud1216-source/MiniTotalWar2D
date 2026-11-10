Add-Type -AssemblyName System.Drawing

$img = [System.Drawing.Image]::FromFile((Resolve-Path 'Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Textures/shrub_grassclump_su_base_colour.png'))
$bmp = New-Object System.Drawing.Bitmap($img)

Write-Host "Searching for solid opaque blade clusters in shrub_grassclump_su..."

# 하단 0 ~ 300 픽셀 (V = 0.0 ~ 0.3) 영역에서 불투명 픽셀 밀집 구역 찾기
$w = $bmp.Width
$h = $bmp.Height

# Y_pixel = Height * (1.0 - V)
# V: 0.0 ~ 0.3 -> Y_pixel: 700 ~ 1024
$boxes = @()

# 대표적인 몇 개 윈도우의 알파 통계
function CheckBox($u1, $v1, $u2, $v2) {
    $x1 = [int]($u1 * $w); $x2 = [int]($u2 * $w)
    $y1 = [int]((1.0 - $v2) * $h); $y2 = [int]((1.0 - $v1) * $h)
    
    $opaque = 0
    $total = 0
    for ($y = $y1; $y -lt $y2; $y += 2) {
        for ($x = $x1; $x -lt $x2; $x += 2) {
            $c = $bmp.GetPixel($x, $y)
            if ($c.A -gt 100) { $opaque++ }
            $total++
        }
    }
    $ratio = [Math]::Round(($opaque / $total) * 100, 1)
    Write-Host "Box U=[$u1 ~ $u2], V=[$v1 ~ $v2]: Opaque ratio = $ratio% ($opaque / $total)"
}

CheckBox 0.78 0.02 0.96 0.16
CheckBox 0.28 0.02 0.48 0.22
CheckBox 0.50 0.03 0.72 0.28
CheckBox 0.65 0.06 0.88 0.36

$bmp.Dispose()
$img.Dispose()

