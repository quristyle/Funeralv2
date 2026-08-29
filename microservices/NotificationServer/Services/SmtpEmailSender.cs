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

    public async Task SendAsync(string to, string subject, string body)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromDisplay, _settings.User));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();

        if (_settings.IgnoreCertificateErrors)
        {
            client.ServerCertificateValidationCallback = (s, c, h, e) => true;
        }

        try
        {
            await client.ConnectAsync(_settings.Host, _settings.Port, _settings.UseSsl);
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
