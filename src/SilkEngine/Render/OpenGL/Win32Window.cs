using System;
using System.Runtime.InteropServices;

namespace SilkEngine.Render.OpenGL;

[Obsolete]
/// <summary>
/// Win32 窗口操作辅助
/// <br/>供 Editor 嵌入使用，封装 SetParent / SetWindowLong / ShowWindow
/// </summary>
internal static class Win32Window
{
    private const int GWL_STYLE = -16;
    private const uint WS_CHILD = 0x40000000;
    private const uint WS_VISIBLE = 0x10000000;
    private const int SW_SHOW = 5;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SetWindowLongW(IntPtr hWnd, int nIndex, uint dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    /// <summary>将子窗口挂入父窗口并设为 WS_CHILD 样式</summary>
    public static void SetParentWindow(IntPtr childHwnd, IntPtr parentHwnd)
    {
        SetParent(childHwnd, parentHwnd);
        SetWindowLongW(childHwnd, GWL_STYLE, WS_CHILD | WS_VISIBLE);
        ShowWindow(childHwnd, SW_SHOW);
    }
}
