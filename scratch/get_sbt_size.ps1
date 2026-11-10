Add-Type -AssemblyName System.Drawing
$img = [System.Drawing.Image]::FromFile((Resolve-Path 'Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Textures/sbt_grass_base_colour.png'))
Write-Host "sbt_grass_base_colour Size: $($img.Width) x $($img.Height)"
$img.Dispose()

