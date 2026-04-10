using System.Runtime.InteropServices;

namespace Win11Seconds;

static class NativeMethods
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmFlush();

    [DllImport("user32.dll")]
    public static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RedrawWindow(
        IntPtr hWnd,
        IntPtr lprcUpdate,
        IntPtr hrgnUpdate,
        uint flags);

    public const int WM_NCLBUTTONDOWN = 0xA1;
    public const int HTCAPTION = 0x2;

    private const int S_OK = 0;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const uint RDW_INVALIDATE = 0x0001;
    private const uint RDW_FRAME = 0x0400;
    private const uint RDW_UPDATENOW = 0x0100;
    private const uint RDW_ALLCHILDREN = 0x0080;

    public enum DwmWindowAttribute
    {
        UseImmersiveDarkMode = 20,
        WindowCornerPreference = 33,
        BorderColor = 34,
        CaptionColor = 35,
        SystemBackdropType = 38
    }

    public enum DwmWindowCornerPreference
    {
        Default = 0,
        DoNotRound = 1,
        Round = 2,
        RoundSmall = 3
    }

    public enum DwmSystemBackdropType
    {
        Auto = 0,
        None = 1,
        MainWindow = 2,
        TransientWindow = 3,
        TabbedWindow = 4
    }

    public static bool TrySetRoundedCorners(IntPtr hwnd)
    {
        int preference = (int)DwmWindowCornerPreference.Round;
        return TrySetWindowAttribute(hwnd, DwmWindowAttribute.WindowCornerPreference, preference);
    }

    public static bool TrySetImmersiveDarkMode(IntPtr hwnd, bool enabled)
    {
        int value = enabled ? 1 : 0;
        return TrySetWindowAttribute(hwnd, DwmWindowAttribute.UseImmersiveDarkMode, value);
    }

    public static bool TrySetCaptionColor(IntPtr hwnd, Color color)
    {
        int colorRef = color.ToArgb() & 0x00FFFFFF;
        return TrySetWindowAttribute(hwnd, DwmWindowAttribute.CaptionColor, colorRef);
    }

    public static bool TryResetCaptionColor(IntPtr hwnd)
    {
        return TrySetColorAttribute(hwnd, DwmWindowAttribute.CaptionColor, DwmColorSpecialValue.Default);
    }

    public static bool TryHideBorder(IntPtr hwnd)
    {
        return TrySetColorAttribute(hwnd, DwmWindowAttribute.BorderColor, DwmColorSpecialValue.None);
    }

    public static bool TrySetSystemBackdropType(IntPtr hwnd, DwmSystemBackdropType backdropType)
    {
        int value = (int)backdropType;
        return TrySetWindowAttribute(hwnd, DwmWindowAttribute.SystemBackdropType, value);
    }

    public static bool TryExtendFrameIntoClientArea(IntPtr hwnd, bool enable)
    {
        var margins = enable ? Margins.WholeWindow : default;
        return DwmExtendFrameIntoClientArea(hwnd, ref margins) == S_OK;
    }

    public static bool TryNotifyFrameChanged(IntPtr hwnd)
    {
        return SetWindowPos(
            hwnd,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
    }

    public static void RefreshWindow(IntPtr hwnd, bool includeFrame)
    {
        uint flags = RDW_INVALIDATE | RDW_ALLCHILDREN | RDW_UPDATENOW;
        if (includeFrame)
        {
            flags |= RDW_FRAME;
        }

        RedrawWindow(
            hwnd,
            IntPtr.Zero,
            IntPtr.Zero,
            flags);
        _ = DwmFlush();
    }

    private static bool TrySetWindowAttribute(
        IntPtr hwnd,
        DwmWindowAttribute attribute,
        int value)
    {
        return DwmSetWindowAttribute(hwnd, (int)attribute, ref value, sizeof(int)) == S_OK;
    }

    private static bool TrySetColorAttribute(
        IntPtr hwnd,
        DwmWindowAttribute attribute,
        DwmColorSpecialValue specialValue)
    {
        int value = unchecked((int)specialValue);
        return TrySetWindowAttribute(hwnd, attribute, value);
    }

    private enum DwmColorSpecialValue : uint
    {
        Default = 0xFFFFFFFF,
        None = 0xFFFFFFFE
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;

        public static Margins WholeWindow => new()
        {
            Left = -1,
            Right = -1,
            Top = -1,
            Bottom = -1
        };
    }
}
