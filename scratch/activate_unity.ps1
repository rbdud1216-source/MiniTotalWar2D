$wshell = New-Object -ComObject WScript.Shell
Get-Process -Name "Unity*" | ForEach-Object {
    Write-Host "Activating PID: $($_.Id) - $($_.ProcessName)"
    $wshell.AppActivate($_.Id)
}
Start-Sleep -Seconds 2
