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
    public static extern bool IsWindowVisible(IntPtr hWnd);
  }
"@

[Win32]::EnumWindows({
    param($hwnd, $lparam)
    if ([Win32]::IsWindowVisible($hwnd)) {
        $sb = New-Object System.Text.StringBuilder 512
        [Win32]::GetWindowText($hwnd, $sb, 512) | Out-Null
        $title = $sb.ToString()
        if ($title.Length -gt 0) {
            Write-Host "Title: $title"
        }
    }
    return $true
}, [IntPtr]::Zero)

