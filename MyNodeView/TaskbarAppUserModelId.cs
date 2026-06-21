using System;
using System.Runtime.InteropServices;

namespace MyNodeView;

/// <summary>
/// 通过 Windows Shell API 为窗口设置/清除 AppUserModelID。
///
/// 在 Windows 10/11 上，任务栏默认按 AppUserModelID 对窗口进行分组。
/// 同一进程中的所有窗口默认共享相同的 AppUserModelID，
/// 导致它们在任务栏上被合并到一起。
///
/// 调用 SetForWindow() 可为每个窗口分配一个独一无二的 AppUserModelID，
/// 使该窗口在任务栏上拥有独立图标，不再与其他窗口合并。
///
/// 窗口关闭前应调用 RemoveForWindow() 将属性设回 VT_EMPTY，
/// 否则系统不会回收该属性占用的资源。
/// 参考：SHGetPropertyStoreForWindow 文档中提到
/// "A window's properties must be removed before the window is closed."
/// </summary>
internal static class TaskbarAppUserModelId
{
    // ==================== PKEY_AppUserModel_ID 常量 ====================

    private static readonly Guid AppUserModelIdFolderId =
        new Guid("{9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3}");

    private const uint AppUserModelIdPropertyId = 5;

    // ==================== P/Invoke: SHGetPropertyStoreForWindow ====================

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern int SHGetPropertyStoreForWindow(
        IntPtr hwnd,
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IPropertyStore propertyStore);

