Add-Type @"
  using System;
  using System.Text;
  using System.Runtime.InteropServices;
  using System.Collections.Generic;

  public class WinEnum {
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    public static List<string> FindWindows(string keyword) {
      List<string> res = new List<string>();
      EnumWindows((hWnd, lParam) => {
        if (IsWindowVisible(hWnd)) {
          int len = GetWindowTextLength(hWnd);
          if (len > 0) {
            StringBuilder sb = new StringBuilder(len + 1);
            GetWindowText(hWnd, sb, sb.Capacity);
            string title = sb.ToString();
            if (title.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) {
              res.Add(hWnd.ToString() + ": " + title);
              SetForegroundWindow(hWnd);
            }
          }
        }
        return true;
      }, IntPtr.Zero);
      return res;
    }
  }
"@

$found = [WinEnum]::FindWindows("MiniTotalWar2D")
foreach ($f in $found) {
    Write-Host "Found & Focused: $f"
}

