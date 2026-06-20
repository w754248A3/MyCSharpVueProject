using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;

namespace MyNodeView;

/// <summary>
/// 子窗口 —— 仅包含一个 WebView2 控件的非模态窗口。
///
/// 设计要点：
/// - 与主窗口共享 CoreWebView2Environment，减少浏览器进程开销
/// - 拥有独立的任务栏按钮（不设置 Owner，ShowInTaskbar=True）
/// - 通过 ChildWindowDescriptor.Key 标识窗口类型，用于去重和配置存储
/// - 窗口尺寸独立存储，关闭时写回 AppConfig
/// </summary>
public partial class ChildWindow : Window
{
    // ==================== 依赖注入的字段 ====================

    private readonly AppConfig _appConfig;
    private readonly WebView2EnvironmentService _environmentService;
    private readonly ChildWindowDescriptor _descriptor;

    /// <summary>
    /// 主窗口 WebView2Url 的 Scheme + Host 部分，用于拼接子窗口完整 URL。
    /// </summary>
    private readonly string _baseUrl;

    // ==================== 构造函数 ====================

    /// <summary>
    /// 创建子窗口实例。
    /// </summary>
    /// <param name="appConfig">应用配置，用于读取/保存窗口尺寸</param>
    /// <param name="environmentService">共享的 WebView2 环境服务</param>
    /// <param name="descriptor">窗口类型描述，定义 Key、标题、URL 路径等</param>
    /// <param name="baseUrl">
    /// 主窗口 WebView2 导航 URL 的 Scheme + Host 部分，
    /// 用于拼接子窗口的完整 URL
    /// </param>
    public ChildWindow(
        AppConfig appConfig,
        WebView2EnvironmentService environmentService,
        ChildWindowDescriptor descriptor,
        string baseUrl)
    {
        _appConfig = appConfig;
        _environmentService = environmentService;
        _descriptor = descriptor;
        _baseUrl = baseUrl;

        InitializeComponent();

        // 应用窗口描述中的配置参数。
        Title = descriptor.Title;
        Topmost = descriptor.Topmost;

        // 从配置中恢复该 Key 对应窗口的上次尺寸，没有则使用描述中的默认值。
        RestoreWindowSize();

        // 注册生命周期事件。
        Loaded += Window_Loaded;
        Closing += Window_Closing;

        // 在窗口句柄创建后立即设置独立的 AppUserModelID，
        // 使子窗口在任务栏上拥有独立按钮，不与主窗口合并。
        SourceInitialized += Window_SourceInitialized;
    }

    // ==================== 属性 ====================

    /// <summary>
    /// 窗口的唯一标识 Key，与 ChildWindowDescriptor.Key 对应。
    /// 外部通过此属性管理窗口的去重。
    /// </summary>
    public string WindowKey => _descriptor.Key;

    // ==================== 窗口生命周期 ====================

    /// <summary>
    /// 窗口句柄创建后立即设置独立的 AppUserModelID，
    /// 使子窗口在任务栏上不被合并到主窗口图标下。
    /// SourceInitialized 是 WPF 中窗口句柄可用的最早时机。
    /// </summary>
    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        // 通过 WindowInteropHelper 获取 WPF 窗口的底层 HWND。
        var windowHandle = new WindowInteropHelper(this).Handle;

        // 生成该窗口类型专属的 AppUserModelID。
        var appUserModelId = BuildAppUserModelId();

        // 调用 Shell API 为窗口设置独立的 AppUserModelID。
        TaskbarAppUserModelId.SetForWindow(windowHandle, appUserModelId);

