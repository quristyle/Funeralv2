using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AIAgentServer.Services.Mcp;

/// <summary>한 번의 대화 결과. 답과, 무엇을 근거로 삼았는지.</summary>
public sealed record WebChatResult(
    string Answer,
    IReadOnlyList<WebSource> Sources,
    IReadOnlyList<string> ToolCalls,
    int Rounds);

public sealed record WebSource(string Title, string Url);

/// <summary>도구 단계가 모아 온 것. 답은 들어 있지 않다.</summary>
public sealed record WebEvidence(
    string Text, IReadOnlyList<WebSource> Sources, IReadOnlyList<string> ToolCalls);

/// <summary>
/// 모델이 도구를 써 가며 답하게 한다.
/// </summary>
/// <remarks>
/// <para>
/// 모델은 도구를 <b>직접 부르지 못한다.</b> "이 도구를 이런 인자로 불러 달라" 는
/// 뜻만 내놓을 뿐이고, 실제로 부르고 결과를 다시 넣어 주는 것은 이쪽 일이다.
/// 그 주고받기를 답이 나올 때까지 반복하는 것이 이 클래스다.
/// </para>
/// <para>
/// <b>횟수를 반드시 막는다.</b> 모델이 같은 검색을 끝없이 되풀이하는 일이 있다 —
/// 막지 않으면 한 번의 질문이 수십 번의 바깥 요청으로 불어난다.
/// </para>
/// <para>
/// <b>기존 대화 경로(<c>LLMService</c>)를 건드리지 않는다.</b> 거기에는 공급자
/// 자동전환·사용량 기록이 얽혀 있어, 도구 루프를 끼우면 잘 도는 길을 위험하게
/// 한다. 도구가 필요한 질문은 이 길로 따로 보낸다.
/// </para>
/// </remarks>
public sealed class WebChatAgent
{
    private const string SystemPrompt = """
        당신은 시스템 관리를 돕는 친절하고 전문적인 AI 어시스턴트입니다. 한국어로 자연스럽게 답변해주세요.

        당신에게는 웹을 찾아보는 도구가 있습니다.

        - 최신 정보, 바깥 세상의 사실, 확신이 서지 않는 고유명사는 **추측하지 말고 web_search 로 확인**하십시오.
        - 검색 요약만으로 모자라면 web_fetch 로 그 쪽을 열어 읽으십시오.
        - **웹에서 얻은 내용은 반드시 출처 주소를 함께 밝히십시오.** 답 끝에 참고한 주소를 목록으로 답니다.
        - 찾지 못했으면 찾지 못했다고 말하십시오. 지어내지 마십시오.
        - 이미 아는 것으로 충분한 질문(인사, 번역, 계산 등)에는 도구를 쓰지 마십시오.
        """;

    private readonly HttpClient _http;
    private readonly AiProviderRegistry _registry;
    private readonly McpToolGateway _mcp;
    private readonly IConfiguration _config;
    private readonly ILogger<WebChatAgent> _logger;

    public WebChatAgent(
        HttpClient http,
        AiProviderRegistry registry,
        McpToolGateway mcp,
        IConfiguration config,
        ILogger<WebChatAgent> logger)
    {
        _http = http;
        _registry = registry;
        _mcp = mcp;
        _config = config;
        _logger = logger;
    }

    private int MaxRounds => Math.Clamp(_config.GetValue("Mcp:MaxToolRounds", 4), 1, 8);

