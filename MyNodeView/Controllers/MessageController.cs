using Microsoft.AspNetCore.Mvc;

namespace MyNodeView.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MessageController : ControllerBase
{
    private readonly MainWindow _mainWindow;

    // 通过构造函数注入 MainWindow (在 Program.cs 中已经注册)
    public MessageController(MainWindow mainWindow)
    {
        _mainWindow = mainWindow;
    }

    // GET: http://localhost:5000/api/message?text=HelloWPF
    [HttpGet]
    public IActionResult SendMessage([FromQuery] string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return BadRequest("文本不能为空");
        }

        // 调用 MainWindow 的方法更新 UI
        _mainWindow.UpdateMessage($"收到 API 消息: {text}");

        return Ok(new { success = true, receivedText = text });
    }
}