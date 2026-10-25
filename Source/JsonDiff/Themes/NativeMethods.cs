using System.Runtime.InteropServices;

namespace JsonDiff.Themes;

internal static class NativeMethods
{
    /// <summary>Asks for the dark title bar; Windows 10 20H1 and later.</summary>
    public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