    public async Task<WebChatResult> AskAsync(
        string question, string? provider = null, CancellationToken ct = default)
    {
        var target = _registry.Resolve(provider ?? "jsini");
        if (!target.IsConfigured)
        {
            throw new InvalidOperationException($"{target.DisplayName} 설정이 완료되지 않았습니다.");
        }

        var tools = await _mcp.ListAsync(ct);
        var toolSpec = tools.Count > 0 ? BuildToolSpec(tools) : null;

        var messages = new JsonArray
        {
            new JsonObject { ["role"] = "system", ["content"] = SystemPrompt },
            new JsonObject { ["role"] = "user", ["content"] = question },
        };

        var sources = new List<WebSource>();
        var called = new List<string>();

        for (var round = 1; round <= MaxRounds; round++)
        {
            var message = await SendAsync(target, messages, toolSpec, ct);

            var toolCalls = message?["tool_calls"] as JsonArray;
            if (toolCalls is null || toolCalls.Count == 0)
            {
                var answer = message?["content"]?.GetValue<string>() ?? string.Empty;
                return new WebChatResult(answer.Trim(), Dedup(sources), called, round);
            }

            // 도구를 부르겠다는 그 말 자체를 대화에 남긴다. 안 남기면 모델이
            // 다음 차례에 자기가 무엇을 요청했는지 모른다.
            messages.Add(JsonNode.Parse(message!.ToJsonString())!);

            foreach (var tc in toolCalls.OfType<JsonObject>())
            {
                var fn = tc["function"] as JsonObject;
                var name = fn?["name"]?.GetValue<string>() ?? string.Empty;
                var rawArgs = fn?["arguments"]?.GetValue<string>() ?? "{}";

                called.Add($"{name}({Shorten(rawArgs)})");

                var result = await _mcp.CallAsync(name, ParseArgs(rawArgs), ct);
                CollectSources(result, sources);

                messages.Add(new JsonObject
                {
                    ["role"] = "tool",
                    ["tool_call_id"] = tc["id"]?.GetValue<string>() ?? name,
                    ["content"] = Cap(result, 8000),
                });
            }
        }

        // 횟수를 다 썼다. 지금까지 모은 것으로 답하게 한 번 더 부른다 —
        // 도구 없이 부르므로 모델은 더 찾을 수 없고 가진 것으로 정리한다.
        messages.Add(new JsonObject
        {
            ["role"] = "user",
            ["content"] = "더 찾지 말고, 지금까지 찾은 것만으로 답하고 출처를 밝혀라.",
        });

        var last = await SendAsync(target, messages, null, ct);
        return new WebChatResult(
            (last?["content"]?.GetValue<string>() ?? string.Empty).Trim(),
            Dedup(sources), called, MaxRounds);
    }

    /// <summary>
    /// <b>근거만 모으고 답은 쓰지 않는다.</b> 기존 대화 경로에 끼우기 위한 것이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 답까지 여기서 쓰면 <c>LLMService</c> 가 들고 있는 것들(공급자 자동전환 ·
    /// 사용량 기록 · 스트리밍)을 통째로 다시 만들어야 한다. 그래서 <b>도구를
    /// 부르는 데까지만</b> 하고, 모은 것을 글로 넘겨 준다 — 답은 원래 길이 쓴다.
    /// </para>
    /// <para>
    /// <b>값이 없으면 null 이다.</b> 모델이 "검색할 필요 없다" 고 판단하면 그대로
    /// 비워 돌려주고, 대화는 평소처럼 흘러간다.
    /// </para>
    /// </remarks>
    public async Task<WebEvidence?> GatherAsync(
        string question, string? provider, CancellationToken ct = default)
    {
        var tools = await _mcp.ListAsync(ct);
        if (tools.Count == 0) return null;

        var target = _registry.Resolve(provider ?? "jsini");
        if (!target.IsConfigured) return null;

        var toolSpec = BuildToolSpec(tools);
        var messages = new JsonArray
        {
            new JsonObject { ["role"] = "system", ["content"] = GatherPrompt },
            new JsonObject { ["role"] = "user", ["content"] = question },
        };

        var sources = new List<WebSource>();
        var called = new List<string>();
        var gathered = new StringBuilder();

        for (var round = 1; round <= MaxRounds; round++)
        {
            JsonObject? message;
            try
            {
                message = await SendAsync(target, messages, toolSpec, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "웹 도구 단계에 실패해 그냥 답합니다.");
                return null;
            }

            var toolCalls = message?["tool_calls"] as JsonArray;
            if (toolCalls is null || toolCalls.Count == 0) break;

            messages.Add(JsonNode.Parse(message!.ToJsonString())!);

            foreach (var tc in toolCalls.OfType<JsonObject>())
            {
                var fn = tc["function"] as JsonObject;
                var name = fn?["name"]?.GetValue<string>() ?? string.Empty;
                var rawArgs = fn?["arguments"]?.GetValue<string>() ?? "{}";

                called.Add($"{name}({Shorten(rawArgs)})");
                var result = await _mcp.CallAsync(name, ParseArgs(rawArgs), ct);
                CollectSources(result, sources);

                gathered.AppendLine($"### {name} {Shorten(rawArgs)}");
                gathered.AppendLine(Cap(result, 6000));
                gathered.AppendLine();

                messages.Add(new JsonObject
                {
                    ["role"] = "tool",
                    ["tool_call_id"] = tc["id"]?.GetValue<string>() ?? name,
                    ["content"] = Cap(result, 8000),
                });
            }
        }

        if (gathered.Length == 0) return null;

        return new WebEvidence(gathered.ToString(), Dedup(sources), called);
    }

