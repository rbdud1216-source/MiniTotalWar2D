$lines = Get-Content 'Assets/Scripts/TacticalForestZone.cs' -Encoding UTF8
$results = @()
for ($i = 0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i].Trim()
    if ($line.StartsWith('//') -or $line.StartsWith('/*') -or $line.StartsWith('*')) {
        $comment = $line -replace '^[/*\s]+', ''
        if ($comment -match '^[=-]+$') { continue }
        $hasKorean = $comment -match '[\uac00-\ud7a3]'
        $hasEnglish = $comment -match '[a-zA-Z]{3,}'
        if (-not $hasKorean -and $hasEnglish) {
            $results += "$($i + 1): $line"
        }
    }
}
Write-Output "Total purely English comments: $($results.Count)"
$results | Select-Object -First 50 | ForEach-Object { Write-Output $_ }
