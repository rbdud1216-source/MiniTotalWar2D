Get-Process | Where-Object { $_.ProcessName -like "*unity*" } | Select-Object Id, ProcessName, MainWindowTitle, MainWindowHandle

