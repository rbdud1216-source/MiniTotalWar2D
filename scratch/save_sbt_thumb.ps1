Add-Type -AssemblyName System.Drawing
$img = [System.Drawing.Image]::FromFile((Resolve-Path 'Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Textures/sbt_grass_base_colour.png'))
$thumb = New-Object System.Drawing.Bitmap(1024, 1024)
$g = [System.Drawing.Graphics]::FromImage($thumb)
$g.DrawImage($img, 0, 0, 1024, 1024)
$g.Dispose()
$img.Dispose()

$outPath = 'C:/Users/PC/.gemini/antigravity/brain/e54e15ce-c4f8-4f5a-bade-59595f8ab819/sbt_grass_thumb.png'
$thumb.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
$thumb.Dispose()
Write-Host "Thumbnail saved to $outPath"

