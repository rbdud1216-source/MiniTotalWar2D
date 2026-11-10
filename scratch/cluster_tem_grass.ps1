$lines = Get-Content 'Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/DetailGrass/Climate_Temperate/Tem_Grass0_8.obj'
$faces = @()
foreach ($l in ($lines | Where-Object { $_ -like 'f *' })) {
    $p = $l.Substring(2).Trim() -split '\s+'
    $idx1 = [int]($p[0] -split '/')[0]
    $idx2 = [int]($p[1] -split '/')[0]
    $idx3 = [int]($p[2] -split '/')[0]
    $faces += ,@($idx1, $idx2, $idx3)
}

Write-Host "Total Triangles: $($faces.Length)"

# 유니온 파인드(Disjoint Set)로 연결된 서브 메쉬(독립 카드) 개수 찾기
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

foreach ($f in $faces) {
    Union $f[0] $f[1]
    Union $f[1] $f[2]
}

$groups = @{}
foreach ($f in $faces) {
    $root = Find $f[0]
    if (!$groups.ContainsKey($root)) { $groups[$root] = New-Object System.Collections.Generic.List[Object] }
    $groups[$root].Add($f)
}

Write-Host "Total Independent Grass Blade Cards: $($groups.Keys.Count)"

# 각 카드의 삼각형 개수 분포
$triCounts = @{}
foreach ($k in $groups.Keys) {
    $cnt = $groups[$k].Count
    if (!$triCounts.ContainsKey($cnt)) { $triCounts[$cnt] = 0 }
    $triCounts[$cnt]++
}
foreach ($k in ($triCounts.Keys | Sort-Object)) {
    Write-Host "Cards with $k triangles: $($triCounts[$k])"
}

