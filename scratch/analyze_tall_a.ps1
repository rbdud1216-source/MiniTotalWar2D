$lines = Get-Content 'Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Shrubs/Shrub_Grassclump_Tall_A.obj'
$uvs = @()
foreach ($l in ($lines | Where-Object { $_ -like 'vt *' })) {
    $p = $l.Substring(3).Trim() -split '\s+'
    $uvs += ,@([float]$p[0], [float]$p[1])
}
$minU = 999.0; $maxU = -999.0; $minV = 999.0; $maxV = -999.0
foreach ($u in $uvs) {
    if ($u[0] -lt $minU) { $minU = $u[0] }
    if ($u[0] -gt $maxU) { $maxU = $u[0] }
    if ($u[1] -lt $minV) { $minV = $u[1] }
    if ($u[1] -gt $maxV) { $maxV = $u[1] }
}
Write-Host "Tall_A UV range: U=[$minU ~ $maxU], V=[$minV ~ $maxV]"

# UV 클러스터 분석
$lowV = ($uvs | Where-Object { $_[1] -lt 0.35 }).Count
$midV = ($uvs | Where-Object { $_[1] -ge 0.35 -and $_[1] -lt 0.65 }).Count
$highV = ($uvs | Where-Object { $_[1] -ge 0.65 }).Count
Write-Host "Tall_A UV Distribution: Low(Ground Grass)=$lowV, Mid=$midV, High(Seedhead)=$highV"

