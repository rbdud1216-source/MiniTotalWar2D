Add-Type @"
  using System;
  using System.Text;
  using System.Runtime.InteropServices;
  public class Win32 {
    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
  }
"@

$list = @()
[Win32]::EnumWindows({
    param($hwnd, $lparam)
    if ([Win32]::IsWindowVisible($hwnd)) {
        $sb = New-Object System.Text.StringBuilder 512
        [Win32]::GetWindowText($hwnd, $sb, 512) | Out-Null
        $title = $sb.ToString()
        if ($title.Length -gt 0) {
            $pid = 0
            [Win32]::GetWindowThreadProcessId($hwnd, [ref]$pid) | Out-Null
            $p = Get-Process -Id $pid -ErrorAction SilentlyContinue
            if ($title -like "*MiniTotalWar*" -or $title -like "*Unity*") {
                Write-Host "HWND: $hwnd | PID: $pid | Proc: $($p.ProcessName) | Title: $title"
                [Win32]::SetForegroundWindow($hwnd)
            }
        }
    }
    return $true
}, [IntPtr]::Zero)

