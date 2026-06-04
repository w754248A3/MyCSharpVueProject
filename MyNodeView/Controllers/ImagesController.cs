using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace MyNodeView.Controllers;

[ApiController]
[Route("api/images")]
public class ImagesController : ControllerBase
{
    private const long MaxImageBytes = 20 * 1024 * 1024;
    private readonly NodeImageStore _imageStore;
    private static readonly JsonSerializerOptions ApiJsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ImagesController(NodeImageStore imageStore)
    {
        _imageStore = imageStore;
    }

    [HttpGet("meta")]
    public async Task<IActionResult> GetMeta([FromQuery] int? nodeId)
    {
        // nodeId 是图片表与节点表关联的入口参数。
        if (nodeId is null)
        {
            return BadRequest(new { error = "nodeId is required" });
        }

        // 查询节点图片摘要，例如图片数量和最新图片 Id。
        var summary = await _imageStore.GetSummaryAsync(nodeId.Value);
        return new JsonResult(summary, ApiJsonSerializerOptions);
    }

    [HttpGet("list")]
    public async Task<IActionResult> GetList([FromQuery] int? nodeId)
    {
        // nodeId 为空时无法定位图片列表。
        if (nodeId is null)
        {
            return BadRequest(new { error = "nodeId is required" });
        }

        // 查询当前节点关联的图片元数据列表。
        var list = await _imageStore.ListAsync(nodeId.Value);
        return new JsonResult(list, ApiJsonSerializerOptions);
    }

    [HttpGet("content")]
    public async Task<IActionResult> GetContent([FromQuery] long? id)
    {
        // 图片内容接口使用图片主键读取二进制数据。
        if (id is null)
        {
            return BadRequest(new { error = "id is required" });
        }

        // 图片不存在时返回标准 404 JSON。
        var image = await _imageStore.GetImageAsync(id.Value);
        if (image is null)
        {
            return NotFound(new { error = "Not found" });
        }

        // File 结果会保留 MIME 类型，让浏览器可以直接预览图片。
        return File(image.Data, image.MimeType);
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload([FromQuery] int? nodeId)
    {
        // 上传接口必须知道图片要挂在哪个节点下。
        if (nodeId is null)
        {
            return BadRequest(new { error = "nodeId is required" });
        }

        // 只接受 image/*，与原 WebView2 拦截逻辑保持一致。
        var mimeType = Request.ContentType;
        if (string.IsNullOrWhiteSpace(mimeType) || !mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "Content-Type must be image/*" });
        }

        // 把请求体复制到内存后进行大小检查，再写入数据库。
        byte[] imageBytes;
        await using (var memoryStream = new System.IO.MemoryStream())
        {
            await Request.Body.CopyToAsync(memoryStream);
            imageBytes = memoryStream.ToArray();
        }

        if (imageBytes.Length == 0)
        {
            return BadRequest(new { error = "Request body is empty" });
        }

        if (imageBytes.LongLength > MaxImageBytes)
        {
            return BadRequest(new { error = "Image is too large (max 20MB)" });
        }

        // 文件名来自前端自定义请求头，存库前先做 URL 解码。
        var rawFileName = Request.Headers["X-File-Name"].FirstOrDefault();
        var fileName = string.IsNullOrWhiteSpace(rawFileName) ? string.Empty : Uri.UnescapeDataString(rawFileName);
        var imageId = await _imageStore.InsertAsync(nodeId.Value, fileName, mimeType, imageBytes);
        return Created($"/api/images/content?id={imageId}", new { id = imageId });
    }

    [HttpDelete("delete")]
    public async Task<IActionResult> Delete([FromQuery] long? id)
    {
        // 删除接口使用图片主键定位记录。
        if (id is null)
        {
            return BadRequest(new { error = "id is required" });
        }

        // 删除失败代表图片不存在。
        var isDeleted = await _imageStore.DeleteAsync(id.Value);
        if (!isDeleted)
        {
            return NotFound(new { error = "Not found" });
        }

        return Ok(new { ok = true });
    }
}
