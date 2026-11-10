$lines = Get-Content 'Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Shrubs/Shrub_Grassclump_Tall_D.obj'
$vCount = ($lines | Where-Object { $_ -like 'v *' }).Count
$vtCount = ($lines | Where-Object { $_ -like 'vt *' }).Count
$fCount = ($lines | Where-Object { $_ -like 'f *' }).Count
Write-Host "Tall_D Vertices: $vCount, UVs: $vtCount, Faces: $fCount"

# 높이 및 바운딩
$minY = 999.0; $maxY = -999.0; $maxR = 0.0
foreach ($l in ($lines | Where-Object { $_ -like 'v *' })) {
    $p = $l.Substring(2).Trim() -split '\s+'
    $x = [float]$p[0]; $y = [float]$p[1]; $z = [float]$p[2]
    if ($y -lt $minY) { $minY = $y }
    if ($y -gt $maxY) { $maxY = $y }
    $r = [Math]::Sqrt($x*$x + $z*$z)
    if ($r -gt $maxR) { $maxR = $r }
}
Write-Host "Height: $([Math]::Round($minY,2))m ~ $([Math]::Round($maxY,2))m ($([Math]::Round($maxY - $minY, 2))m)"
Write-Host "Radius: $([Math]::Round($maxR,2))m (Diameter: $([Math]::Round($maxR*2,2))m)"

