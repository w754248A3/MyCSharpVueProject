using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MyNodeView;

/// <summary>
/// 应用程序配置模型。
/// 配置文件以 JSON 格式存放在可执行文件所在的目录中，
/// 首次运行时会自动使用默认值创建。
/// </summary>
public class AppConfig
{
    // ==================== 默认常量 ====================

    private const string DefaultAppUrl = "http://localhost:34114";
    private const string DefaultWebView2Url = "http://localhost:34114";
    private const double DefaultWindowWidth = 800;
    private const double DefaultWindowHeight = 450;

    // ==================== 配置属性 ====================

    /// <summary>
    /// ASP.NET Core Web 服务器监听的地址。
    /// </summary>
    public string AppUrl { get; set; } = DefaultAppUrl;

    /// <summary>
    /// WebView2 控件导航的目标地址。
    /// 开发时可改为 Vite 开发服务器地址（如 http://localhost:5173/）。
    /// </summary>
    public string WebView2Url { get; set; } = DefaultWebView2Url;

    /// <summary>
    /// Vue 前端编译产物（dist 目录）的本地路径。
    /// 为空字符串时，程序会自动查找 dist 目录的位置。
    /// </summary>
    public string StaticFilesPath { get; set; } = string.Empty;

    /// <summary>
    /// /SingleFilePage/ 路径下静态文件的本地目录。
    /// 为空字符串时，访问该路径会返回配置未设置的错误信息。
    /// </summary>
    public string SingleFilePagePath { get; set; } = string.Empty;

    /// <summary>
    /// 主窗口的宽度（像素）。
    /// </summary>
    public double WindowWidth { get; set; } = DefaultWindowWidth;

    /// <summary>
    /// 主窗口的高度（像素）。
    /// </summary>
    public double WindowHeight { get; set; } = DefaultWindowHeight;

    /// <summary>
    /// 各个子窗口的尺寸配置，以窗口 Key 为索引。
    /// Key 由 ChildWindowDescriptor 定义，每个 Key 对应一种子窗口类型。
    /// 程序退出时自动保存，下次启动时恢复对应窗口的大小。
    /// </summary>
    public Dictionary<string, WindowSize> ChildWindowSizes { get; set; } = new();

    // ==================== JSON 序列化选项 ====================

    /// <summary>
    /// 用于读写配置文件的 JSON 序列化选项（缩进格式，便于人工编辑）。
    /// </summary>
    private static readonly JsonSerializerOptions ConfigJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // ==================== 加载与保存 ====================

    /// <summary>
    /// 从可执行文件同目录下的 appsettings.json 加载配置。
    /// 如果文件不存在，则返回带有默认值的 AppConfig 实例。
    /// </summary>
    public static AppConfig Load()
    {
        var configFilePath = GetConfigFilePath();

        // 配置文件不存在时，返回全新的默认配置实例。
        if (!File.Exists(configFilePath))
        {
            return new AppConfig();
        }

        try
        {
            // 读取并反序列化 JSON 配置文件。
            var jsonText = File.ReadAllText(configFilePath);
            var loadedConfig = JsonSerializer.Deserialize<AppConfig>(jsonText, ConfigJsonOptions);

            // 反序列化结果为 null 时（例如 JSON 文件内容为空），回退到默认配置。
            if (loadedConfig is null)
            {
                return new AppConfig();
            }

            return loadedConfig;
        }
        catch (JsonException)
        {
            // JSON 格式损坏时，为了避免程序无法启动，回退到默认配置。
            return new AppConfig();
        }
    }

    /// <summary>
    /// 将当前配置保存到可执行文件同目录下的 appsettings.json 文件。
    /// </summary>
    public void Save()
    {
        var configFilePath = GetConfigFilePath();

        try
        {
            var jsonText = JsonSerializer.Serialize(this, ConfigJsonOptions);
            File.WriteAllText(configFilePath, jsonText);
        }
        catch (Exception)
        {
            // 保存配置失败不应影响程序正常退出流程，
            // 因此这里只做静默忽略，外部调用方无需处理异常。
        }
    }

    // ==================== 辅助方法 ====================

    /// <summary>
    /// 获取配置文件在磁盘上的完整路径。
    /// 配置文件固定名为 appsettings.json，位于可执行文件所在目录。
    /// </summary>
    private static string GetConfigFilePath()
    {
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var configFilePath = Path.Combine(baseDirectory, "appsettings.json");
        return configFilePath;
    }
}

// ==================== 配置子类型 ====================

/// <summary>
/// 窗口尺寸配置，用于存储主窗口或子窗口的宽高。
/// </summary>
public class WindowSize
{
    /// <summary>
    /// 窗口宽度（像素），默认 600。
    /// </summary>
    public double Width { get; set; } = 600;

    /// <summary>
    /// 窗口高度（像素），默认 400。
    /// </summary>
    public double Height { get; set; } = 400;
}
