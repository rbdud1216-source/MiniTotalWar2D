$proc = Get-Process -Name "Unity" | Where-Object { $_.MainWindowHandle -ne 0 }
if ($proc) {
    Write-Host "Found Unity process PID: $($proc.Id), Title: $($proc.MainWindowTitle)"
    $sig = @"
using System;
using System.Runtime.InteropServices;
public class User32 {
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
"@
    Add-Type -TypeDefinition $sig
    [User32]::ShowWindow($proc.MainWindowHandle, 9) # SW_RESTORE = 9
    [User32]::SetForegroundWindow($proc.MainWindowHandle)
    Write-Host "SetForegroundWindow called."
} else {
    Write-Host "No Unity process with MainWindowHandle."
}