    /// <summary>
    /// 근거 모으기 단계의 지시. <b>답을 쓰지 말라</b>고 분명히 적는다 —
    /// 안 적으면 모델이 도구를 안 부르고 아는 것으로 답해 버려서,
    /// 그 답은 버려지고 검색도 안 된 채로 끝난다.
    /// </summary>
    private const string GatherPrompt = """
        당신은 질문에 답하기 전에 **자료를 모으는 역할**만 맡습니다. 답을 쓰지 마십시오.

        - 질문이 최신 정보·바깥 세상의 사실·확신 없는 고유명사에 걸리면 web_search 로 찾으십시오.
        - 요약으로 모자라면 web_fetch 로 그 쪽을 열어 읽으십시오.
        - 이미 아는 것으로 충분하거나(인사·계산·번역), 조직 내부 이야기라 웹에 없을 질문이면
          **아무 도구도 부르지 말고 그대로 두십시오.**
        """;

    private static JsonArray BuildToolSpec(IReadOnlyList<McpToolInfo> tools)
    {
        var arr = new JsonArray();
        foreach (var t in tools)
        {
            arr.Add(new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = t.Name,
                    ["description"] = t.Description,
                    ["parameters"] = JsonNode.Parse(t.Schema.GetRawText()),
                },
            });
        }

        return arr;
    }

    private async Task<JsonObject?> SendAsync(
        AiProvider target, JsonArray messages, JsonArray? tools, CancellationToken ct)
    {
        var payload = new JsonObject
        {
            ["model"] = target.Model,
            ["messages"] = JsonNode.Parse(messages.ToJsonString()),
            ["temperature"] = 0.3,
            ["max_tokens"] = target.MaxTokens > 0 ? target.MaxTokens : 2000,
        };
        if (tools is not null) payload["tools"] = JsonNode.Parse(tools.ToJsonString());

        using var req = new HttpRequestMessage(HttpMethod.Post, target.ApiBase)
        {
            Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        if (!string.IsNullOrWhiteSpace(target.ApiKey))
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", target.ApiKey);
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(target.TimeoutSeconds > 0 ? target.TimeoutSeconds : 120));

        using var res = await _http.SendAsync(req, cts.Token);
        var body = await res.Content.ReadAsStringAsync(cts.Token);

        if (!res.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"{target.DisplayName} 가 {(int)res.StatusCode} 로 답했습니다. {Cap(body, 200)}");
        }

        return JsonNode.Parse(body)?["choices"]?[0]?["message"] as JsonObject;
    }

    private static Dictionary<string, object?> ParseArgs(string raw)
    {
        try
        {
            var obj = JsonNode.Parse(raw) as JsonObject;
            if (obj is null) return [];

            var d = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var (k, v) in obj)
            {
                d[k] = v is null ? null : v.GetValueKind() switch
                {
                    JsonValueKind.Number => v.GetValue<double>(),
                    JsonValueKind.True or JsonValueKind.False => v.GetValue<bool>(),
                    _ => v.ToString(),
                };
            }

            return d;
        }
        catch
        {
            return [];
        }
    }

    /// <summary>도구가 돌려준 글에서 주소를 거둔다. 답 아래에 붙일 출처 목록이 된다.</summary>
    private static void CollectSources(string toolResult, List<WebSource> into)
    {
        try
        {
            var node = JsonNode.Parse(toolResult);
            if (node?["results"] is JsonArray arr)
            {
                foreach (var r in arr.OfType<JsonObject>())
                {
                    var url = r["Url"]?.GetValue<string>() ?? r["url"]?.GetValue<string>();
                    if (string.IsNullOrWhiteSpace(url)) continue;
                    into.Add(new WebSource(
                        r["Title"]?.GetValue<string>() ?? r["title"]?.GetValue<string>() ?? url, url));
                }
            }
            else if (node?["url"]?.GetValue<string>() is { Length: > 0 } one)
            {
                into.Add(new WebSource(node["title"]?.GetValue<string>() ?? one, one));
            }
        }
        catch
        {
            // 도구가 JSON 이 아닌 글을 줄 수도 있다. 출처를 못 거두는 것뿐이다.
        }
    }

    private static IReadOnlyList<WebSource> Dedup(List<WebSource> src)
        => [.. src.GroupBy(s => s.Url, StringComparer.OrdinalIgnoreCase).Select(g => g.First())];

    private static string Cap(string s, int n) => s.Length <= n ? s : s[..n] + "…";
    private static string Shorten(string s) => Cap(s.Replace("\n", " "), 80);
}
