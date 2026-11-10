$procs = Get-Process Unity -ErrorAction SilentlyContinue
foreach ($p in $procs) {
    Write-Host "Unity PID: $($p.Id), Handle: $($p.MainWindowHandle), Title: $($p.MainWindowTitle)"
}

