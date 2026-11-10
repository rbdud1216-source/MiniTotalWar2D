Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win32Focus {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);
}
"@
$p = Get-Process Unity | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if ($p) {
    [Win32Focus]::keybd_event(0x12, 0, 0, 0)
    [Win32Focus]::ShowWindow($p.MainWindowHandle, 9) | Out-Null
    [Win32Focus]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
    [Win32Focus]::keybd_event(0x12, 0, 2, 0)
    Write-Host "Activated Unity HWND: $($p.MainWindowHandle)"
}
Start-Sleep -Seconds 3
Get-Content 'C:\Users\PC\AppData\Local\Unity\Editor\Editor.log' -Tail 10
