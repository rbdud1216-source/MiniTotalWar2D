$lines = Get-Content 'Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Shrubs/Shrub_Grassclump_Tall_Seedhead_A.obj'
$v = New-Object System.Collections.Generic.List[Object]
$vt = New-Object System.Collections.Generic.List[Object]
$vn = New-Object System.Collections.Generic.List[Object]
$faces = New-Object System.Collections.Generic.List[Object]

foreach ($line in $lines) {
    if ($line.StartsWith('v ')) {
        $parts = $line.Substring(2).Trim() -split '\s+'
        $v.Add(@([float]$parts[0], [float]$parts[1], [float]$parts[2]))
    } elseif ($line.StartsWith('vt ')) {
        $parts = $line.Substring(3).Trim() -split '\s+'
        $vt.Add(@([float]$parts[0], [float]$parts[1]))
    } elseif ($line.StartsWith('vn ')) {
        $parts = $line.Substring(3).Trim() -split '\s+'
        $vn.Add(@([float]$parts[0], [float]$parts[1], [float]$parts[2]))
    } elseif ($line.StartsWith('f ')) {
        $faces.Add($line)
    }
}

Write-Host "Total Vertices: $($v.Count)"
Write-Host "Total UVs: $($vt.Count)"
Write-Host "Total Normals: $($vn.Count)"
Write-Host "Total Faces: $($faces.Count)"

for ($card = 0; $card -lt 8; $card++) {
    $startIdx = $card * 6
    Write-Host "`n=== Card $($card + 1) ==="
    for ($i = 0; $i -lt 6; $i++) {
        $idx = $startIdx + $i
        $pt = $v[$idx]
        $uv = $vt[$idx]
        $norm = $vn[$idx]
        Write-Host "  V$($idx+1): Pos=($($pt[0]), $($pt[1]), $($pt[2])) UV=($($uv[0]), $($uv[1])) Normal=($($norm[0]), $($norm[1]), $($norm[2]))"
    }
}

