Add-Type @"
  using System;
  using System.Runtime.InteropServices;
  public class Win32 {
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool EnumThreadWindows(int dwThreadId, EnumThreadDelegate lpfn, IntPtr lParam);

    public delegate bool EnumThreadDelegate(IntPtr hWnd, IntPtr lParam);
  }
"@

$p = Get-Process -Id 10108
Write-Host "Process Name: $($p.ProcessName), ID: $($p.Id)"

$handles = New-Object System.Collections.Generic.List[IntPtr]
foreach ($thread in $p.Threads) {
    [Win32]::EnumThreadWindows($thread.Id, [Win32+EnumThreadDelegate]{
        param($h, $l)
        $handles.Add($h)
        return $true
    }, [IntPtr]::Zero)
}

Write-Host "Found $($handles.Count) window handles for thread."
foreach ($h in $handles) {
    [Win32]::ShowWindow($h, 9) # SW_RESTORE
    [Win32]::SetForegroundWindow($h)
}

