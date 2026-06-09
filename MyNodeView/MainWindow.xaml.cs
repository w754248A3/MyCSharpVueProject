using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace MyNodeView;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    // ==================== 子窗口类型定义 ====================

    /// <summary>
    /// "单文件页面" 子窗口的描述。
    /// Key 为 "SingleFilePage"，用于窗口去重和配置存储。
    /// 要新增子窗口类型，只需仿照此模式定义新的 descriptor
    /// 并在菜单和事件处理中添加对应项即可。
    /// </summary>
    private static readonly ChildWindowDescriptor SingleFilePageDescriptor = new()
    {
        Key = "SingleFilePage",
        Title = "单文件页面",
        UrlPath = "/SingleFilePage/",
        Topmost = false,
        DefaultWidth = 600,
        DefaultHeight = 400
    };

    // ==================== 依赖注入的字段 ====================

    private readonly AppConfig _appConfig;
    private readonly WebView2EnvironmentService _environmentService;
    private string _webApiMessage = "";

    /// <summary>
    /// 当前打开的子窗口字典，以 ChildWindowDescriptor.Key 为索引。
    /// 用于确保相同 Key 的窗口只有一个实例存在。
    /// </summary>
    private readonly Dictionary<string, ChildWindow> _openChildWindows = new();

    // ==================== 构造函数 ====================

    /// <summary>
    /// 通过依赖注入接收 AppConfig 和 WebView2EnvironmentService。
    /// </summary>
    public MainWindow(AppConfig appConfig, WebView2EnvironmentService environmentService)
    {
        _appConfig = appConfig;
        _environmentService = environmentService;

        InitializeComponent();
        Loaded += Window_Loaded;
        Closing += Window_Closing;
    }

    // ==================== 窗口生命周期 ====================

    /// <summary>
    /// 窗口加载完成后再初始化 WebView2，避免控件尚未创建就访问 CoreWebView2。
    /// </summary>
    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        InitWebView2();
        Loaded -= Window_Loaded;
    }

    /// <summary>
    /// 窗口关闭前保存当前窗口大小和所有子窗口尺寸到配置对象。
    /// 注意：这里只是写入内存中的配置对象，实际落盘由 Program.Main 在退出时统一执行。
    /// </summary>
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        // 关闭窗口前做一次确认，避免误操作退出整个桌面应用。
        var confirmResult = MessageBox.Show(
            "确定要退出吗？", "确认退出",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirmResult == MessageBoxResult.No)
        {
            e.Cancel = true;
            return;
        }

        // 关闭所有已打开的子窗口，让它们有机会保存各自的尺寸。
        CloseAllChildWindows();

        // 在用户确认退出后，将当前窗口尺寸记录到配置对象中。
        // 程序正常退出后会由 Program.Main 统一将配置写回磁盘。
        _appConfig.WindowWidth = Width;
        _appConfig.WindowHeight = Height;
    }

    // ==================== 主窗口 WebView2 URL 的基地址 ====================

    /// <summary>
    /// 获取主窗口 WebView2Url 的 Scheme + Host 部分。
    /// 子窗口使用此基地址拼装自己的完整 URL。
    /// 例如 WebView2Url="http://localhost:34114/" → baseUrl="http://localhost:34114"
    /// </summary>
    private string GetBaseUrl()
    {
        var mainUri = new Uri(_appConfig.WebView2Url);
        var baseUrl = mainUri.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped);
        return baseUrl;
    }

    // ==================== 菜单事件处理 ====================

    private void ReLoad_Click(object sender, RoutedEventArgs e)
    {
        // 重新加载当前 WebView2 页面，便于调试前端界面。
        webView2.CoreWebView2.Reload();
    }

    private void Message_Click(object sender, RoutedEventArgs e)
    {
        // 显示后台 Web API 写入到窗口中的测试消息。
        MessageBox.Show(_webApiMessage);
    }

    /// <summary>
    /// Web API 运行在后台线程，WPF UI 只能在主线程更新。
    /// </summary>
    public void UpdateMessage(string message)
    {
        Dispatcher.Invoke(() =>
        {
            _webApiMessage = message;
        });
    }

    private void Test_Click(object sender, RoutedEventArgs e)
    {
        // 预留菜单入口：当前没有测试动作。
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        // 预留菜单入口：当前没有导出动作。
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        // 保留原有导入示例代码，便于后续继续实现数据导入功能。
        var json = File.ReadAllText("json_data.json", Encoding.UTF8);
        var nodes = JsonSerializer.Deserialize<List<NodeData>>(json);
    }

    // ==================== 子窗口管理 ====================

    /// <summary>
    /// 菜单按钮"打开单文件页面"的点击事件。
    /// 使用预定义的 SingleFilePageDescriptor 创建或激活子窗口。
    /// </summary>
    private void OpenSingleFilePage_Click(object sender, RoutedEventArgs e)
    {
        CreateOrActivateChildWindow(SingleFilePageDescriptor);
    }

    /// <summary>
    /// 根据描述创建子窗口，或激活已存在的同 Key 子窗口。
    ///
    /// 去重逻辑：以 descriptor.Key 为标识，
    /// - 如果该 Key 对应的窗口尚未创建，则创建新窗口
    /// - 如果该 Key 对应的窗口已存在，则将其置顶并聚焦
    ///
    /// 扩展方式：要新增一种子窗口，只需：
    /// 1. 定义一个 ChildWindowDescriptor 静态字段
    /// 2. 在菜单 XAML 中添加按钮
    /// 3. 在按钮 Click 事件中调用此方法并传入 descriptor
    /// </summary>
    private void CreateOrActivateChildWindow(ChildWindowDescriptor descriptor)
    {
        var windowKey = descriptor.Key;

        // 检查该 Key 对应的窗口是否已经打开。
        if (_openChildWindows.TryGetValue(windowKey, out var existingWindow))
        {
            // 窗口已存在：尝试将其带到前台。
            // 如果窗口被最小化了则先恢复，然后激活。
            if (existingWindow.WindowState == WindowState.Minimized)
            {
                existingWindow.WindowState = WindowState.Normal;
            }
            existingWindow.Activate();
            return;
        }

        // 窗口不存在：创建新的子窗口实例。
        var baseUrl = GetBaseUrl();

        var childWindow = new ChildWindow(
            _appConfig,
            _environmentService,
            descriptor,
            baseUrl);

        // 监听子窗口关闭事件，从字典中移除对应条目。
        childWindow.Closed += ChildWindow_Closed;

        // 将子窗口加入字典，防止重复创建。
        _openChildWindows[windowKey] = childWindow;

        // 以非模态方式显示子窗口（主窗口仍可交互）。
        childWindow.Show();
    }

    /// <summary>
    /// 子窗口关闭后，从跟踪字典中移除对应条目。
    /// </summary>
    private void ChildWindow_Closed(object? sender, EventArgs e)
    {
        if (sender is ChildWindow childWindow)
        {
            var windowKey = childWindow.WindowKey;
            _openChildWindows.Remove(windowKey);
        }
    }

    /// <summary>
    /// 关闭所有已打开的子窗口。
    /// 在主窗口退出时调用，让每个子窗口有机会保存各自的尺寸。
    /// </summary>
    private void CloseAllChildWindows()
    {
        // 复制一份窗口列表再遍历，避免在遍历过程中修改集合。
        var childWindows = new List<ChildWindow>(_openChildWindows.Values);

        foreach (var childWindow in childWindows)
        {
            // 跳过已关闭的窗口。
            if (!childWindow.IsLoaded)
            {
                continue;
            }

            childWindow.Close();
        }

        _openChildWindows.Clear();
    }

    // ==================== WebView2 初始化 ====================

    /// <summary>
    /// 使用共享的 CoreWebView2Environment 初始化 WebView2 控件。
    /// 与子窗口共用同一个环境和 UserDataFolder，减少进程和内存开销。
    /// </summary>
    private async void InitWebView2()
    {
        // 获取共享的 CoreWebView2Environment（首次调用时创建）。
        var sharedEnvironment = await _environmentService.GetEnvironmentAsync();

        // 将 WebView2 控件绑定到共享环境。
        await webView2.EnsureCoreWebView2Async(sharedEnvironment);

        // 使用配置中的 WebView2Url 进行导航。
        // 开发时可修改 appsettings.json 中的 WebView2Url 指向 Vite 开发服务器。
        var targetUrl = _appConfig.WebView2Url;
        webView2.CoreWebView2.Navigate(targetUrl);
    }
}
