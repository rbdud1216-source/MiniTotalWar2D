$ddsFiles = Get-ChildItem -Path "Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Textures" -Filter "*grass*.dds"
$results = @()
foreach ($file in $ddsFiles) {
    $baseName = $file.BaseName
    $pngPath = "Assets/TotalWar_ThreeKingdoms_Extracted/Vegetation/Textures/$baseName.png"
    $hasPng = Test-Path $pngPath
    $results += [PSCustomObject]@{
        Name = $baseName
        HasPNG = $hasPng
    }
}
$results | Format-Table -AutoSize

