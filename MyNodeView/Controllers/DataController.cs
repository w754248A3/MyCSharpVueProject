using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace MyNodeView.Controllers;

[ApiController]
[Route("api/data")]
public class DataController : ControllerBase
{
    private readonly NodeDataApiService _nodeDataApiService;

    public DataController(NodeDataApiService nodeDataApiService)
    {
        _nodeDataApiService = nodeDataApiService;
    }

    [HttpPost]
    public async Task<IActionResult> Post()
    {
        // 验证 Content-Type，避免把非 JSON 内容交给数据消息处理器。
        var contentType = Request.ContentType;
        var isJsonContent = !string.IsNullOrWhiteSpace(contentType)
            && contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase);
        if (!isJsonContent)
        {
            return BadRequest(new { error = "Content-Type is not json" });
        }

        // 读取原始 JSON 字符串，保持与原 RunDataReadWriteSQL 方法相同的输入格式。
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var requestJson = await reader.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(requestJson))
        {
            return BadRequest(new { error = "Request body is empty" });
        }

        // 调用数据服务执行业务逻辑，并把服务生成的 JSON 原样返回给前端。
        var responseJson = await _nodeDataApiService.RunDataReadWriteSQL(requestJson);
        return Content(responseJson, "application/json; charset=utf-8");
    }
}
