Add-Type -AssemblyName System.Drawing

function Test-PentagonCutout($srcPath, $outPath, $u0, $v0, $u1, $v1, $u2, $v2, $u3, $v3, $u4, $v4) {
    $bmp = New-Object System.Drawing.Bitmap($srcPath)
    $W = $bmp.Width
    $H = $bmp.Height
    $vMin = [Math]::Min($v0, $v4)
    $vMax = $v2
    $yTop = [int]((1.0 - $vMax) * $H)
    $yBot = [int]((1.0 - $vMin) * $H)
    $cropH = $yBot - $yTop
    $outBmp = New-Object System.Drawing.Bitmap($W, $cropH)
    $g = [System.Drawing.Graphics]::FromImage($outBmp)
    $g.Clear([System.Drawing.Color]::FromArgb(255, 30, 35, 40))

    $pts = New-Object System.Drawing.PointF[](5)
    $pts[0] = New-Object System.Drawing.PointF(($u0 * $W), ((1.0 - $v0) * $H - $yTop))
    $pts[1] = New-Object System.Drawing.PointF(($u1 * $W), ((1.0 - $v1) * $H - $yTop))
    $pts[2] = New-Object System.Drawing.PointF(($u2 * $W), ((1.0 - $v2) * $H - $yTop))
    $pts[3] = New-Object System.Drawing.PointF(($u3 * $W), ((1.0 - $v3) * $H - $yTop))
    $pts[4] = New-Object System.Drawing.PointF(($u4 * $W), ((1.0 - $v4) * $H - $yTop))

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddPolygon($pts)
    $g.SetClip($path)

    $srcRect = New-Object System.Drawing.Rectangle(0, $yTop, $W, $cropH)
    $destRect = New-Object System.Drawing.Rectangle(0, 0, $W, $cropH)
    $g.DrawImage($bmp, $destRect, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)

    $g.ResetClip()
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::Lime, 3)
    $g.DrawPolygon($pen, $pts)

    $outBmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose()
    $outBmp.Dispose()
    $bmp.Dispose()
}

$temPath = "c:\unityProject\MiniTotalWar2D\Assets\TotalWar_ThreeKingdoms_Extracted\Vegetation\Textures\tem_grass_sp_base_colour.png"
$sbtPath = "c:\unityProject\MiniTotalWar2D\Assets\TotalWar_ThreeKingdoms_Extracted\Vegetation\Textures\sbt_grass0_sp_base_colour.png"

Test-PentagonCutout $temPath "c:\unityProject\MiniTotalWar2D\scratch\cutout_tem_pure_row4.png" 0.004 0.004  0.25 0.15  0.50 0.296  0.75 0.15  0.996 0.004
Test-PentagonCutout $sbtPath "c:\unityProject\MiniTotalWar2D\scratch\cutout_sbt_pure_row3.png" 0.004 0.304  0.25 0.40  0.50 0.496  0.75 0.40  0.996 0.304
Write-Host "Pure rows generated!"

