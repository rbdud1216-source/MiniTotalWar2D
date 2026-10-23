Add-Type -AssemblyName System.Drawing
$paths = @(
    'scratch/extracted_tw_vfx/3k_anim_smoke_a_diffuse.png',
    'Assets/TotalWar_ThreeKingdoms_Extracted/VFX/Textures/totalwar_smoke_particle.png'
)
foreach ($p in $paths) {
    $full = Resolve-Path $p
    $bmp = [System.Drawing.Bitmap]::FromFile($full)
    $totA = 0; $totR = 0; $totG = 0; $totB = 0; $maxA = 0; $nz = 0
    $w = $bmp.Width; $h = $bmp.Height; $samples = 0
    for ($y = 0; $y -lt $h; $y += 8) {
        for ($x = 0; $x -lt $w; $x += 8) {
            $c = $bmp.GetPixel($x, $y)
            $totA += $c.A; $totR += $c.R; $totG += $c.G; $totB += $c.B
            if ($c.A -gt 0) { $nz++ }
            if ($c.A -gt $maxA) { $maxA = $c.A }
            $samples++
        }
    }
    Write-Host $p
    Write-Host ("  Size: " + $w + "x" + $h)
    Write-Host ("  Avg A: " + [Math]::Round($totA / $samples, 2) + " / 255 (" + [Math]::Round(($totA / $samples) / 255 * 100, 1) + "%)")
    Write-Host ("  Avg RGB: (" + [Math]::Round($totR / $samples) + ", " + [Math]::Round($totG / $samples) + ", " + [Math]::Round($totB / $samples) + ")")
    Write-Host ("  Max A: " + $maxA + " / 255")
    Write-Host ("  NonZero%: " + [Math]::Round($nz / $samples * 100, 2) + "%")
    $bmp.Dispose()
}
