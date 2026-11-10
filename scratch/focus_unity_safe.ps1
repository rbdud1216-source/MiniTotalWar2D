Add-Type @"
  using System;
  using System.Runtime.InteropServices;
  using System.Text;
  public class Win32 {
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
  }
"@

[Win32]::EnumWindows([Win32+EnumWindowsProc]{
    param($h, $l)
    $sb = New-Object System.Text.StringBuilder 256
    [Win32]::GetWindowText($h, $sb, 256) | Out-Null
    $title = $sb.ToString()
    if ($title -like "*MiniTotalWar2D*" -or $title -like "*식생 쇼케이스*") {
        Write-Host "Focusing window: $title"
        [Win32]::ShowWindow($h, 9)
        [Win32]::SetForegroundWindow($h)
    }
    return $true
}, [IntPtr]::Zero)

