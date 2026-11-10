Get-Process -Name "*Unity*" -ErrorAction SilentlyContinue | Select-Object Id, ProcessName, MainWindowTitle
