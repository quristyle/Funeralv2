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
        string? textBody = null)
    {
        // **설정이 없으면 붙어 보지도 않는다.**
        //
        // 빈 호스트로 ConnectAsync 를 부르면 MailKit 이 던지는 것은 「인수가
        // 잘못됐다」 뿐이라, 로그만 보고서는 메일 서버가 죽은 것인지 우리가
        // 설정을 안 넣은 것인지 가릴 수 없다. 여기서 먼저 멈추고 **비어 있는
        // 키 이름을 적어** 던진다 — 이 서비스의 메일이 전부 이 한 곳을 거치므로
        // (비밀번호 찾기·문의 접수·AI 작업 알림) 이 한 줄이 유일한 단서가 된다.
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
        message.Subject = subject;

        // **붙일 것이 있으면 언제나 BodyBuilder 로 간다.** 평문이어도 그렇다 —
        // `TextPart` 는 본문 하나짜리라 첨부를 달 자리가 없고, 그대로 두면
        // 파일을 골라도 **아무 말 없이 본문만 나간다.**
        if (attachments is { Count: > 0 })
        {
            var builder = new BodyBuilder();

            if (html)
            {
                builder.HtmlBody = body;

                // **평문 갈래도 같이 싣는다.** 주는 쪽이 있을 때만이다 —
                // 자세한 까닭은 `IEmailSender.SendAsync` 의 `textBody` 주석에 있다.
                if (!string.IsNullOrWhiteSpace(textBody)) builder.TextBody = textBody;
            }
            else
            {
                builder.TextBody = body;
            }

            foreach (var file in attachments)
            {
                var bytes = Convert.FromBase64String(file.Content);

                // 형식이 비었거나 알아볼 수 없으면 octet-stream 으로 붙인다.
                // 파싱에서 던지면 **메일 전체가 안 나가므로** 거기서 멈추지 않는다.
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

                    // 비워 두면 MailKit 이 평문 갈래를 만들지 않는다 — 그래도
                    // 되는 것이지, 있는데 안 싣는 것이 아니다(위 주석).
                    TextBody = string.IsNullOrWhiteSpace(textBody) ? null : textBody,
                }.ToMessageBody()
                : new TextPart("plain") { Text = body };
        }

        using var client = new SmtpClient();

        // 부르는 쪽(AuthServer 는 20초)보다 먼저 포기해야 실패한 까닭이 우리
        // 로그에 남는다. MailKit 의 기본값은 2분이라 그냥 두면 저쪽이 먼저 끊는다.
        if (_settings.TimeoutMs > 0)
        {
            client.Timeout = _settings.TimeoutMs;
        }

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

            _logger.LogInformation("[{Time}] [Info] [Email] Sent to {To} ({Files} attachment(s))",
                DateTime.UtcNow, to, attachments?.Count ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Time}] [Error] [Email] Failed to send to {To}", DateTime.UtcNow, to);
            throw;
        }
    }
}
