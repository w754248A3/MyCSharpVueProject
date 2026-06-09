using System;
using System.Runtime.InteropServices;

namespace MyNodeView;

/// <summary>
/// 通过 Windows Shell API 为窗口设置独立的 AppUserModelID。
///
/// 在 Windows 10/11 上，任务栏默认按 AppUserModelID 对窗口进行分组。
/// 同一进程中的所有窗口默认共享相同的 AppUserModelID，
/// 导致它们在任务栏上被合并到一起。
///
/// 调用 SetForWindow() 可为每个窗口分配一个独一无二的 AppUserModelID，
/// 使该窗口在任务栏上拥有独立图标，不再与其他窗口合并。
///
/// 使用方式：
/// 在窗口的 SourceInitialized 事件（此时窗口句柄已创建）中调用：
///   TaskbarAppUserModelId.SetForWindow(windowHandle, "MyApp.UniqueId");
/// </summary>
internal static class TaskbarAppUserModelId
{
    // ==================== PKEY_AppUserModel_ID 常量 ====================

    /// <summary>
    /// PKEY_AppUserModel_ID 的 fmtid 部分。
    /// 来自 Windows SDK 的 propkey.h。
    /// </summary>
    private static readonly Guid AppUserModelIdFolderId =
        new Guid("{9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3}");

    /// <summary>
    /// PKEY_AppUserModel_ID 的 pid 部分。
    /// </summary>
    private const uint AppUserModelIdPropertyId = 5;

    // ==================== P/Invoke: SHGetPropertyStoreForWindow ====================

    /// <summary>
    /// 获取窗口的属性存储接口。
    /// 通过此接口可读写窗口的 Shell 属性（如 AppUserModelID）。
    /// </summary>
    /// <param name="hwnd">目标窗口的句柄</param>
    /// <param name="riid">IPropertyStore 接口的 GUID 引用</param>
    /// <param name="propertyStore">输出的 IPropertyStore 接口指针</param>
    /// <returns>HRESULT，0 表示成功</returns>
    [DllImport("shell32.dll", SetLastError = true)]
    private static extern int SHGetPropertyStoreForWindow(
        IntPtr hwnd,
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IPropertyStore propertyStore);

    // ==================== COM 接口: IPropertyStore ====================

    /// <summary>
    /// Windows Shell 属性存储 COM 接口。
    /// GUID: {886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99}
    /// </summary>
    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        /// <summary>获取属性数量</summary>
        [PreserveSig]
        int GetCount(out uint propertyCount);

        /// <summary>按索引获取属性 Key</summary>
        [PreserveSig]
        int GetAt(uint propertyIndex, out PropertyKey key);

        /// <summary>获取属性值</summary>
        [PreserveSig]
        int GetValue(ref PropertyKey key, out PropVariant value);

        /// <summary>设置属性值</summary>
        [PreserveSig]
        int SetValue(ref PropertyKey key, ref PropVariant value);

        /// <summary>将属性变更提交到系统</summary>
        [PreserveSig]
        int Commit();
    }

    // ==================== 辅助结构体 ====================

    /// <summary>
    /// Windows PROPERTYKEY 结构体。
    /// 由 GUID（fmtid）和 DWORD（pid）组成，唯一标识一个 Shell 属性。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey
    {
        public Guid fmtid;
        public uint pid;
    }

    /// <summary>
    /// Windows PROPVARIANT 结构体的最小定义。
    /// 仅包含设置字符串值所需的字段。
    ///
    /// 内存布局（x64）：
    ///   offset 0: vt (2 bytes)
    ///   offset 2-7: reserved 填充
    ///   offset 8: ptrValue (8 bytes pointer)
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct PropVariant
    {
        /// <summary>VARTYPE 类型标识，VT_LPWSTR = 31</summary>
        public ushort vt;

        /// <summary>保留字段 1</summary>
        public ushort reserved1;

        /// <summary>保留字段 2</summary>
        public ushort reserved2;

        /// <summary>保留字段 3</summary>
        public ushort reserved3;

        /// <summary>联合体中的指针字段，对于 VT_LPWSTR 存放字符串指针</summary>
        public IntPtr ptrValue;

        /// <summary>VT_LPWSTR 常量</summary>
        private const ushort VtLpwstr = 31;

        /// <summary>
        /// 从字符串创建 PROPVARIANT。
        /// 使用 CoTaskMem 分配内存，与 Windows Shell API 的内存管理约定一致。
        /// </summary>
        public static PropVariant FromString(string value)
        {
            var variant = new PropVariant();

            // 设置类型为 VT_LPWSTR（以 null 结尾的宽字符串）。
            variant.vt = VtLpwstr;

            // 将字符串复制到 CoTaskMem，Shell API 会按 COM 约定管理这块内存。
            variant.ptrValue = Marshal.StringToCoTaskMemUni(value);

            return variant;
        }

        /// <summary>
        /// 释放由 FromString 分配的 CoTaskMem 内存。
        /// 调用 Commit() 后应立即调用此方法，防止内存泄漏。
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
    /// </summary>
    /// <param name="hwnd">窗口句柄，可通过 new WindowInteropHelper(window).Handle 获取</param>
    /// <param name="appUserModelId">
    /// 要设置的 AppUserModelID 字符串。
    /// 建议使用 "CompanyName.AppName.WindowType" 格式，
    /// 例如 "MyNodeView.SingleFilePage"
    /// </param>
    public static void SetForWindow(IntPtr hwnd, string appUserModelId)
    {
        // 参数有效性检查。
        var isHandleValid = hwnd != IntPtr.Zero;
        var isIdValid = !string.IsNullOrEmpty(appUserModelId);

        if (!isHandleValid || !isIdValid)
        {
            return;
        }

        // 获取 IPropertyStore 接口的 GUID。
        var propertyStoreGuid = typeof(IPropertyStore).GUID;

        // 调用 Shell API 获取窗口属性存储。
        var hresult = SHGetPropertyStoreForWindow(
            hwnd,
            ref propertyStoreGuid,
            out var propertyStore);

        // HRESULT 小于 0 表示调用失败。
        var isFailed = hresult < 0;
        var isStoreNull = propertyStore is null;

        if (isFailed || isStoreNull)
        {
            return;
        }

        // 构造 PKEY_AppUserModel_ID 属性键。
        var appUserModelIdKey = new PropertyKey
        {
            fmtid = AppUserModelIdFolderId,
            pid = AppUserModelIdPropertyId
        };

        // 将 AppUserModelID 包装为 PROPVARIANT。
        var propVar = PropVariant.FromString(appUserModelId);

        // 设置属性值并提交到系统。
        propertyStore.SetValue(ref appUserModelIdKey, ref propVar);
        propertyStore.Commit();

        // 释放临时分配的字符串内存。
        propVar.FreeString();
    }
}
