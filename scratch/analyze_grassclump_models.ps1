$models = @(
    'Shrub_Grassclump_Tall_Seedhead_A.obj',
    'Shrub_Grassclump_Tall_A.obj',
    'Shrub_Grassclump_Thick_A.obj'
)

foreach ($name in $models) {
    $path = "Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Shrubs/$name"
    $lines = Get-Content $path
    $vCount = ($lines | Where-Object { $_ -like 'v *' }).Count
    $vtCount = ($lines | Where-Object { $_ -like 'vt *' }).Count
    $fCount = ($lines | Where-Object { $_ -like 'f *' }).Count
    
    # 높이(Y) 범위 및 수평(XZ) 반경 구하기
    $minY = 9999.0; $maxY = -9999.0; $maxR = 0.0
    foreach ($l in ($lines | Where-Object { $_ -like 'v *' })) {
        $p = $l.Substring(2).Trim() -split '\s+'
        $y = [float]$p[1]
        $x = [float]$p[0]
        $z = [float]$p[2]
        if ($y -lt $minY) { $minY = $y }
        if ($y -gt $maxY) { $maxY = $y }
        $r = [Math]::Sqrt($x*$x + $z*$z)
        if ($r -gt $maxR) { $maxR = $r }
    }
    
    Write-Host "=============================="
    Write-Host "Model: $name"
    Write-Host "  Vertices: $vCount, Faces: $fCount"
    Write-Host "  Height (Y): $([Math]::Round($minY, 2))m ~ $([Math]::Round($maxY, 2))m (Total: $([Math]::Round($maxY - $minY, 2))m)"
    Write-Host "  Radius (XZ): $([Math]::Round($maxR, 2))m (Diameter: $([Math]::Round($maxR*2, 2))m)"
}

