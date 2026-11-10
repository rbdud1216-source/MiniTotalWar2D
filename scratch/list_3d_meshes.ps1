Get-ChildItem -Path Assets/Settings/Vegetation/RadialMeshes | Where-Object { $_.Name -like "*Reed*" -or $_.Name -like "*Bush*" -or $_.Name -like "*Thick*" -or $_.Name -like "*Forest0_9*" -or $_.Name -like "*3D*" } | Select-Object Name

