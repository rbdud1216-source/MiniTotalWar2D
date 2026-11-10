$models = Get-ChildItem -Path Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Shrubs -Filter 'Shrub_Grassclump*.obj'

foreach ($m in $models) {
    $lines = Get-Content $m.FullName
    $uvs = @()
    foreach ($l in ($lines | Where-Object { $_ -like 'vt *' })) {
        $p = $l.Substring(3).Trim() -split '\s+'
        $uvs += ,@([float]$p[0], [float]$p[1])
    }
    
    # UV 바운딩 박스
    $minU = 999.0; $maxU = -999.0; $minV = 999.0; $maxV = -999.0
    foreach ($u in $uvs) {
        if ($u[0] -lt $minU) { $minU = $u[0] }
        if ($u[0] -gt $maxU) { $maxU = $u[0] }
        if ($u[1] -lt $minV) { $minV = $u[1] }
        if ($u[1] -gt $maxV) { $maxV = $u[1] }
    }
    
    # 정점 높이
    $minY = 999.0; $maxY = -999.0
    foreach ($l in ($lines | Where-Object { $_ -like 'v *' })) {
        $p = $l.Substring(2).Trim() -split '\s+'
        $y = [float]$p[1]
        if ($y -lt $minY) { $minY = $y }
        if ($y -gt $maxY) { $maxY = $y }
    }
    
    $vCount = ($lines | Where-Object { $_ -like 'v *' }).Count
    $fCount = ($lines | Where-Object { $_ -like 'f *' }).Count
    
    Write-Host "$($m.Name): Verts=$vCount, Faces=$fCount, Height=$([Math]::Round($maxY - $minY, 2))m, U=[$([Math]::Round($minU,2))~$([Math]::Round($maxU,2))], V=[$([Math]::Round($minV,2))~$([Math]::Round($maxV,2))]"
}

