$path = 'c:\unityProject\MiniTotalWar2D\Docs\TacticalForestZone.md'
$text = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
$text = $text.Replace("투과 농도: 피크 알파 `0.52`(반투명)로 불꽃과 뒤편 나뭇가지 투과.", "투과 농도 및 진하기: `smokeDensity` 슬라이더 (0.1 ~ 1.0, 기본값 0.75) 연동 (인스펙터 실시간 반영).")
[System.IO.File]::WriteAllText($path, $text, [System.Text.Encoding]::UTF8)
Write-Output "String Replace Finished."
