Add-Type -AssemblyName System.Drawing

function Show-UnclippedWithPolygon($srcPath, $outPath, $ptsUV, $vMin, $vMax) {
    $bmp = New-Object System.Drawing.Bitmap($srcPath)
    $W = $bmp.Width
    $H = $bmp.Height
    $yTop = [int]([Math]::Max(0, (1.0 - $vMax) * $H))
    $yBot = [int]([Math]::Min($H, (1.0 - $vMin) * $H))
    $cropH = $yBot - $yTop
    $outBmp = New-Object System.Drawing.Bitmap($W, $cropH)
    $g = [System.Drawing.Graphics]::FromImage($outBmp)
    $g.Clear([System.Drawing.Color]::FromArgb(255, 25, 28, 32))

    $srcRect = New-Object System.Drawing.Rectangle(0, $yTop, $W, $cropH)
    $destRect = New-Object System.Drawing.Rectangle(0, 0, $W, $cropH)
    $g.DrawImage($bmp, $destRect, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)

    $pts = New-Object System.Drawing.PointF[]($ptsUV.Count)
    for ($i = 0; $i -lt $ptsUV.Count; $i++) {
        $u = $ptsUV[$i][0]
        $v = $ptsUV[$i][1]
        $pts[$i] = New-Object System.Drawing.PointF(($u * $W), ((1.0 - $v) * $H - $yTop))
    }

    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::Cyan, 4)
    $g.DrawPolygon($pen, $pts)

    $outBmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose()
    $outBmp.Dispose()
    $bmp.Dispose()
}

$temPath = "c:\unityProject\MiniTotalWar2D\Assets\TotalWar_ThreeKingdoms_Extracted\Vegetation\Textures\tem_grass_sp_base_colour.png"
$sbtPath = "c:\unityProject\MiniTotalWar2D\Assets\TotalWar_ThreeKingdoms_Extracted\Vegetation\Textures\sbt_grass0_sp_base_colour.png"

Show-UnclippedWithPolygon $temPath "c:\unityProject\MiniTotalWar2D\scratch\unclipped_pink_row3.png" @(
    @(0.004, 0.303), @(0.250, 0.400), @(0.500, 0.497), @(0.750, 0.400), @(0.996, 0.303)
) 0.29 0.51

Show-UnclippedWithPolygon $temPath "c:\unityProject\MiniTotalWar2D\scratch\unclipped_purple_row1.png" @(
    @(0.004, 0.803), @(0.250, 0.900), @(0.500, 0.996), @(0.750, 0.900), @(0.996, 0.803)
) 0.79 1.00

Show-UnclippedWithPolygon $sbtPath "c:\unityProject\MiniTotalWar2D\scratch\unclipped_orange_row4.png" @(
    @(0.070, 0.004), @(0.260, 0.118), @(0.495, 0.295), @(0.768, 0.132), @(0.945, 0.004)
) 0.0 0.32
Write-Host "Done"