        // 在 Release 构建中禁止截图工具捕获本窗口内容。
        WindowProtection.HideFromScreenshots(windowHandle);
    }

    /// <summary>
    /// 窗口加载完成后初始化 WebView2。
    /// </summary>
    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await InitWebView2Async();
        Loaded -= Window_Loaded;
    }

    /// <summary>
    /// 窗口关闭前弹出确认对话框。
    /// 用户确认后将当前窗口尺寸写入配置对象（由 Program.Main 统一落盘）。
    /// </summary>
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        // 关闭前确认，避免误操作。
        var confirmResult = MessageBox.Show(
            "确定要关闭此窗口吗？",
            Title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirmResult == MessageBoxResult.No)
        {
            e.Cancel = true;
            return;
        }

        // 保存当前窗口尺寸到配置（按 Key 独立存储）。
        SaveWindowSize();
    }

    // ==================== WebView2 初始化 ====================

    /// <summary>
    /// 使用共享的 CoreWebView2Environment 初始化 WebView2 控件。
    /// 由于与主窗口共用同一环境和 UserDataFolder，
    /// 浏览器进程、cookie、缓存等资源均被共享。
    /// </summary>
    private async Task InitWebView2Async()
    {
        // 获取与主窗口共享的 CoreWebView2Environment。
        var sharedEnvironment = await _environmentService.GetEnvironmentAsync();

        // 将 WebView2 控件绑定到共享环境。
        await webView2.EnsureCoreWebView2Async(sharedEnvironment);

        // 拼接子窗口的目标 URL。
        var targetUrl = BuildTargetUrl();
        webView2.CoreWebView2.Navigate(targetUrl);
    }

    // ==================== 窗口尺寸管理 ====================

    /// <summary>
    /// 从配置中恢复该窗口的上次尺寸。
    /// 如果配置中没有记录（首次创建），则使用 ChildWindowDescriptor 中的默认值。
    /// </summary>
    private void RestoreWindowSize()
    {
        var windowKey = _descriptor.Key;

        // 检查配置中是否有该 Key 对应的尺寸记录。
        if (_appConfig.ChildWindowSizes.TryGetValue(windowKey, out var savedSize))
        {
            Width = savedSize.Width;
            Height = savedSize.Height;
        }
        else
        {
            // 首次创建，使用描述中的默认尺寸。
            Width = _descriptor.DefaultWidth;
            Height = _descriptor.DefaultHeight;
        }
    }

    /// <summary>
    /// 将当前窗口尺寸写入配置对象（按 Key 独立存储）。
    /// 不会立即落盘，由 Program.Main 在程序退出时统一保存。
    /// </summary>
    private void SaveWindowSize()
    {
        var windowKey = _descriptor.Key;

        var currentSize = new WindowSize
        {
            Width = Width,
            Height = Height
        };

        // 更新或添加该 Key 对应窗口的尺寸记录。
        _appConfig.ChildWindowSizes[windowKey] = currentSize;
    }

    // ==================== AppUserModelID 生成 ====================

    /// <summary>
    /// 为该窗口生成唯一的 AppUserModelID。
    /// 格式为 "MyNodeView.{WindowKey}"，
    /// 不同 Key 的窗口拥有不同的 ID，在任务栏上独立显示。
    /// </summary>
    private string BuildAppUserModelId()
    {
        var appUserModelId = "MyNodeView." + _descriptor.Key;
        return appUserModelId;
    }

    // ==================== URL 构建 ====================

    /// <summary>
    /// 根据主窗口的 base URL 和 ChildWindowDescriptor 的 UrlPath 拼接子窗口目标地址。
    /// 例如 baseUrl="http://localhost:34114", UrlPath="/SingleFilePage/"
    /// → "http://localhost:34114/SingleFilePage/"
    /// </summary>
    private string BuildTargetUrl()
    {
        // 使用主窗口 URL 的 scheme + host 拼接子窗口的目标路径。
        // 例如 _baseUrl="http://localhost:34114", UrlPath="/SingleFilePage/"
        // → "http://localhost:34114/SingleFilePage/"
        var trimmedBase = _baseUrl.TrimEnd('/');
        var trimmedPath = _descriptor.UrlPath.TrimStart('/');
        var targetUrl = trimmedBase + "/" + trimmedPath;
        return targetUrl;
    }
}
