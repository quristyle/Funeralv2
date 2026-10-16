using System.Text.Json;
using ModelContextProtocol.Client;

namespace AIAgentServer.Services.Mcp;

/// <summary>모델에게 보여 줄 도구 하나의 생김새.</summary>
public sealed record McpToolInfo(string Name, string Description, JsonElement Schema);

/// <summary>
/// MCP 도구 서버에 붙어 도구 목록을 받고 대신 불러 준다.
/// </summary>
/// <remarks>
/// <para>
/// <b>도구를 코드에 적지 않는다.</b> 어떤 도구가 있는지는 MCP 서버에게 물어서
/// 안다 — 서버에 도구를 하나 더하면 이쪽은 고치지 않아도 모델이 그것을 쓸 수
/// 있게 된다. 그러려고 MCP 를 쓰는 것이다.
/// </para>
/// <para>
/// <b>연결은 한 번만 맺고 아껴 쓴다.</b> 요청마다 붙으면 핸드셰이크가 매번
/// 들어간다. 끊어지면 다음 부를 때 다시 맺는다.
/// </para>
/// </remarks>
public sealed class McpToolGateway : IAsyncDisposable
{
    private readonly IConfiguration _config;
    private readonly ILogger<McpToolGateway> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private McpClient? _client;
    private IList<McpClientTool>? _tools;

    public McpToolGateway(IConfiguration config, ILogger<McpToolGateway> logger)
    {
        _config = config;
        _logger = logger;
    }

    public bool Enabled => _config.GetValue("Mcp:Enabled", true)
                           && !string.IsNullOrWhiteSpace(Endpoint);

    private string? Endpoint => _config["Mcp:Endpoint"];

    /// <summary>도구 목록. 못 붙으면 빈 목록 — 도구 없이 답하면 된다.</summary>
    public async Task<IReadOnlyList<McpToolInfo>> ListAsync(CancellationToken ct = default)
    {
        var tools = await EnsureAsync(ct);
        if (tools is null) return [];

        return [.. tools.Select(t => new McpToolInfo(
            t.Name,
            t.Description ?? string.Empty,
            t.JsonSchema))];
    }

    /// <summary>
    /// 도구를 부른다. <b>실패해도 던지지 않는다</b> — 오류 글을 모델에게 그대로
    /// 돌려주면 모델이 다른 도구를 쓰거나 모른다고 답한다. 예외로 끊으면
    /// 사람에게는 대화가 통째로 죽은 것으로 보인다.
    /// </summary>
    public async Task<string> CallAsync(
        string name, IReadOnlyDictionary<string, object?> args, CancellationToken ct = default)
    {
        var tools = await EnsureAsync(ct);
        var tool = tools?.FirstOrDefault(t => t.Name == name);
        if (tool is null) return $"(그런 도구가 없습니다: {name})";

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(60));

            var result = await tool.CallAsync(args, cancellationToken: cts.Token);

            var texts = result.Content
                .OfType<ModelContextProtocol.Protocol.TextContentBlock>()
                .Select(c => c.Text)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToArray();

            return texts.Length > 0
                ? string.Join("\n", texts)
                : JsonSerializer.Serialize(result.StructuredContent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MCP 도구 호출 실패: {Tool}", name);
            return $"(도구 {name} 를 쓰지 못했습니다: {ex.Message})";
        }
    }

    private async Task<IList<McpClientTool>?> EnsureAsync(CancellationToken ct)
    {
        if (!Enabled) return null;
        if (_tools is not null) return _tools;

        await _gate.WaitAsync(ct);
        try
        {
            if (_tools is not null) return _tools;

            var transport = new HttpClientTransport(new HttpClientTransportOptions
            {
                Endpoint = new Uri(Endpoint!),
                Name = "jsini-tools",
            });

            _client = await McpClient.CreateAsync(transport, cancellationToken: ct);
            _tools = await _client.ListToolsAsync(cancellationToken: ct);

            _logger.LogInformation(
                "MCP 도구 서버에 붙었습니다. 도구 {Count}개 — {Names}",
                _tools.Count, string.Join(", ", _tools.Select(t => t.Name)));

            return _tools;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MCP 도구 서버에 붙지 못했습니다. endpoint={Endpoint}", Endpoint);
            _client = null;
            _tools = null;
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null) await _client.DisposeAsync();
        _gate.Dispose();
    }
}