    // ==================== COM 接口: IPropertyStore ====================

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig]
        int GetCount(out uint propertyCount);

        [PreserveSig]
        int GetAt(uint propertyIndex, out PropertyKey key);

        [PreserveSig]
        int GetValue(ref PropertyKey key, out PropVariant value);

        [PreserveSig]
        int SetValue(ref PropertyKey key, ref PropVariant value);

        [PreserveSig]
        int Commit();
    }

    // ==================== 辅助结构体 ====================

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey
    {
        public Guid fmtid;
        public uint pid;
    }

    /// <summary>
    /// Windows PROPVARIANT 结构体的最小定义。
    ///
    /// 内存布局（x64）：
    ///   offset 0: vt (2 bytes)
    ///   offset 2-7: reserved 填充
    ///   offset 8: ptrValue (8 bytes pointer)
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct PropVariant
    {
        public ushort vt;
        public ushort reserved1;
        public ushort reserved2;
        public ushort reserved3;
        public IntPtr ptrValue;

        private const ushort VtLpwstr = 31;

        /// <summary>
        /// 从字符串创建 PROPVARIANT（VT_LPWSTR）。
        /// 使用 CoTaskMem 分配内存，与 Windows Shell API 的内存管理约定一致。
        /// 调用方必须在不再使用时调用 FreeString() 释放内存。
        /// </summary>
        public static PropVariant FromString(string value)
        {
            var variant = new PropVariant();
            variant.vt = VtLpwstr;
            variant.ptrValue = Marshal.StringToCoTaskMemUni(value);
            return variant;
        }

        /// <summary>
        /// 释放由 FromString 分配的 CoTaskMem 内存。
        /// </summary>
        public void FreeString()
        {
            var isStringType = vt == VtLpwstr;
            var hasPointer = ptrValue != IntPtr.Zero;

            if (isStringType && hasPointer)
            {
                Marshal.FreeCoTaskMem(ptrValue);
                ptrValue = IntPtr.Zero;
                vt = 0;
            }
        }
    }

    // ==================== 公开方法 ====================

    /// <summary>
    /// 为指定窗口设置 AppUserModelID。
    ///
    /// 必须在窗口句柄创建之后调用（例如在 SourceInitialized 事件中）。
    /// 设置后，任务栏将使用此 ID 决定窗口的分组行为：
    /// 不同 ID 的窗口拥有独立的任务栏按钮，不会合并。
    ///
    /// 窗口关闭前务必调用 RemoveForWindow() 移除属性，
    /// 否则系统资源可能无法回收。
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    /// <param name="appUserModelId">AppUserModelID 字符串</param>
    public static void SetForWindow(IntPtr hwnd, string appUserModelId)
    {
        var isHandleValid = hwnd != IntPtr.Zero;
        var isIdValid = !string.IsNullOrEmpty(appUserModelId);

        if (!isHandleValid || !isIdValid)
        {
            return;
        }

        // 获取窗口属性存储接口。
        var propertyStore = GetPropertyStore(hwnd);
        if (propertyStore is null)
        {
            return;
        }

        // 构造 PKEY_AppUserModel_ID 属性键。
        var appUserModelIdKey = new PropertyKey
        {
            fmtid = AppUserModelIdFolderId,
            pid = AppUserModelIdPropertyId
        };

        // 分配 PROPVARIANT 并设置到属性存储中。
        var propVar = PropVariant.FromString(appUserModelId);

        HresultOrException hresult;

        try
        {
            hresult = HresultOrException.Success;
            propertyStore.SetValue(ref appUserModelIdKey, ref propVar);
            propertyStore.Commit();
        }
        catch (Exception ex)
        {
            // 确保在 SetValue 或 Commit 抛出异常时记录错误，
            // 但仍然在 finally 中释放 CoTaskMem。
            hresult = HresultOrException.FromException(ex);
        }
        finally
        {
            // 无论成功与否，都要释放 FromString 分配的 CoTaskMem 内存，
            // 防止内存泄漏。COM 侧在 SetValue 时已拷贝数据，
            // Commit 后本地内存即可安全释放。
            propVar.FreeString();

            // 显式释放 COM RCW，避免依赖 GC 延迟回收。
            // 子窗口可能频繁创建销毁，主动释放更可靠。
            Marshal.ReleaseComObject(propertyStore);
        }

        if (!hresult.IsSuccess)
        {
            var logMessage = $"SetForWindow 异常: {hresult.Exception?.Message}";
            System.Diagnostics.Trace.WriteLine(logMessage);
        }
    }

    /// <summary>
    /// 移除之前通过 SetForWindow() 设置的 AppUserModelID 属性。
    ///
    /// Windows 文档要求窗口关闭前调用此方法将属性设回 VT_EMPTY，
    /// 否则为属性分配的系统资源不会释放。
    /// 参考 SHGetPropertyStoreForWindow 文档：
    /// "A window's properties must be removed before the window is closed."
    ///
    /// 在窗口的 Closing 或 Closed 事件中调用此方法即可。
    /// 多次调用无副作用——属性已不存在时 Commit 为无操作。
    /// </summary>
    /// <param name="hwnd">窗口句柄</param>
    public static void RemoveForWindow(IntPtr hwnd)
    {
        var isHandleValid = hwnd != IntPtr.Zero;
        if (!isHandleValid)
        {
            return;
        }

        var propertyStore = GetPropertyStore(hwnd);
        if (propertyStore is null)
        {
            return;
        }

        var appUserModelIdKey = new PropertyKey
        {
            fmtid = AppUserModelIdFolderId,
            pid = AppUserModelIdPropertyId
        };

        try
        {
            // 将属性值设为 VT_EMPTY（new PropVariant() 的 vt 默认为 0），
            // 这相当于移除该属性，通知系统回收相关资源。
            var emptyVariant = new PropVariant();
            propertyStore.SetValue(ref appUserModelIdKey, ref emptyVariant);
            propertyStore.Commit();
        }
        catch (Exception ex)
        {
            var logMessage = $"RemoveForWindow 异常: {ex.Message}";
            System.Diagnostics.Trace.WriteLine(logMessage);
        }
        finally
        {
            Marshal.ReleaseComObject(propertyStore);
        }
    }

    // ==================== 私有辅助方法 ====================

    /// <summary>
    /// 获取窗口的 IPropertyStore 接口。
    /// 返回 null 表示获取失败。
    /// </summary>
    private static IPropertyStore? GetPropertyStore(IntPtr hwnd)
    {
        var propertyStoreGuid = typeof(IPropertyStore).GUID;

        var hresult = SHGetPropertyStoreForWindow(
            hwnd,
            ref propertyStoreGuid,
            out var propertyStore);

        var isFailed = hresult < 0;
        var isStoreNull = propertyStore is null;

        if (isFailed || isStoreNull)
        {
            return null;
        }

        return propertyStore;
    }

    /// <summary>
    /// 表示 HRESULT 或托管异常的联合结果。
    /// 用于在 try/catch 中统一记录操作成败。
    /// </summary>
    private readonly struct HresultOrException
    {
        public static HresultOrException Success => default;

        public Exception? Exception { get; private init; }

        public bool IsSuccess => Exception is null;

        public static HresultOrException FromException(Exception ex)
        {
            return new HresultOrException { Exception = ex };
        }
    }
}
