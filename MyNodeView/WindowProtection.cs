using System;
using System.Runtime.InteropServices;

namespace MyNodeView;

/// <summary>
/// 窗口反截图保护。
///
/// 通过调用 Windows 10 2004+ 的 SetWindowDisplayAffinity API，
/// 设置窗口内容在截图/录屏/远程桌面时不可见。
///
/// 仅在 Release 构建中生效，DEBUG 构建不做限制。
/// </summary>
internal static class WindowProtection
{
    // ==================== P/Invoke ====================

    /// <summary>
    /// 设置窗口内容对屏幕捕获的可见性。
    /// https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowdisplayaffinity
    /// </summary>
    /// <param name="hWnd">目标窗口句柄</param>
    /// <param name="dwAffinity">
    /// WDA_NONE = 0x00 —— 不限制（默认）
    /// WDA_MONITOR = 0x01 —— 仅在显示器上可见
    /// WDA_EXCLUDEFROMCAPTURE = 0x11 —— 从屏幕捕获中排除（Windows 10 2004+）
    /// </param>
    [DllImport("user32.dll")]
    private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

    // ==================== 常量 ====================

    /// <summary>
    /// 将窗口内容从屏幕捕获（截图、录屏、远程桌面等）中排除。
    /// 需要 Windows 10 build 19041（2004 版本）及以上。
    /// </summary>
    private const uint WDA_EXCLUDEFROMCAPTURE = 0x11;

    // ==================== 公开方法 ====================

    /// <summary>
    /// 设置窗口内容在截图时不可见。
    ///
    /// 必须在窗口句柄创建之后调用（例如在 SourceInitialized 事件中）。
    /// 仅在 Release 构建中实际调用 API，DEBUG 构建不做任何操作。
    /// </summary>
    /// <param name="hwnd">窗口句柄，可通过 new WindowInteropHelper(window).Handle 获取</param>
    public static void HideFromScreenshots(IntPtr hwnd)
    {
        // 参数有效性检查。
        var isHandleValid = hwnd != IntPtr.Zero;
        if (!isHandleValid)
        {
            return;
        }

#if !DEBUG
        // 仅在 Release 构建中启用反截图保护。
        // DEBUG 构建需要方便开发调试，不做截图限制。
        SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE);
#endif
    }
}
