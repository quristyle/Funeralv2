using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NotificationServer.Models;

namespace NotificationServer.Services;

public class SmtpEmailSender : IEmailSender
{
    private readonly EmailSettings _settings;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailSettings> options, ILogger<SmtpEmailSender> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string body, bool html = false)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromDisplay, _settings.User));
        // 받는 사람이 여럿이면 쉼표로 온다 (역할 수신 등)
        foreach (var addr in to.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            message.To.Add(MailboxAddress.Parse(addr));
        }
        message.Subject = subject;
        message.Body = html
            ? new BodyBuilder { HtmlBody = body }.ToMessageBody()
            : new TextPart("plain") { Text = body };

        using var client = new SmtpClient();

        if (_settings.IgnoreCertificateErrors)
        {
            client.ServerCertificateValidationCallback = (s, c, h, e) => true;
        }

        // 587 은 STARTTLS 포트다 — UseSsl(암시적 SSL)로 붙으면 핸드셰이크가 깨진다.
        // 465 만 접속부터 SSL(SslOnConnect)이고, 그 외에 UseSsl 이면 STARTTLS 로 올린다.
        var secure = _settings.Port == 465
            ? MailKit.Security.SecureSocketOptions.SslOnConnect
            : _settings.UseSsl
                ? MailKit.Security.SecureSocketOptions.StartTls
                : MailKit.Security.SecureSocketOptions.StartTlsWhenAvailable;

        try
        {
            await client.ConnectAsync(_settings.Host, _settings.Port, secure);
            await client.AuthenticateAsync(_settings.User, _settings.Password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("[{Time}] [Info] [Email] Sent to {To}", DateTime.UtcNow, to);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Time}] [Error] [Email] Failed to send to {To}", DateTime.UtcNow, to);
            throw;
        }
    }
}
