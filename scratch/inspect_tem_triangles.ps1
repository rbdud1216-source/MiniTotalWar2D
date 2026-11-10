$lines = Get-Content 'Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/DetailGrass/Climate_Temperate/Tem_Grass0_8.obj'
$v = @()
$vt = @()
foreach ($l in $lines) {
    if ($l.StartsWith('v ')) {
        $p = $l.Substring(2).Trim() -split '\s+'
        $v += ,@([float]$p[0], [float]$p[1], [float]$p[2])
    } elseif ($l.StartsWith('vt ')) {
        $p = $l.Substring(3).Trim() -split '\s+'
        $vt += ,@([float]$p[0], [float]$p[1])
    }
}

# 처음 10개 삼각형의 크기와 UV 분석
for ($i = 0; $i -lt 10; $i++) {
    $idx1 = $i * 3
    $p1 = $v[$idx1]; $p2 = $v[$idx1+1]; $p3 = $v[$idx1+2]
    $u1 = $vt[$idx1]; $u2 = $vt[$idx1+1]; $u3 = $vt[$idx1+2]
    
    $dx1 = $p2[0] - $p1[0]; $dy1 = $p2[1] - $p1[1]; $dz1 = $p2[2] - $p1[2]
    $dx2 = $p3[0] - $p1[0]; $dy2 = $p3[1] - $p1[1]; $dz2 = $p3[2] - $p1[2]
    $len1 = [Math]::Sqrt($dx1*$dx1 + $dy1*$dy1 + $dz1*$dz1)
    $len2 = [Math]::Sqrt($dx2*$dx2 + $dy2*$dy2 + $dz2*$dz2)
    
    Write-Host "Tri $($i+1): Center=($([Math]::Round(($p1[0]+$p2[0]+$p3[0])/3, 2)), $([Math]::Round(($p1[1]+$p2[1]+$p3[1])/3, 2)), $([Math]::Round(($p1[2]+$p2[2]+$p3[2])/3, 2))) Edge1=$([Math]::Round($len1, 2))m, Edge2=$([Math]::Round($len2, 2))m"
    Write-Host "  UVs: ($($u1[0]), $($u1[1])) | ($($u2[0]), $($u2[1])) | ($($u3[0]), $($u3[1]))"
}

