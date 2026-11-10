$wshell = New-Object -ComObject wscript.shell
$success = $wshell.AppActivate("Unity")
Write-Host "AppActivate Unity: $success"
Start-Sleep -Seconds 2

# 유니티 메뉴 직접 실행 (Tools -> MiniTotalWar -> Bake Standard Foliage to Terrain)
# Alt + t 누르고 m, b 등 키 입력 또는 단축키 전달
$wshell.SendKeys("%t")
Start-Sleep -Milliseconds 400
$wshell.SendKeys("{DOWN}")
Start-Sleep -Milliseconds 200
$wshell.SendKeys("{RIGHT}")
Start-Sleep -Milliseconds 200
$wshell.SendKeys("{ENTER}")
Start-Sleep -Seconds 3

