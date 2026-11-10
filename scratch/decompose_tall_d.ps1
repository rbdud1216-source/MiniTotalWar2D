$lines = Get-Content 'Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Shrubs/Shrub_Grassclump_Tall_D.obj'
$v = New-Object System.Collections.Generic.List[Object]
$vt = New-Object System.Collections.Generic.List[Object]
$vn = New-Object System.Collections.Generic.List[Object]
$faces = New-Object System.Collections.Generic.List[Object]

foreach ($l in $lines) {
    if ($l.StartsWith('v ')) {
        $p = $l.Substring(2).Trim() -split '\s+'
        $v.Add(@([float]$p[0], [float]$p[1], [float]$p[2]))
    } elseif ($l.StartsWith('vt ')) {
        $p = $l.Substring(3).Trim() -split '\s+'
        $vt.Add(@([float]$p[0], [float]$p[1]))
    } elseif ($l.StartsWith('vn ')) {
        $p = $l.Substring(3).Trim() -split '\s+'
        $vn.Add(@([float]$p[0], [float]$p[1], [float]$p[2]))
    } elseif ($l.StartsWith('f ')) {
        $faces.Add($l)
    }
}

Write-Host "Tall_D: Verts=$($v.Count), UVs=$($vt.Count), Normals=$($vn.Count), Faces=$($faces.Count)"

$parent = @{}
function Find($i) {
    if (!$parent.ContainsKey($i)) { $parent[$i] = $i }
    if ($parent[$i] -ne $i) { $parent[$i] = Find($parent[$i]) }
    return $parent[$i]
}
function Union($i, $j) {
    $ri = Find($i); $rj = Find($j)
    if ($ri -ne $rj) { $parent[$ri] = $rj }
}

$triList = @()
foreach ($f in $faces) {
    $parts = $f.Substring(2).Trim() -split '\s+'
    $i1 = [int]($parts[0] -split '/')[0] - 1
    $i2 = [int]($parts[1] -split '/')[0] - 1
    $i3 = [int]($parts[2] -split '/')[0] - 1
    Union $i1 $i2
    Union $i2 $i3
    $triList += ,@($i1, $i2, $i3)
}

$cards = @{}
for ($t = 0; $t -lt $triList.Length; $t++) {
    $root = Find $triList[$t][0]
    if (!$cards.ContainsKey($root)) { $cards[$root] = New-Object System.Collections.Generic.List[Object] }
    $cards[$root].Add($triList[$t])
}

Write-Host "Tall_D Total Independent Blade Cards: $($cards.Keys.Count)"

$cIdx = 0
foreach ($k in $cards.Keys) {
    $tris = $cards[$k]
    $vSet = New-Object System.Collections.Generic.HashSet[int]
    foreach ($tri in $tris) {
        $vSet.Add($tri[0]) | Out-Null; $vSet.Add($tri[1]) | Out-Null; $vSet.Add($tri[2]) | Out-Null
    }
    
    $minY = 999.0; $maxY = -999.0; $minX = 999.0; $maxX = -999.0; $minZ = 999.0; $maxZ = -999.0
    foreach ($vi in $vSet) {
        $pt = $v[$vi]
        if ($pt[0] -lt $minX) { $minX = $pt[0] }; if ($pt[0] -gt $maxX) { $maxX = $pt[0] }
        if ($pt[1] -lt $minY) { $minY = $pt[1] }; if ($pt[1] -gt $maxY) { $maxY = $pt[1] }
        if ($pt[2] -lt $minZ) { $minZ = $pt[2] }; if ($pt[2] -gt $maxZ) { $maxZ = $pt[2] }
    }
    $h = $maxY - $minY
    $w = [Math]::Sqrt(($maxX-$minX)*($maxX-$minX) + ($maxZ-$minZ)*($maxZ-$minZ))
    $cIdx++
    Write-Host "Card $($cIdx) : Verts=$($vSet.Count), Tris=$($tris.Count), Height=$([Math]::Round($h, 2))m, Width=$([Math]::Round($w, 2))m"
}
