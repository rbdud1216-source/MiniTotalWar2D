$wshell = New-Object -ComObject wscript.shell
$success = $wshell.AppActivate("MiniTotalWar2D")
Write-Host "AppActivate MiniTotalWar2D: $success"
Start-Sleep -Seconds 2
