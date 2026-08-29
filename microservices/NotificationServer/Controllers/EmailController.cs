using System.Threading.Tasks;
using NotificationServer.Models;
using NotificationServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace NotificationServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmailController : ControllerBase
{
    private readonly IEmailSender _emailSender;
    private readonly ILogger<EmailController> _logger;

    public EmailController(IEmailSender emailSender, ILogger<EmailController> logger)
    {
        _emailSender = emailSender;
        _logger = logger;
    }

    public record EmailRequest(string Email, string Name, string Message);

    [HttpPost("send")]
    public async Task<IActionResult> Send([FromBody] EmailRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.Message))
        {
            _logger.LogWarning("[{{Time}}] [Warn] [Email] Invalid request payload");
            return BadRequest(new { success = false, error = "Invalid payload" });
        }

        var subject = $"[Jsini 문의] {request.Name} 님 문의";
        var body = $"보낸 사람: {request.Name} <{request.Email}>\n\n내용:\n{request.Message}";

        try
        {
            await _emailSender.SendAsync("user15@example.invalid", subject, body);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{{Time}}] [Error] [Email] Sending failed");
            return StatusCode(500, new { success = false, error = "Email sending failed" });
        }
    }
}
