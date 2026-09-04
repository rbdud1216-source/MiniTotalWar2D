$path = "c:\unityProject\MiniTotalWar2D\Assets\Scripts\Squad.cs"
$lines = Get-Content $path
$lines[36] = '    [Header("Status Variables")]'
$lines[81] = '    private void OnDestroy()'
[System.IO.File]::WriteAllLines($path, $lines, [System.Text.Encoding]::UTF8)
