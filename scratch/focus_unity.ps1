$p = Get-Process -Name "Unity" -ErrorAction SilentlyContinue | Select-Object -First 1
if ($p) {
    $wshell = New-Object -ComObject WScript.Shell
    $wshell.AppActivate($p.Id)
    Start-Sleep -Seconds 1
}
