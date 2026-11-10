Get-ChildItem -Path Assets/Editor -Filter *.cs | Sort-Object LastWriteTime -Descending | Select-Object Name, LastWriteTime -First 10

