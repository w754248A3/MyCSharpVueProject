using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace MyNodeView;

public class Program
{
    public const string AppUrl = "http://localhost:34114";

    // WPF 需要单线程单元 (STA) 才能运行。
    [STAThread]
    public static void Main(string[] args)
    {
        // 创建 ASP.NET Core 应用，让桌面程序同时提供标准 HTTP Web API 和静态页面。
        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.UseUrls(AppUrl);

        // 注册 MVC Controller，前端通过 /api/* 调用这些标准 HTTP API。
        builder.Services.AddControllers();

        // 注册 WPF 窗口与数据服务，Controller 与窗口共享同一个 DI 容器。
        builder.Services.AddSingleton<App>();
        builder.Services.AddSingleton<MainWindow>();
        builder.Services.AddSingleton<MyNodeDataStore>();
        builder.Services.AddSingleton<NodeImageStore>();
        builder.Services.AddSingleton<NodeDataApiService>();

        // 构建 Web 应用，并先映射 API，再映射静态页面回退。
        var app = builder.Build();
        app.MapControllers();
        MapVueStaticFiles(app);

        // 在后台启动 Web 服务，主线程继续运行 WPF 消息循环。
        app.StartAsync().GetAwaiter().GetResult();
        var wpfApp = app.Services.GetRequiredService<App>();
        var mainWindow = app.Services.GetRequiredService<MainWindow>();
        wpfApp.Run(mainWindow);

        // WPF 窗口关闭后，优雅停止本地 HTTP 服务。
        app.StopAsync().GetAwaiter().GetResult();
    }

    private static void MapVueStaticFiles(WebApplication app)
    {
        // 优先使用源码目录中的 Vue 编译产物，便于本地开发直接运行 C# 项目。
        var contentRoot = app.Environment.ContentRootPath;
        var sourceDistPath = Path.GetFullPath(Path.Combine(contentRoot, "..", "Vue", "WebView2Page", "dist"));

        // 发布后也可以把 dist 放到程序目录下，程序会自动使用这个备选位置。
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var publishedDistPath = Path.Combine(baseDirectory, "dist");
        var webRootPath = Directory.Exists(sourceDistPath) ? sourceDistPath : publishedDistPath;
        webRootPath = @"C:\Users\PC\code\MyCSharpVueProject\Vue\WebView2Page\dist";
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

    private static async Task WriteVueFallbackAsync(HttpContext context, string webRootPath)
    {
        // API 地址没有命中 Controller 时保留 404，避免被 index.html 掩盖接口错误。
        var requestPath = context.Request.Path.Value;
        var isApiPath = requestPath is not null && requestPath.StartsWith("/api/", StringComparison.OrdinalIgnoreCase);
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
        await context.Response.WriteAsync("Vue dist not found. Please run npm run build in Vue/WebView2Page first.");
    }
}
