Add-Type -AssemblyName System.Drawing
$img = [System.Drawing.Image]::FromFile((Resolve-Path 'Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Textures/shrub_grassclump_su_base_colour.png'))
Write-Host "Image Size: $($img.Width) x $($img.Height)"
$img.Dispose()

