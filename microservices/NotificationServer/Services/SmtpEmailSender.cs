using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NotificationServer.DTOs;
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

    public async Task SendAsync(
        string to, string subject, string body, bool html = false,
        IReadOnlyList<EmailAttachmentDto>? attachments = null,
        string? textBody = null,
        string? cc = null)
    {
        // 설정이 없으면 붙어 보지도 않는다(빈 호스트는 MailKit 이 「인수가 잘못됐다」만 던져 원인을 가리므로 키 이름을 적어 먼저 던진다).
        if (!_settings.IsConfigured)
        {
            var missing = _settings.MissingKeys();

            _logger.LogError(
                "SMTP 설정이 없어 메일을 보내지 못했습니다 ({Missing}). "
                + "to={To} — appsettings.Local.json 에 넣어야 합니다 (docs/email-smtp.md).",
                missing, to);

            throw new InvalidOperationException(
                $"SMTP 설정이 없습니다 ({missing}). appsettings.Local.json 을 확인하십시오.");
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromDisplay, _settings.User));
        // 받는 사람이 여럿이면 쉼표로 온다 (역할 수신 등)
        foreach (var addr in to.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            message.To.Add(MailboxAddress.Parse(addr));
        }

        // 참조는 받는 사람과 같은 규약이다. 주소 꼴은 여기서 다시 보지 않는다(참조를 조용히 떨어뜨리면 보낸 사람은 갔다고 믿는다).
        foreach (var addr in (cc ?? string.Empty)
                     .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            message.Cc.Add(MailboxAddress.Parse(addr));
        }

        message.Subject = subject;

        // 첨부가 있으면 언제나 BodyBuilder 로 간다(TextPart 는 첨부를 못 달아 파일을 골라도 본문만 나간다).
        if (attachments is { Count: > 0 })
        {
            var builder = new BodyBuilder();

            if (html)
            {
                builder.HtmlBody = body;

                // 평문 갈래도 같이 싣는다 — 주는 쪽이 있을 때만이다(IEmailSender.SendAsync 의 textBody).
                if (!string.IsNullOrWhiteSpace(textBody)) builder.TextBody = textBody;
            }
            else
            {
                builder.TextBody = body;
            }

            foreach (var file in attachments)
            {
                var bytes = Convert.FromBase64String(file.Content);

                // 형식이 비었거나 알아볼 수 없으면 octet-stream 으로 붙인다(던지면 메일 전체가 안 나간다).
                ContentType type;
                try
                {
                    type = ContentType.Parse(
                        string.IsNullOrWhiteSpace(file.ContentType)
                            ? "application/octet-stream"
                            : file.ContentType);
                }
                catch (ParseException)
                {
                    type = ContentType.Parse("application/octet-stream");
                }

                builder.Attachments.Add(file.FileName, bytes, type);
            }

            message.Body = builder.ToMessageBody();
        }
        else
        {
            message.Body = html
                ? new BodyBuilder
                {
                    HtmlBody = body,

                    // 비워 두면 MailKit 이 평문 갈래를 만들지 않는다. 있는데 안 싣는 것이 아니다.
                    TextBody = string.IsNullOrWhiteSpace(textBody) ? null : textBody,
                }.ToMessageBody()
                : new TextPart("plain") { Text = body };
        }

        using var client = new SmtpClient();

        // 부르는 쪽(AuthServer 20초)보다 먼저 포기해야 실패 까닭이 우리 로그에 남는다. MailKit 기본 2분은 저쪽이 먼저 끊으므로 줄인다.
        if (_settings.TimeoutMs > 0)
        {
            client.Timeout = _settings.TimeoutMs;
        }

        if (_settings.IgnoreCertificateErrors)
        {
            client.ServerCertificateValidationCallback = (s, c, h, e) => true;
        }

        // 587 은 STARTTLS 포트라 UseSsl(암시적 SSL)로 붙으면 핸드셰이크가 깨진다(465 만 접속부터 SSL).
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

            _logger.LogInformation("[{Time}] [Info] [Email] Sent to {To} cc={Cc} ({Files} attachment(s))",
                DateTime.UtcNow, to, cc, attachments?.Count ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Time}] [Error] [Email] Failed to send to {To}", DateTime.UtcNow, to);
            throw;
        }
    }
}
