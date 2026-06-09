using System.ComponentModel;
using System.Diagnostics;
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
    // ==================== 依赖注入的字段 ====================

    private readonly AppConfig _appConfig;
    private string _webApiMessage = "";

    // ==================== 构造函数 ====================

    /// <summary>
    /// 通过依赖注入接收 AppConfig，使窗口能读取和保存配置。
    /// </summary>
    public MainWindow(AppConfig appConfig)
    {
        _appConfig = appConfig;

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
    /// 窗口关闭前保存当前窗口大小到配置对象。
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

        // 在用户确认退出后，将当前窗口尺寸记录到配置对象中。
        // 程序正常退出后会由 Program.Main 统一将配置写回磁盘。
        _appConfig.WindowWidth = Width;
        _appConfig.WindowHeight = Height;
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

    // ==================== WebView2 初始化 ====================

    /// <summary>
    /// 初始化 WebView2 控件。
    /// 使用配置文件中指定的 URL 作为导航目标，
    /// 不再通过 DEBUG 编译分支区分开发/生产环境。
    /// </summary>
    private async void InitWebView2()
    {
        // 为 WebView2 指定独立用户数据目录，避免与系统浏览器配置互相影响。
        var userDataPath = GetUserDataPath();

        var options = new CoreWebView2EnvironmentOptions();
        var environment = await CoreWebView2Environment.CreateAsync(
            userDataFolder: userDataPath,
            options: options);

        // WebView2 不再拦截自定义域名，也不再代理 Vite 静态资源；
        // 它和普通浏览器一样访问配置文件中的目标 URL。
        await webView2.EnsureCoreWebView2Async(environment);

        // 使用配置中的 WebView2Url 进行导航。
        // 开发时可修改 appsettings.json 中的 WebView2Url 指向 Vite 开发服务器。
        var targetUrl = _appConfig.WebView2Url;
        webView2.CoreWebView2.Navigate(targetUrl);
    }

    // ==================== 辅助方法 ====================

    /// <summary>
    /// 获取 WebView2 用户数据目录。
    /// 用户数据目录放在程序目录下，方便定位和清理 WebView2 缓存。
    /// </summary>
    private static string GetUserDataPath()
    {
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var userDataPath = Path.Combine(baseDirectory, "MyNodeView.exe.WebView2");
        return userDataPath;
    }
}
