using System.Text;
using System.Text.Json;

using AuthServer.DTOs;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace AuthServer.Services;

/// <summary>
/// 배포 실행 서비스 구현체
/// </summary>
/// <remarks>
/// 이 서비스가 하는 일은 "이 스크립트를 돌려 달라"는 메시지를 큐에 넣는 것뿐이다.
/// 실제 실행은 배포 장비에서 도는 큐 소비자가 맡는다.
/// 그래서 여기서는 스크립트의 진행 상황이나 성공 여부를 알 수 없다.
///
/// 예전에는 헬프데스크가 이 일을 했고 대상이 코드에 두 개 박혀 있었다.
/// JSini 포털이 여러 시스템을 관장하므로 대상을 설정(Release:Targets)으로 옮겼다.
/// </remarks>
public class ReleaseService : IReleaseService
{
    private readonly ReleaseOptions _options;
    private readonly ILogger<ReleaseService> _logger;

    public ReleaseService(IOptions<ReleaseOptions> options, ILogger<ReleaseService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public List<ReleaseTargetDto> GetTargets()
    {
        return _options.Targets
            .Where(t => !string.IsNullOrWhiteSpace(t.Key))
            .Select(t => new ReleaseTargetDto
            {
                Key = t.Key,
                Name = string.IsNullOrWhiteSpace(t.Name) ? t.Key : t.Name,
                Description = t.Description,
                EstimatedSeconds = t.EstimatedSeconds
            })
            .ToList();
    }

    public ReleaseResultDto Trigger(string key, string? userId)
    {
        var target = _options.Targets.FirstOrDefault(t =>
            string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase));

        if (target is null)
        {
            return new ReleaseResultDto
            {
                Queued = false,
                TargetKey = key,
                Message = $"'{key}' 배포 대상이 설정에 없습니다."
            };
        }

        if (string.IsNullOrWhiteSpace(target.ScriptPath))
        {
            return new ReleaseResultDto
            {
                Queued = false,
                TargetKey = key,
                Message = $"'{target.Name}' 의 실행 스크립트 경로가 비어 있습니다."
            };
        }

        try
        {
            // 연결은 요청할 때만 연다. 브로커가 내려가 있어도 서비스 기동에는 영향이 없다.
            var factory = new ConnectionFactory
            {
                HostName = string.IsNullOrWhiteSpace(_options.HostName)
                    ? "localhost"
                    : _options.HostName,
                DispatchConsumersAsync = true
            };

            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            channel.QueueDeclare(
                queue: _options.QueueName,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var payload = new { script = target.ScriptPath, args = target.Args.ToArray() };
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));

            channel.BasicPublish(
                exchange: string.Empty,
                routingKey: _options.QueueName,
                basicProperties: null,
                body: body);

            _logger.LogInformation(
                "배포 요청을 큐에 넣었습니다. target={Target} script={Script} user={User}",
                target.Key, target.ScriptPath, userId);

            return new ReleaseResultDto
            {
                Queued = true,
                TargetKey = target.Key,
                Message = $"'{target.Name}' 배포 요청을 보냈습니다."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "배포 요청 전송 실패. target={Target}", target.Key);
            return new ReleaseResultDto
            {
                Queued = false,
                TargetKey = target.Key,
                Message = $"메시지 큐에 연결하지 못했습니다: {ex.Message}"
            };
        }
    }
}
