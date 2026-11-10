Get-CimInstance Win32_Process -Filter "Name LIKE '%Unity%'" | Select-Object ProcessId, Name, CommandLine

