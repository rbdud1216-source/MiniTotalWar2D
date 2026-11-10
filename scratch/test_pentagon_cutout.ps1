Add-Type -AssemblyName System.Drawing

function Save-PentagonCutout($srcPath, $outPath, $uvPoints) {
    $src = [System.Drawing.Bitmap]::FromFile($srcPath)
    $w = $src.Width
    $h = $src.Height

    # Find bounding box in pixel space (Note: Unity UV V=0 is bottom, so Y_px = (1 - V) * h)
    $pts = New-Object System.Drawing.PointF[] $uvPoints.Count
    $minY = $h; $maxY = 0
    for ($i = 0; $i -lt $uvPoints.Count; $i++) {
        $u = $uvPoints[$i][0]
        $v = $uvPoints[$i][1]
        $px = [float]($u * $w)
        $py = [float]((1.0 - $v) * $h)
        $pts[$i] = New-Object System.Drawing.PointF($px, $py)
        if ($py -lt $minY) { $minY = [int]$py }
        if ($py -gt $maxY) { $maxY = [int]$py }
    }

    $minY = [Math]::Max(0, $minY - 5)
    $maxY = [Math]::Min($h, $maxY + 5)
    $cropH = $maxY - $minY

    $dst = New-Object System.Drawing.Bitmap($w, $cropH)
    $g = [System.Drawing.Graphics]::FromImage($dst)
    $g.Clear([System.Drawing.Color]::FromArgb(255, 35, 40, 45))

    # Shift polygon points by -minY
    $shiftedPts = New-Object System.Drawing.PointF[] $uvPoints.Count
    for ($i = 0; $i -lt $pts.Count; $i++) {
        $shiftedPts[$i] = New-Object System.Drawing.PointF($pts[$i].X, ($pts[$i].Y - $minY))
    }

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddPolygon($shiftedPts)
    $g.SetClip($path)
    $g.DrawImage($src, 0, -$minY, $w, $h)
    $g.ResetClip()

    # Draw cyan outline of the 5-gon cutout to verify no stray branches are inside
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::Cyan, 3)
    $g.DrawPolygon($pen, $shiftedPts)

    $dst.Save($outPath)
    $g.Dispose()
    $dst.Dispose()
    $src.Dispose()
}

$temPath = "c:\unityProject\MiniTotalWar2D\Assets\TotalWar_ThreeKingdoms_Extracted\Vegetation\Textures\tem_grass_sp_base_colour.png"
$sbtPath = "c:\unityProject\MiniTotalWar2D\Assets\TotalWar_ThreeKingdoms_Extracted\Vegetation\Textures\sbt_grass0_sp_base_colour.png"

# 1. Pink flower strip (Tem_Grass0_6 UV: (0,0.30) -> (0.25,0.40) -> (0.50,0.50) -> (0.75,0.40) -> (1,0.30))
Save-PentagonCutout $temPath "c:\unityProject\MiniTotalWar2D\scratch\cutout_pink_strip.png" @(
    @(0.00, 0.30), @(0.25, 0.40), @(0.50, 0.50), @(0.75, 0.40), @(1.00, 0.30)
)

# 2. Purple flower strip (Tem_Grass1_2 UV: (0,0.80) -> (0.25,0.90) -> (0.50,1.00) -> (0.75,0.90) -> (1,0.80))
Save-PentagonCutout $temPath "c:\unityProject\MiniTotalWar2D\scratch\cutout_purple_strip.png" @(
    @(0.00, 0.80), @(0.25, 0.90), @(0.50, 1.00), @(0.75, 0.90), @(1.00, 0.80)
)

# 3. Orange flower spike strip (Sbt_Grass0_3 UV: (0,0.00) -> (0.25,0.151) -> (0.50,0.301) -> (0.809,0.115) -> (1,0.00))
Save-PentagonCutout $sbtPath "c:\unityProject\MiniTotalWar2D\scratch\cutout_orange_strip.png" @(
    @(0.00, 0.002), @(0.250, 0.151), @(0.500, 0.301), @(0.809, 0.115), @(0.999, 0.002)
)

Write-Host "All 3 pentagon cutouts generated!"

