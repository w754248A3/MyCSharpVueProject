using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace MyNodeView;

public class Program
{
    // ==================== 单实例检测 ====================

    /// <summary>
    /// 系统命名信号量的名称，用于确保整个 Windows 会话中只有一个程序实例在运行。
    /// </summary>
    private const string SingleInstanceSemaphoreName = "MyNodeView_SingleInstance";

    // ==================== 程序入口 ====================

    /// <summary>
    /// WPF 需要单线程单元 (STA) 才能运行。
    /// </summary>
    [STAThread]
    public static void Main(string[] args)
    {
        // ---------- 步骤 1：单实例检测 ----------
        // 使用系统命名信号量检测是否已有实例在运行。
        // 信号量初始计数和最大计数都为 1，确保同一时间只有一个实例能获取。
        using var singleInstanceSemaphore = new Semaphore(
            initialCount: 1,
            maximumCount: 1,
            name: SingleInstanceSemaphoreName,
            createdNew: out bool isFirstInstance
        );

        // 如果信号量已存在，说明另一个实例正在运行，直接退出。
        if (!isFirstInstance)
        {
            var message = "应用程序已在运行中，不能同时启动多个实例。";
            MessageBox.Show(message, "MyNodeView", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // ---------- 步骤 2：加载应用配置 ----------
        var appConfig = AppConfig.Load();

        // ---------- 步骤 3：创建 ASP.NET Core 应用 ----------
        // 让桌面程序同时提供标准 HTTP Web API 和静态页面。
        var builder = WebApplication.CreateBuilder(args);

        // 使用配置文件中的地址启动 Web 服务器。
        builder.WebHost.UseUrls(appConfig.AppUrl);

        // 注册 MVC Controller，前端通过 /api/* 调用这些标准 HTTP API。
        builder.Services.AddControllers();

        // 注册 WPF 窗口与数据服务，Controller 与窗口共享同一个 DI 容器。
        builder.Services.AddSingleton<App>();
        builder.Services.AddSingleton<MainWindow>();
        builder.Services.AddSingleton<MyNodeDataStore>();
        builder.Services.AddSingleton<NodeImageStore>();
        builder.Services.AddSingleton<NodeDataApiService>();

        // 注册 WebView2 环境共享服务，主窗口与子窗口共享浏览器进程。
        builder.Services.AddSingleton<WebView2EnvironmentService>();

        // 将配置实例注册到 DI 容器，供 MainWindow 等组件通过构造函数注入。
        builder.Services.AddSingleton(appConfig);

        // ---------- 步骤 4：构建 Web 应用并映射路由 ----------
        var app = builder.Build();
        app.MapControllers();

        // 使用配置中的静态文件路径来提供 Vue 前端页面。
        MapVueStaticFiles(app, appConfig.StaticFilesPath);

        // 映射 /SingleFilePage/ 路径的静态文件（或错误回退）。
        MapSingleFilePage(app, appConfig.SingleFilePagePath);

        // ---------- 步骤 5：启动 Web 服务与 WPF 窗口 ----------
        // 在后台启动 Web 服务，主线程继续运行 WPF 消息循环。
        app.StartAsync().GetAwaiter().GetResult();

        var wpfApp = app.Services.GetRequiredService<App>();
        var mainWindow = app.Services.GetRequiredService<MainWindow>();

        // 根据配置设置窗口初始大小。
        mainWindow.Width = appConfig.WindowWidth;
        mainWindow.Height = appConfig.WindowHeight;

        wpfApp.Run(mainWindow);

        // ---------- 步骤 6：窗口关闭后保存配置并停止服务 ----------
        // 在退出前将窗口大小等运行时状态写回配置文件。
        appConfig.Save();

        // 优雅停止本地 HTTP 服务。
        app.StopAsync().GetAwaiter().GetResult();
    }

    // ==================== 静态文件映射 ====================

    /// <summary>
    /// 将 Vue 前端编译产物映射为 ASP.NET Core 的静态文件。
    /// 如果指定了 staticFilesPath 则直接使用该路径，
    /// 否则自动在源码目录和发布目录中查找 dist 文件夹。
    /// </summary>
    private static void MapVueStaticFiles(WebApplication app, string configuredStaticFilesPath)
    {
        // 确定最终使用的静态文件根目录路径。
        var webRootPath = configuredStaticFilesPath;

        // 静态文件目录存在时才注册中间件。
        if (Directory.Exists(webRootPath))
        {
            // 让 ASP.NET Core 直接提供 Vite 编译后的 JS、CSS、图片等静态资源。
            var fileProvider = new PhysicalFileProvider(webRootPath);

            var defaultFilesOptions = new DefaultFilesOptions
            {
                FileProvider = fileProvider
            };

            var staticFileOptions = new StaticFileOptions
            {
                FileProvider = fileProvider
            };

            app.UseDefaultFiles(defaultFilesOptions);
            app.UseStaticFiles(staticFileOptions);
        }

        // Vue Router 的前端路由需要回退到 index.html；dist 不存在时返回清晰提示。
        app.MapFallback(async context =>
        {
            await WriteVueFallbackAsync(context, webRootPath);
        });
    }

    // ==================== /SingleFilePage/ 静态文件映射 ====================

    /// <summary>
    /// 将 /SingleFilePage/ 路径映射到配置中指定的本地目录。
    /// 如果 SingleFilePagePath 为空或目录不存在，
    /// 则对该路径的所有请求返回配置未设置的错误信息。
    /// </summary>
    private static void MapSingleFilePage(WebApplication app, string singleFilePagePath)
    {
        var isPathConfigured = !string.IsNullOrWhiteSpace(singleFilePagePath);
        var isDirectoryExists = isPathConfigured && Directory.Exists(singleFilePagePath);

        if (isDirectoryExists)
        {
            // 路径已配置且目录存在：注册静态文件中间件。
            var fileProvider = new PhysicalFileProvider(singleFilePagePath);

            var staticFileOptions = new StaticFileOptions
            {
                FileProvider = fileProvider,
                RequestPath = "/SingleFilePage"
            };

            // 使用 UseStaticFiles 注册，让 /SingleFilePage/* 映射到本地目录。
            app.UseStaticFiles(staticFileOptions);
        }
        else
        {
            // 路径未配置或目录不存在：任何 /SingleFilePage/* 请求返回错误。
            app.Map("/SingleFilePage", singleFilePageBranch =>
            {
                singleFilePageBranch.Run(SingleFilePageNotConfiguredHandler);
            });
        }
    }

    /// <summary>
    /// /SingleFilePage/ 未配置时的回退处理。
    /// 对所有 /SingleFilePage/* 请求返回 503 状态码和说明文字。
    /// </summary>
    private static async Task SingleFilePageNotConfiguredHandler(HttpContext context)
    {
        // 返回 HTTP 503，提示用户在配置文件中设置 SingleFilePagePath。
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.ContentType = "text/plain; charset=utf-8";

        var errorMessage = "SingleFilePage 路径未配置。\n"
            + "请在 appsettings.json 中设置 singleFilePagePath 为本地目录路径。";

        await context.Response.WriteAsync(errorMessage);
    }

    // ==================== Vue 页面回退 ====================

    /// <summary>
    /// 处理前端路由回退逻辑。
    /// API 路径返回 404 JSON，普通页面路径返回 index.html。
    /// </summary>
    private static async Task WriteVueFallbackAsync(HttpContext context, string webRootPath)
    {
        // API 地址没有命中 Controller 时保留 404，避免被 index.html 掩盖接口错误。
        var requestPath = context.Request.Path.Value;
        var isApiPath = requestPath is not null
            && requestPath.StartsWith("/api/", StringComparison.OrdinalIgnoreCase);

        if (isApiPath)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new { error = "API not found" });
            return;
        }

        // 普通页面地址统一返回 Vue 的入口 HTML。
        var indexPath = Path.Combine(webRootPath, "index.html");
        if (File.Exists(indexPath))
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.SendFileAsync(indexPath);
            return;
        }

        // 没有编译前端时给出操作提示，避免用户看到空白页面。
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.ContentType = "text/plain; charset=utf-8";
        await context.Response.WriteAsync(
            "Vue dist not found. Please run npm run build in Vue/WebView2Page first.");
    }
}
