$p = Get-Process -Id 10108
Write-Host "MainWindowHandle: $($p.MainWindowHandle)"
Write-Host "MainWindowTitle: $($p.MainWindowTitle)"

