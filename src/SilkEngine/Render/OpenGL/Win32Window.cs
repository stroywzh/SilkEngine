using System;
using System.Runtime.InteropServices;

namespace SilkEngine.Render.OpenGL;

[Obsolete("Editor 嵌入窗口逻辑待重构：当前实现仅限内部调试使用")]
/// <summary>
/// Win32 窗口操作辅助
/// <br/>供 Editor 嵌入使用，封装 SetParent / SetWindowLong(Ptr) / ShowWindow
/// </summary>
internal static class Win32Window
{
    private const int GWL_STYLE = -16;
    private const uint WS_CHILD = 0x40000000;
    private const uint WS_VISIBLE = 0x10000000;
    private const int SW_SHOW = 5;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    // 64 位进程必须使用 SetWindowLongPtr（SetWindowLongW 会截断 64 位句柄/样式值）
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    /// <summary>将子窗口挂入父窗口并设为 WS_CHILD 样式；失败时抛 InvalidOperationException（含 GetLastError 错误码）</summary>
    public static void SetParentWindow(IntPtr childHwnd, IntPtr parentHwnd)
    {
        SetParent(childHwnd, parentHwnd);

        IntPtr newStyle = (IntPtr)(WS_CHILD | WS_VISIBLE);
        IntPtr result = IntPtr.Size == 8
            ? SetWindowLongPtr(childHwnd, GWL_STYLE, newStyle)
            : SetWindowLong(childHwnd, GWL_STYLE, newStyle);
        if (result == IntPtr.Zero)
            throw new InvalidOperationException(
                $"SetWindowLong 失败，错误码 {Marshal.GetLastWin32Error()}"
            );

        ShowWindow(childHwnd, SW_SHOW);
    }
}
