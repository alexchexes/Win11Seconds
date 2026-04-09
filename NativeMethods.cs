using System.Drawing;
using System.Runtime.InteropServices;

static class NativeMethods
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);

    [DllImport("user32.dll")]
    public static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam);

    public const int WM_NCLBUTTONDOWN = 0xA1;
    public const int HTCAPTION = 0x2;

    private const int S_OK = 0;

    public enum DwmWindowAttribute
    {
        WindowCornerPreference = 33,
        BorderColor = 34,
        CaptionColor = 35
    }

    public enum DwmWindowCornerPreference
    {
        Default = 0,
        DoNotRound = 1,
        Round = 2,
        RoundSmall = 3
    }

    public static bool TrySetRoundedCorners(IntPtr hwnd)
    {
        int preference = (int)DwmWindowCornerPreference.Round;
        return TrySetWindowAttribute(hwnd, DwmWindowAttribute.WindowCornerPreference, preference);
    }

    public static bool TrySetCaptionColor(IntPtr hwnd, Color color)
    {
        int colorRef = color.ToArgb() & 0x00FFFFFF;
        return TrySetWindowAttribute(hwnd, DwmWindowAttribute.CaptionColor, colorRef);
    }

    private static bool TrySetWindowAttribute(
        IntPtr hwnd,
        DwmWindowAttribute attribute,
        int value)
    {
        return DwmSetWindowAttribute(hwnd, (int)attribute, ref value, sizeof(int)) == S_OK;
    }
}
