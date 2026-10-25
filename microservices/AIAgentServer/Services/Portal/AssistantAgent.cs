using System.Diagnostics;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;

using AIAgentServer.Data;
using AIAgentServer.DTOs;
using AIAgentServer.Services.Mcp;
using AIAgentServer.Services.Rag;

using JSini.Shared.Infrastructure.Time;

namespace AIAgentServer.Services.Portal;

/// <summary>업무 비서의 도구 호출 루프와 답 스트리밍. <see cref="WebChatAgent.AskAsync"/> 와 얼개가
/// 같다 — 다른 점은 모델 호출이 대리자로 빠져 라운드 소진·도구 실패를 가짜 모델로 시험할 수 있다는 것과,
/// 마지막 답이 스트리밍으로 나간다는 것뿐이다.</summary>
public sealed class AssistantAgent(
    HttpClient http,
    AiProviderRegistry registry,
    PortalToolRunner toolRunner,
    IServiceScopeFactory scopes,
    IConfiguration config,
    ILogger<AssistantAgent> logger)
{
    /// <summary>모델 호출 한 번. <c>tools</c> 가 <c>null</c> 이면 도구 없이 정리만 시키는 마지막 호출이다.</summary>
    public delegate Task<JsonObject?> ModelCall(JsonArray messages, JsonArray? tools, CancellationToken ct);

    /// <summary>도구 하나를 부른다. <see cref="PortalToolRunner.CallAsync"/> 자체는 예외를 던지지 않지만,
    /// 그 경계 바깥(네트워크 계층 등)에서 새는 예외까지 <see cref="RunAsync"/> 가 도구 결과 글로 바꿔 삼킨다.</summary>
    public delegate Task<string> ToolCall(string name, IReadOnlyDictionary<string, object?> args, CancellationToken ct);

    private const int ToolResultCap = 8000;

    /// <summary>업무 비서 시스템 프롬프트. 오늘 한국 날짜를 박아 넣는다 — 모델은 「오늘」을 모르고,
    /// 사내 자료에 없으면 지어내지 말고 모른다고 하게 못박는다.
    /// 아래 "답의 근거(요청글 번호, …)" 문구의 "요청글" 은 꾸밈말이 아니다 — 화면(web/.../Assistant.razor.cs
    /// 의 RequestRefPattern)이 답 글 속에서 그 낱말과 <c>#id</c> 가 붙어 나온 자리만 요청글 상세 화면으로
    /// 링크를 건다. 이 낱말을 바꾸면(예: "요청 번호") 그 정규식도 함께 고쳐야 한다 — 안 고치면 링크가
    /// 오류 없이 조용히 사라진다.</summary>
    public static string SystemPrompt(DateTime nowUtc, RagScope? scope)
    {
        var todayKorea = AppTime.ToKorea(nowUtc).ToString("yyyy-MM-dd");
        var scopeLine = scope is null
            ? ""
            : $"\n- 문서 검색 범위는 「{scope.Label}」로 좁혀져 있습니다. 이 범위 밖 자료는 찾지 못합니다.";

        return $$"""
            당신은 JSini 업무 포털의 업무 비서입니다. 한국어로 자연스럽게 답하십시오.

            오늘은 {{todayKorea}}(한국 날짜)입니다. "오늘"·"이번 주"·"다음 주" 같은 말은 이 날짜를 기준으로 셉니다.

            당신에게는 포털 안의 자료(요청 글·일정·할 일·메일·문서)를 읽는 도구가 있습니다. 다음을 반드시 지키십시오.

            - 도구 하나가 빈손이어도 **거기서 답을 맺지 마십시오.** 질문에 아직 답하지 못했으면 남은 도구로
              더 찾아본 뒤에 답하십시오 — 특히 사람·조직·프로젝트 이름은 포털 계정에 없어도 사내 문서에
              적혀 있는 일이 많으니 search_documents 를 반드시 한 번은 써 보십시오.
            - **그렇다고 찾아낸 것이 질문의 답이 되는 것은 아닙니다.** 문서 토막에 물어본 이름이나 낱말이
              실제로 **적혀 있지 않으면 그 문서는 그 질문에 대한 자료가 아닙니다.** 비슷한 주제라는
              이유로 그 사람의 직함·역할·한 일을 만들어 내지 마십시오. 인용은 문서에 **그대로 적힌 글**만
              하고, 적혀 있지 않은 것은 찾지 못했다고 답하십시오.
            - **한 도구의 빈자리를 다른 도구가 찾아 준 것으로 메우지 마십시오.** 포털 계정이 없는데
              문서에서 이름을 봤다고 계정이 있는 것처럼 말하면 안 됩니다. 무엇이 어느 자료에서
              나왔는지 갈라 적으십시오(예: "포털 계정은 없습니다. 사내 문서에는 …").
            - 다 찾아봤는데도 아무 도구에서도 안 나오면 그때 **"없습니다"** 라고 답하십시오.
            - 도구 결과가 **상한에 잘렸으면(총 N건 중 M건)** 몇 건 중 몇 건을 봤는지 그대로 적으십시오.
            - 숫자·날짜·글 번호(id)는 도구 결과에 적힌 그대로 옮기십시오. **다시 계산하거나 반올림하지 마십시오.**
            - 답의 근거(요청글 번호, 파일 이름 등)를 반드시 밝히십시오.
            - **자료에 없는 것은 지어내지 마십시오.** 모르면 모른다고 답하십시오.
            - 일정(WBS)의 담당자는 손으로 적은 이름일 뿐 계정과 자동으로 이어지지 않습니다. 같은 이름의 계정이
              여럿일 수 있으니, 어느 계정인지 확정할 수 없으면 그렇게 말하고 넘겨짚지 마십시오.
            - 도구로 찾은 문서나 기록이 질문과 관련이 없으면 억지로 답을 끼워 맞추지 말고 찾지 못했다고 답하십시오.{{scopeLine}}
            """;
    }

    /// <summary>도구 루프 본체. 모델 호출·도구 호출이 모두 대리자라, 가짜 모델로 라운드 소진·도구
    /// 미호출·도구 예외 세 갈래를 전부 시험할 수 있다 — <see cref="WebChatAgent.AskAsync"/> 의 얼개 그대로다.</summary>
    public static async Task<(string Answer, IReadOnlyList<string> Called)> RunAsync(
        ModelCall call, ToolCall tool, JsonArray messages, JsonArray? toolSpec, int maxRounds, CancellationToken ct)
    {
        var called = new List<string>();

        for (var round = 1; round <= maxRounds; round++)
        {
            var message = await call(messages, toolSpec, ct);

            var toolCalls = message?["tool_calls"] as JsonArray;
            if (toolCalls is null || toolCalls.Count == 0)
            {
                var answer = message?["content"]?.GetValue<string>() ?? string.Empty;
                return (answer.Trim(), called);
            }

            // 도구를 부르겠다는 그 말 자체를 대화에 남긴다. 안 남기면 모델이 다음 차례에
            // 자기가 무엇을 요청했는지 모른다.
            messages.Add(JsonNode.Parse(message!.ToJsonString())!);

            foreach (var tc in toolCalls.OfType<JsonObject>())
            {
                var fn = tc["function"] as JsonObject;
                var name = fn?["name"]?.GetValue<string>() ?? string.Empty;
                var rawArgs = fn?["arguments"]?.GetValue<string>() ?? "{}";

                called.Add($"{name}({Shorten(rawArgs)})");

                string result;
                try
                {
                    result = await tool(name, ParseArgs(rawArgs), ct);
                }
                // 취소는 도구 실패가 아니다 — ct 가 끊겨 난 OperationCanceledException 을 여기서
                // 글로 바꿔 삼키면, 이미 끊긴 요청인데도 모델을 한 라운드 더 불러 보고서야(그 호출도
                // 결국 같은 ct 로 터진다) 멈춘다. ct 때문에 난 취소만 그대로 밖으로 내보낸다.
                catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
                {
                    // 도구 하나가 죽어도 루프는 멈추지 않는다 — 그 까닭을 도구 결과 글로 바꿔
                    // 모델에게 넘기면, 모델은 나머지 도구로 모은 것만으로 답을 이어 쓴다.
                    result = $"({name} 도구를 쓰지 못했습니다: {ex.Message})";
                }

                messages.Add(new JsonObject
                {
                    ["role"] = "tool",
                    ["tool_call_id"] = tc["id"]?.GetValue<string>() ?? name,
                    ["content"] = Cap(result, ToolResultCap),
                });
            }
        }

        // 라운드를 다 썼다. 도구 없이 한 번 더 불러 지금까지 모은 것만으로 정리시킨다 —
        // 도구를 또 쥐여 주면 모델이 영원히 더 찾으려 들어 끝나지 않는다.
        messages.Add(new JsonObject
        {
            ["role"] = "user",
            ["content"] = "더 찾지 말고, 지금까지 찾은 것만으로 답하십시오.",
        });

        var last = await call(messages, null, ct);
        return ((last?["content"]?.GetValue<string>() ?? string.Empty).Trim(), called);
    }

    private int MaxRounds => Math.Clamp(config.GetValue("Assistant:MaxToolRounds", 6), 1, 10);

    /// <summary>실제 모델·실제 도구로 <see cref="RunAsync"/> 를 감싸 스트리밍한다. 도구 대리자가 실제로
    /// 그 도구를 부르기 <b>직전에</b> 안내 조각을 채널로 흘려야 화면 진행 표시가 실시간으로 맞는다 — 끝나고
    /// 나서 한꺼번에 모아 보여 주면 "뒤지는 중…" 이 답과 동시에 떠 의미가 없어진다. 감사 기록은 답이
    /// (성공이든 실패든) 확정된 뒤 한 번만 남기고, 그 저장이 실패해도 이미 흘려 보낸 답은 지우지 않는다.</summary>
    public async IAsyncEnumerable<ChatStreamPart> StreamAsync(
        IReadOnlyList<Message> messages,
        RagScope? scope,
        string askedBy,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var startedUtc = AppTime.UtcNow;
        var question = messages.LastOrDefault(m => m.role == "user")?.content ?? string.Empty;

        var target = registry.Resolve("jsini");
        if (!target.IsConfigured)
        {
            throw new InvalidOperationException($"{target.DisplayName} 설정이 완료되지 않았습니다.");
        }

        var internalEnabled = !string.IsNullOrWhiteSpace(config["Assistant:InternalToken"]);
        var toolSpec = BuildToolSpec(PortalToolSet.Tools(internalEnabled));

        var jsonMessages = new JsonArray
        {
            new JsonObject { ["role"] = "system", ["content"] = SystemPrompt(startedUtc, scope) },
        };
        foreach (var m in messages)
        {
            jsonMessages.Add(new JsonObject { ["role"] = m.role, ["content"] = m.content });
        }

        var channel = Channel.CreateUnbounded<ChatStreamPart>();
        var sw = Stopwatch.StartNew();

        var worker = Task.Run(async () =>
        {
            string answer;
            IReadOnlyList<string> called;
            string? error = null;

            try
            {
                (answer, called) = await RunAsync(
                    call: (msgs, toolsArg, callCt) => SendAsync(target, msgs, toolsArg, callCt),
                    tool: async (name, args, toolCt) =>
                    {
                        // 실제로 부르기 직전에 안내부터 흘린다 — 느린 도구일수록 이 순서가 중요하다.
                        await channel.Writer.WriteAsync(ChatStreamPart.Info($"{ToolNotice(name)}…", "tool"), toolCt);
                        return await toolRunner.CallAsync(name, args, scope, toolCt);
                    },
                    messages: jsonMessages,
                    toolSpec: toolSpec,
                    maxRounds: MaxRounds,
                    ct: ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "업무 비서 대화 실패");
                error = ex.Message;
                answer = $"답을 만들지 못했습니다: {ex.Message}";
                called = [];
            }

            sw.Stop();

            await FinalizeAsync(
                channel.Writer,
                answer,
                // 범위가 여럿이면 쉼표로 잇는다 — 기록은 「무엇을 뒤져 답했나」가 전부라
                // 하나만 남기면 나머지 카테고리의 자료가 어디서 왔는지 못 되짚는다.
                saveCt => SaveAuditAsync(
                    askedBy, question,
                    scope?.Categories is { Count: > 0 } cats ? string.Join(",", cats) : null,
                    called, answer, sw.ElapsedMilliseconds, error, saveCt),
                ct);
        }, ct);

        await foreach (var part in channel.Reader.ReadAllAsync(ct))
        {
            yield return part;
        }

        await worker;
    }

    /// <summary>답 쓰기 → 감사 저장 → 채널 닫기, 세 가지를 마무리짓는 자리. <b>사람이 창을 닫아 <paramref name="ct"/>
    /// 가 끊겨도 뒤 두 가지는 반드시 일어나야 한다</b> — 이 루프가 이미 남의 요청글·일정·메일을 읽은 뒤이므로,
    /// 그 흔적이 감사표에 안 남는 순간이 가장 위험하다. 그래서 <c>try/finally</c> 로 묶고(끊겨서 쓰기가
    /// 던져도 finally 는 돈다), 감사 저장은 <paramref name="ct"/> 가 아니라 <see cref="CancellationToken.None"/>
    /// 으로 부른다 — 같은 토큰을 썼다면 저장도 똑같이 끊겨 기록이 사라진다. <paramref name="saveAudit"/> 를
    /// 대리자로 뺀 것은 DB 없이(진짜 <see cref="AiUsageDbContext"/> 없이) 이 보장만 따로 시험하기 위해서다.</summary>
    internal static async Task FinalizeAsync(
        ChannelWriter<ChatStreamPart> writer, string answer, Func<CancellationToken, Task> saveAudit, CancellationToken ct)
    {
        try
        {
            await writer.WriteAsync(ChatStreamPart.Content(answer), ct);
        }
        finally
        {
            await saveAudit(CancellationToken.None);
            writer.Complete();
        }
    }

    /// <summary>감사 기록은 이 기능의 유일한 쓰기다. 아직 운영 DB에 표를 안 넣었으니 지금은 저장이 늘
    /// 실패한다 — 그래도 이미 사용자에게 나간 답을 해치면 안 되므로 로그만 남기고 삼킨다.</summary>
    private async Task SaveAuditAsync(
        string askedBy, string question, string? scopeCategory, IReadOnlyList<string> toolCalls,
        string answer, long elapsedMs, string? error, CancellationToken ct)
    {
        try
        {
            var row = AssistantAudit.Build(
                askedBy, question, scopeCategory, toolCalls, sources: [], answer, elapsedMs, error);

            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AiUsageDbContext>();
            db.AssistantAudits.Add(row);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "업무 비서 감사 기록 저장 실패");
        }
    }

    private static string ToolNotice(string toolName) => toolName switch
    {
        "find_user" => "사람 찾는 중",
        "search_requests" or "get_request" => "요청글 뒤지는 중",
        "search_schedules" => "일정 뒤지는 중",
        "search_todos" => "할 일 뒤지는 중",
        "search_mails" => "보낸 메일 뒤지는 중",
        "search_documents" => "사내 문서 뒤지는 중",
        _ => "자료 찾는 중",
    };

    private async Task<JsonObject?> SendAsync(AiProvider target, JsonArray messages, JsonArray? tools, CancellationToken ct)
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

        using var res = await http.SendAsync(req, cts.Token);
        var body = await res.Content.ReadAsStringAsync(cts.Token);

        if (!res.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"{target.DisplayName} 가 {(int)res.StatusCode} 로 답했습니다. {Cap(body, 200)}");
        }

        return JsonNode.Parse(body)?["choices"]?[0]?["message"] as JsonObject;
    }

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

    private static string Shorten(string text) => text.Length <= 300 ? text : text[..300] + "…";

    private static string Cap(string s, int n) => s.Length <= n ? s : s[..n] + "…";
}
