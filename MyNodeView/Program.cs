using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Windows;

namespace MyNodeView;

public class Program
{
    // WPF 需要单线程单元 (STA) 才能运行
    [STAThread]
    public static void Main(string[] args)
    {
        // 1. 创建 Web 应用程序构建器
        var builder = WebApplication.CreateBuilder(args);

        // 配置 Web API 监听的端口 (例如监听 5000 端口)
        builder.WebHost.UseUrls("http://localhost:5000");

        // 添加控制器支持
        builder.Services.AddControllers();

        // 2. 将 WPF 的 App 和 MainWindow 注册到依赖注入容器中
        // 这样 Web API 的 Controller 就能获取到 MainWindow 的实例，从而更新 UI
        builder.Services.AddSingleton<App>();
        builder.Services.AddSingleton<MainWindow>();

        // 3. 构建应用
        var app = builder.Build();

        // 映射 API 路由
        app.MapControllers();

        // 4. 启动 Web API (使用 StartAsync 是为了不阻塞主线程)
        _ = app.StartAsync();

        // 5. 启动 WPF 界面
        // 从 DI 容器中解析出 App 和 MainWindow
        var wpfApp = app.Services.GetRequiredService<App>();
        var mainWindow = app.Services.GetRequiredService<MainWindow>();
        
        // 运行 WPF 消息循环 (这行代码会阻塞，直到关闭 WPF 窗口)
        wpfApp.Run(mainWindow);

        // 6. WPF 窗口关闭后，优雅地停止 Web API
        app.StopAsync().Wait();
    }
}