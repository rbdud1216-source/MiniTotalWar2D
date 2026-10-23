$content = Get-Content 'Assets/Scripts/TacticalForestZone.cs' -Encoding UTF8
$inlineComments = @()
for ($i = 0; $i -lt $content.Count; $i++) {
    $line = $content[$i]
    if ($line -match '//(.*)$') {
        $c = $matches[1].Trim()
        if ($c -notmatch '[\uac00-\ud7a3]' -and $c -match '[a-zA-Z]{3,}') {
            $inlineComments += "$($i + 1): $line"
        }
    }
}
Write-Output "=== Inline English comments ==="
Write-Output "Count: $($inlineComments.Count)"
$inlineComments | Select-Object -First 30 | ForEach-Object { Write-Output $_ }

$tooltips = @()
for ($i = 0; $i -lt $content.Count; $i++) {
    $line = $content[$i]
    if ($line -match '\[Tooltip\("([^"]+)"\)\]') {
        $t = $matches[1]
        if ($t -notmatch '[\uac00-\ud7a3]') {
            $tooltips += "$($i + 1): $line"
        }
    }
    if ($line -match '\[Header\("([^"]+)"\)\]') {
        $h = $matches[1]
        if ($h -notmatch '[\uac00-\ud7a3]') {
            $tooltips += "$($i + 1): $line"
        }
    }
}
Write-Output "=== English Tooltips or Headers ==="
Write-Output "Count: $($tooltips.Count)"
$tooltips | ForEach-Object { Write-Output $_ }
