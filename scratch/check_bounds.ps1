# OBJ 파일의 버텍스 노멀 Y 분포 확인
$lines = [System.IO.File]::ReadAllLines("Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Trees/Huangshan_Pine_A.obj")
$allNy = New-Object System.Collections.Generic.List[float]
foreach ($l in $lines) {
    if ($l.StartsWith("vn ")) {
        $p = $l.Split(" ", [System.StringSplitOptions]::RemoveEmptyEntries)
        $allNy.Add([float]$p[2])
    }
}
Write-Host "Total Normals: $($allNy.Count)"
$sortedNy = $allNy | Sort-Object
$q10 = $sortedNy[[int]($sortedNy.Count * 0.10)]
$q25 = $sortedNy[[int]($sortedNy.Count * 0.25)]
$q50 = $sortedNy[[int]($sortedNy.Count * 0.50)]
$q75 = $sortedNy[[int]($sortedNy.Count * 0.75)]
$q90 = $sortedNy[[int]($sortedNy.Count * 0.90)]
Write-Host "Min: $($sortedNy[0]), 10%: $q10, 25%: $q25, 50%: $q50, 75%: $q75, 90%: $q90, Max: $($sortedNy[-1])"
