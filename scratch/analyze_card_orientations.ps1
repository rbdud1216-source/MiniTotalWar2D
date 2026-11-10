$lines = Get-Content 'Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Shrubs/Shrub_Grassclump_Tall_Seedhead_A.obj'
$v = New-Object System.Collections.Generic.List[Object]

foreach ($line in $lines) {
    if ($line.StartsWith('v ')) {
        $parts = $line.Substring(2).Trim() -split '\s+'
        $v.Add(@([float]$parts[0], [float]$parts[1], [float]$parts[2]))
    }
}

for ($card = 0; $card -lt 8; $card++) {
    $startIdx = $card * 6
    $rootX = ($v[$startIdx + 1][0] + $v[$startIdx + 3][0]) / 2.0
    $rootZ = ($v[$startIdx + 1][2] + $v[$startIdx + 3][2]) / 2.0
    $topX = $v[$startIdx + 4][0]
    $topY = $v[$startIdx + 4][1]
    $topZ = $v[$startIdx + 4][2]
    
    $dx = $topX - $rootX
    $dz = $topZ - $rootZ
    $dist = [Math]::Sqrt($dx*$dx + $dz*$dz)
    $angleDeg = [Math]::Atan2($dz, $dx) * 180.0 / [Math]::PI
    
    Write-Host "Card $($card + 1): Root=($([Math]::Round($rootX,2)), $([Math]::Round($rootZ,2))) -> Top=($([Math]::Round($topX,2)), $([Math]::Round($topY,2)), $([Math]::Round($topZ,2))) | Length 2D=$([Math]::Round($dist,2))m, Angle=$([Math]::Round($angleDeg,1)) deg"
}

