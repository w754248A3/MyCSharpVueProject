using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

namespace MyNodeView;

/// <summary>
/// WebView2 环境共享服务。
/// 确保主窗口和所有子窗口共用同一个 CoreWebView2Environment 实例，
/// 从而共享底层浏览器进程和用户数据目录，减少内存占用。
///
/// 无痕浏览模式：
/// 通过 CoreWebView2EnvironmentOptions 的 AdditionalBrowserArguments
/// 传入 --incognito 标志，使所有 WebView2 窗口以类似 Chrome 无痕模式的方式运行。
/// 浏览数据（Cookie、缓存、历史记录等）在窗口关闭后不会保留到磁盘。
///
/// 此服务注册为 DI 单例，由 Program.Main 在启动时创建。
/// </summary>
public class WebView2EnvironmentService
{
    // ==================== 字段 ====================

    /// <summary>
    /// 缓存的 CoreWebView2Environment 实例。
    /// 仅在首次调用 GetEnvironmentAsync 时创建，后续复用。
    /// </summary>
    private CoreWebView2Environment? _cachedEnvironment;

    /// <summary>
    /// WebView2 用户数据目录的路径。
    /// 所有窗口共用此目录，确保 cookie、缓存等数据一致。
    /// </summary>
    public string UserDataFolder { get; }

    // ==================== 构造函数 ====================

    /// <summary>
    /// 初始化服务，确定用户数据目录位置。
    /// </summary>
    public WebView2EnvironmentService()
    {
        UserDataFolder = ResolveUserDataFolder();
    }

    // ==================== 公开方法 ====================

    /// <summary>
    /// 获取共享的 CoreWebView2Environment 实例。
    /// 首次调用时异步创建，后续调用直接返回已缓存的实例。
    /// 每个窗口在初始化 WebView2 控件时调用此方法，
    /// 并通过 EnsureCoreWebView2Async(environment) 绑定到共享环境。
    /// </summary>
    public async Task<CoreWebView2Environment> GetEnvironmentAsync()
    {
        // 环境已创建，直接返回缓存实例。
        if (_cachedEnvironment is not null)
        {
            return _cachedEnvironment;
        }

        // 首次调用：创建 CoreWebView2Environment 并缓存。
        // 此操作会启动 WebView2 浏览器进程（如果尚未运行）。
        //
        // --incognito 标志使浏览器以无痕模式运行，
        // 不会将浏览数据（cookie、缓存、localStorage 等）持久化到磁盘。
        var options = new CoreWebView2EnvironmentOptions
        {
            AdditionalBrowserArguments = "--incognito"
        };

        _cachedEnvironment = await CoreWebView2Environment.CreateAsync(
            userDataFolder: UserDataFolder,
            options: options);

        return _cachedEnvironment;
    }

    // ==================== 辅助方法 ====================

    /// <summary>
    /// 计算 WebView2 用户数据目录路径。
    /// 目录位于程序可执行文件同级的 "MyNodeView.exe.WebView2" 文件夹中。
    /// </summary>
    private static string ResolveUserDataFolder()
    {
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var userDataPath = Path.Combine(baseDirectory, "MyNodeView.exe.WebView2");
        return userDataPath;
    }
}
