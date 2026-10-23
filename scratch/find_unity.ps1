$sig = @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public class WindowFinder {
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    public static IntPtr FoundHwnd = IntPtr.Zero;
    public static string FoundTitle = "";

    public static bool Callback(IntPtr hWnd, IntPtr lParam) {
        StringBuilder sb = new StringBuilder(512);
        GetWindowText(hWnd, sb, sb.Capacity);
        string title = sb.ToString();
        if (title.Contains("Unity") || title.Contains("MiniTotalWar")) {
            uint pid;
            GetWindowThreadProcessId(hWnd, out pid);
            Console.WriteLine($"Found Window: '{title}' (PID {pid}, HWND 0x{hWnd.ToInt64():X})");
            FoundHwnd = hWnd;
            FoundTitle = title;
            return false; // stop
        }
        return true;
    }

    public static void ActivateUnity() {
        EnumWindows(Callback, IntPtr.Zero);
        if (FoundHwnd != IntPtr.Zero) {
            ShowWindow(FoundHwnd, 9);
            SetForegroundWindow(FoundHwnd);
            Console.WriteLine("Activated window!");
        }
    }
}
"@
Add-Type -TypeDefinition $sig
[WindowFinder]::ActivateUnity()
