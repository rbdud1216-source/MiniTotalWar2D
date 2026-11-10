$path = "Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/DetailGrass/Climate_Temperate/Tem_Grass0_8.obj"
$lines = Get-Content $path
$v = New-Object System.Collections.Generic.List[Object]
$vt = New-Object System.Collections.Generic.List[Object]
$faces = New-Object System.Collections.Generic.List[Object]

foreach ($l in $lines) {
    if ($l.StartsWith('v ')) {
        $p = $l.Substring(2).Trim() -split '\s+'
        $v.Add(@([float]$p[0], [float]$p[1], [float]$p[2]))
    } elseif ($l.StartsWith('vt ')) {
        $p = $l.Substring(3).Trim() -split '\s+'
        $vt.Add(@([float]$p[0], [float]$p[1]))
    } elseif ($l.StartsWith('f ')) {
        $faces.Add($l)
    }
}

Write-Host "Tem_Grass0_8: Verts=$($v.Count), UVs=$($vt.Count), Faces=$($faces.Count)"

# 높이와 너비
$minX=999.0; $maxX=-999.0; $minY=999.0; $maxY=-999.0; $minZ=999.0; $maxZ=-999.0
foreach ($pt in $v) {
    if ($pt[0] -lt $minX) { $minX = $pt[0] }; if ($pt[0] -gt $maxX) { $maxX = $pt[0] }
    if ($pt[1] -lt $minY) { $minY = $pt[1] }; if ($pt[1] -gt $maxY) { $maxY = $pt[1] }
    if ($pt[2] -lt $minZ) { $minZ = $pt[2] }; if ($pt[2] -gt $maxZ) { $maxZ = $pt[2] }
}
Write-Host "X bounds: $([Math]::Round($minX,2)) ~ $([Math]::Round($maxX,2)) ($([Math]::Round($maxX-$minX,2))m)"
Write-Host "Y bounds: $([Math]::Round($minY,2)) ~ $([Math]::Round($maxY,2)) ($([Math]::Round($maxY-$minY,2))m)"
Write-Host "Z bounds: $([Math]::Round($minZ,2)) ~ $([Math]::Round($maxZ,2)) ($([Math]::Round($maxZ-$minZ,2))m)"

# 개별 카드(독립된 서브 메쉬/조각) 분리 분석
# 인접한 삼각형들을 클러스터링하여 몇 개의 독립 조각으로 되어 있는지 확인

