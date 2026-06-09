namespace MyNodeView;

/// <summary>
/// 描述一种子窗口类型的配置参数。
/// 每个实例代表一种可用 Key 标识的窗口模板，
/// 在主窗口菜单中通过按钮触发创建。
///
/// 要新增一种子窗口类型，只需：
/// 1. 在 MainWindow 中定义一个 static readonly 的 ChildWindowDescriptor 字段
/// 2. 在菜单中添加一个对应的按钮
/// 3. 在按钮事件中调用 CreateOrActivateChildWindow(descriptor)
/// </summary>
public class ChildWindowDescriptor
{
    /// <summary>
    /// 窗口的唯一标识 Key。
    /// 相同 Key 的窗口同时只能存在一个，用于去重和配置存储。
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// 窗口标题栏显示的文字。
    /// </summary>
    public string Title { get; init; } = "子窗口";

    /// <summary>
    /// 窗口内 WebView2 访问的 URL 路径部分。
    /// 最终 URL = 主窗口 URL 的 Scheme + Host + 此路径。
    /// 例如 "/SingleFilePage/" 会拼接到 http://localhost:34114 后面。
    /// </summary>
    public string UrlPath { get; init; } = "/";

    /// <summary>
    /// 窗口是否置顶显示。
    /// </summary>
    public bool Topmost { get; init; } = false;

    /// <summary>
    /// 窗口默认宽度（像素），仅在首次创建时使用。
    /// </summary>
    public double DefaultWidth { get; init; } = 600;

    /// <summary>
    /// 窗口默认高度（像素），仅在首次创建时使用。
    /// </summary>
    public double DefaultHeight { get; init; } = 400;
}
