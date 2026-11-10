Select-String -Path "$env:LOCALAPPDATA\Unity\Editor\Editor.log" -Pattern "CaptureTerrain" -Context 1, 3 | Select-Object -Last 5

