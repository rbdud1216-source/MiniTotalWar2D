Select-String -Path "$env:LOCALAPPDATA\Unity\Editor\Editor.log" -Pattern "BakeStandardFoliageToTerrain|AutoBakeRunner|BakeFoliage|EnsureDefaultPrototypes" -Context 3, 5

