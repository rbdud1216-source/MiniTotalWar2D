$path = 'c:\unityProject\MiniTotalWar2D\Docs\TacticalForestZone.md'
$enc = [System.Text.UTF8Encoding]::new($false)
$lines = [System.IO.File]::ReadAllLines($path, $enc)
$lines[339] = '      - 투과 농도 및 진하기: `smokeDensity` 슬라이더 (0.1 ~ 1.0, 기본값 0.75) 연동 (인스펙터 실시간 반영).'
[System.IO.File]::WriteAllLines($path, $lines, $enc)
Write-Output "UTF8 Saved"
