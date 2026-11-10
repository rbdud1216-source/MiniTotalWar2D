Add-Type -AssemblyName System.Drawing

$lines = Get-Content 'Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Shrubs/Shrub_Grassclump_Tall_D.obj'
$v = @(); $vt = @(); $faces = @()
foreach ($l in $lines) {
    if ($l.StartsWith('v ')) {
        $p = $l.Substring(2).Trim() -split '\s+'
        $v += ,@([float]$p[0], [float]$p[1], [float]$p[2])
    } elseif ($l.StartsWith('vt ')) {
        $p = $l.Substring(3).Trim() -split '\s+'
        $vt += ,@([float]$p[0], [float]$p[1])
    } elseif ($l.StartsWith('f ')) {
        $faces += $l
    }
}

$img = [System.Drawing.Image]::FromFile((Resolve-Path 'Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Textures/shrub_grassclump_su_base_colour.png'))
$bmp = New-Object System.Drawing.Bitmap($img)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$pen = New-Object System.Drawing.Pen([System.Drawing.Color]::Lime, 2)
$brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::Red)

foreach ($f in $faces) {
    $p = $f.Substring(2).Trim() -split '\s+'
    $uvIndices = @(
        [int]($p[0] -split '/')[1] - 1,
        [int]($p[1] -split '/')[1] - 1,
        [int]($p[2] -split '/')[1] - 1
    )
    
    $pt1 = New-Object System.Drawing.Point([int]($vt[$uvIndices[0]][0] * $bmp.Width), [int]((1.0 - $vt[$uvIndices[0]][1]) * $bmp.Height))
    $pt2 = New-Object System.Drawing.Point([int]($vt[$uvIndices[1]][0] * $bmp.Width), [int]((1.0 - $vt[$uvIndices[1]][1]) * $bmp.Height))
    $pt3 = New-Object System.Drawing.Point([int]($vt[$uvIndices[2]][0] * $bmp.Width), [int]((1.0 - $vt[$uvIndices[2]][1]) * $bmp.Height))
    
    $g.DrawLine($pen, $pt1, $pt2)
    $g.DrawLine($pen, $pt2, $pt3)
    $g.DrawLine($pen, $pt3, $pt1)
}

$outPath = 'C:/Users/PC/.gemini/antigravity/brain/e54e15ce-c4f8-4f5a-bade-59595f8ab819/tall_d_uv_overlay.png'
$bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)

$g.Dispose(); $bmp.Dispose(); $img.Dispose()
Write-Host "Tall_D UV overlay saved: $outPath"

