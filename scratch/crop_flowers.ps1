Add-Type -AssemblyName System.Drawing

$temPath = "c:\unityProject\MiniTotalWar2D\Assets\TotalWar_ThreeKingdoms_Extracted\Vegetation\Textures\tem_grass_sp_base_colour.png"
$sbtPath = "c:\unityProject\MiniTotalWar2D\Assets\TotalWar_ThreeKingdoms_Extracted\Vegetation\Textures\sbt_grass0_sp_base_colour.png"

$tem = [System.Drawing.Bitmap]::FromFile($temPath)
$sbt = [System.Drawing.Bitmap]::FromFile($sbtPath)

Write-Host "tem size: $($tem.Width) x $($tem.Height)"
Write-Host "sbt size: $($sbt.Width) x $($sbt.Height)"

# 1. tem_grass_sp row 3 pink flower tuft (around X: 1150..1500, Y: 1050..1480)
$rect1 = New-Object System.Drawing.Rectangle(1150, 1020, 350, 460)
$crop1 = $tem.Clone($rect1, $tem.PixelFormat)
$crop1.Save("c:\unityProject\MiniTotalWar2D\scratch\crop_pink_tuft.png")
$crop1.Dispose()

# 2. tem_grass_sp row 1 purple flower tuft (around X: 950..1400, Y: 0..450)
$rect2 = New-Object System.Drawing.Rectangle(950, 0, 450, 450)
$crop2 = $tem.Clone($rect2, $tem.PixelFormat)
$crop2.Save("c:\unityProject\MiniTotalWar2D\scratch\crop_purple_tuft.png")
$crop2.Dispose()

# 3. sbt_grass0_sp row 4 red/orange spike tuft (around X: 1100..1650, Y: 1450..2048)
$rect3 = New-Object System.Drawing.Rectangle(1100, 1450, 550, 598)
$crop3 = $sbt.Clone($rect3, $sbt.PixelFormat)
$crop3.Save("c:\unityProject\MiniTotalWar2D\scratch\crop_orange_tuft.png")
$crop3.Dispose()

$tem.Dispose()
$sbt.Dispose()
Write-Host "Crops saved successfully!"

