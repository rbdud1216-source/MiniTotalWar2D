Get-ChildItem -Path Assets/Settings/Vegetation/RadialMeshes | Where-Object { $_.Name -like "*TotalWar*" -or $_.Name -like "*Cluster*" } | Select-Object Name

