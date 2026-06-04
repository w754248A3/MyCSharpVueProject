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
    
    private string _webApiMessage = "";

    public MainWindow()
    {
        InitializeComponent();
        Loaded += Window_Loaded;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // 窗口加载完成后再初始化 WebView2，避免控件尚未创建就访问 CoreWebView2。
        InitWebView2();
        Loaded -= Window_Loaded;
    }

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

    public void UpdateMessage(string message)
    {
        // Web API 运行在后台线程，WPF UI 只能在主线程更新。
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

    private async void InitWebView2()
    {
        // 为 WebView2 指定独立用户数据目录，避免与系统浏览器配置互相影响。
        var userDataPath = GetUserDataPath();
        var options = new CoreWebView2EnvironmentOptions();
        var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataPath, options: options);

        // WebView2 不再拦截自定义域名，也不再代理 Vite 静态资源；它和普通浏览器一样访问本地 HTTP 服务。
        await webView2.EnsureCoreWebView2Async(environment);

#if DEBUG
     webView2.CoreWebView2.Navigate("http://localhost:5173/");
#else
     webView2.CoreWebView2.Navigate(Program.AppUrl);
#endif
       
    }

    private static string GetUserDataPath()
    {
        // 用户数据目录放在程序目录下，方便定位和清理 WebView2 缓存。
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var userDataPath = Path.Combine(baseDirectory, "MyNodeView.exe.WebView2");
        return userDataPath;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // 关闭窗口前做一次确认，避免误操作退出整个桌面应用。
        var result = MessageBox.Show("确定要退出吗？", "确认退出", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.No)
        {
            e.Cancel = true;
        }

        base.OnClosing(e);
    }
}
