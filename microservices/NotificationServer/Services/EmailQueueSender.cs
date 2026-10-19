using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Options;
using NotificationServer.DTOs;
using NotificationServer.Options;
using RabbitMQ.Client;

namespace NotificationServer.Services;

/// <summary>이메일 발송(큐에 넣는 방식). 결과는 큐에 넣었다까지만 안다.</summary>
public interface IEmailQueueSender
{
    Task<SendEmailResultDto> SendAsync(SendEmailDto request, CancellationToken ct = default);
}

/// <summary>메일 내용을 JSON 파일로 떨어뜨리고 run_script 큐에 발송 스크립트를 넣는다. 배포 장비의 소비자가 실제로 보낸다.
/// JSON 키 이름(title·body·mailto)은 배포 장비 스크립트가 읽으므로 바꾸면 안 된다.</summary>
public class EmailQueueSender : IEmailQueueSender
{
    private readonly EmailQueueOptions _options;
    private readonly ILogger<EmailQueueSender> _logger;

    public EmailQueueSender(IOptions<EmailQueueOptions> options, ILogger<EmailQueueSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SendEmailResultDto> SendAsync(SendEmailDto request, CancellationToken ct = default)
    {
        if (!_options.IsConfigured)
        {
            return new SendEmailResultDto
            {
                Message = "이메일 큐 설정이 없습니다. EmailQueue:SpoolPath·ScriptPath 를 확인하세요."
            };
        }

        if (string.IsNullOrWhiteSpace(request.To))
        {
            return new SendEmailResultDto { Message = "받는 사람이 비어 있습니다." };
        }

        var spoolFile = Path.Combine(
            _options.SpoolPath, $"email_{Guid.NewGuid()}.json");

        try
        {
            Directory.CreateDirectory(_options.SpoolPath);

            // 키 이름은 배포 장비의 스크립트가 읽는 규약이다. 바꾸면 메일이 안 나간다.
            var content = JsonSerializer.Serialize(new
            {
                title = request.Subject,
                body = request.Body,
                mailto = request.To
            });

            await File.WriteAllTextAsync(spoolFile, content, new UTF8Encoding(false), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "메일 내용을 저장하지 못했습니다. path={Path}", spoolFile);
            return new SendEmailResultDto
            {
                Message = $"메일 내용을 저장하지 못했습니다: {ex.Message}"
            };
        }

        try
        {
            // 연결은 보낼 때만 연다(브로커가 내려가 있어도 서비스 기동에는 영향이 없다).
            var factory = new ConnectionFactory
            {
                HostName = string.IsNullOrWhiteSpace(_options.HostName)
                    ? "localhost"
                    : _options.HostName
            };

            await using var connection = await factory.CreateConnectionAsync(ct);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

            // durable 은 기존 큐와 같아야 한다(다르게 선언하면 브로커가 PRECONDITION_FAILED 를 낸다).
            await channel.QueueDeclareAsync(
                queue: _options.QueueName,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: ct);

            var payload = new
            {
                script = _options.ScriptPath,
                args = new[] { request.Subject, spoolFile }
            };

            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: _options.QueueName,
                body: Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)),
                cancellationToken: ct);

            _logger.LogInformation(
                "메일 발송 요청을 큐에 넣었습니다. to={To} spool={Spool}", request.To, spoolFile);

            return new SendEmailResultDto { Queued = true, SpoolFile = spoolFile };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "메일 발송 요청을 큐에 넣지 못했습니다. to={To}", request.To);

            // 떨어뜨린 파일은 지우지 않는다(큐만 복구하면 사람이 다시 밀어 넣을 수 있다).
            return new SendEmailResultDto
            {
                SpoolFile = spoolFile,
                Message = $"메시지 큐에 연결하지 못했습니다: {ex.Message}"
            };
        }
    }
}
