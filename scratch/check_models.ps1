$models = @(
    'Shrub_Grassclump_Tall_D.obj',
    'Shrub_Grassclump_Thick_A.obj',
    'Shrub_Grassclump_Tall_Seedhead_A.obj',
    'Shrub_Grassclump_Tall_A.obj'
)
foreach ($m in $models) {
    $p = "Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Shrubs/$m"
    if (Test-Path $p) {
        Write-Host "Found: $m"
    } else {
        Write-Host "NOT Found: $m"
    }
}

