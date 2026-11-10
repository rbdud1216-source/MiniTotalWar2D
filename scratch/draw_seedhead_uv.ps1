Add-Type -AssemblyName System.Drawing

$img = [System.Drawing.Image]::FromFile((Resolve-Path 'Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Textures/shrub_grassclump_su_base_colour.png'))
$bmp = New-Object System.Drawing.Bitmap($img)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$pen = New-Object System.Drawing.Pen([System.Drawing.Color]::Red, 3)
$penBlue = New-Object System.Drawing.Pen([System.Drawing.Color]::Cyan, 2)
$font = New-Object System.Drawing.Font('Arial', 14, [System.Drawing.FontStyle]::Bold)
$brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::Yellow)

# Shrub_Grassclump_Tall_Seedhead_A.obj UV
# UV: (0.2058, 0.4951), (0.0058, 0.02), (0.0018, 0.634), (0.0967, 0.0039), (0.2708, 0.7253), (0.4795, 0.4609)
# Note: Unity/OBJ V coordinate: Y_pixel = Height * (1.0 - V)
$uvs = @(
    @(0.2058, 0.4951),
    @(0.0058, 0.0200),
    @(0.0018, 0.6340),
    @(0.0967, 0.0039),
    @(0.2708, 0.7253),
    @(0.4795, 0.4609)
)

$pts = @()
foreach ($uv in $uvs) {
    $px = [int]($uv[0] * $bmp.Width)
    $py = [int]((1.0 - $uv[1]) * $bmp.Height)
    $pts += New-Object System.Drawing.Point($px, $py)
}

# 5개 삼각형 그리기
# f 1 2 3 -> 0 1 2
# f 4 2 1 -> 3 1 0
# f 5 1 3 -> 4 0 2
# f 6 4 1 -> 5 3 0
# f 6 1 5 -> 5 0 4
$tris = @(
    @(0, 1, 2),
    @(3, 1, 0),
    @(4, 0, 2),
    @(5, 3, 0),
    @(5, 0, 4)
)

foreach ($tri in $tris) {
    $p0 = $pts[$tri[0]]
    $p1 = $pts[$tri[1]]
    $p2 = $pts[$tri[2]]
    $g.DrawLine($penBlue, $p0, $p1)
    $g.DrawLine($penBlue, $p1, $p2)
    $g.DrawLine($penBlue, $p2, $p0)
}

for ($i = 0; $i -lt $pts.Length; $i++) {
    $p = $pts[$i]
    $g.FillEllipse($brush, $p.X - 5, $p.Y - 5, 10, 10)
    $g.DrawString("V$($i+1)", $font, $brush, $p.X + 8, $p.Y - 8)
}

$outPath = 'C:/Users/PC/.gemini/antigravity/brain/e54e15ce-c4f8-4f5a-bade-59595f8ab819/seedhead_uv_debug.png'
$bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)

$g.Dispose()
$bmp.Dispose()
$img.Dispose()
Write-Host "UV debug image saved: $outPath"

