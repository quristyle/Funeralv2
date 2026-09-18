using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace ProjMngServer.Services;

/// <summary>
/// AI 가 다시 써 준 결과 요약 한 벌. <b>메일의 「AI 의 답」 칸이 이것으로 그려진다.</b>
/// </summary>
/// <param name="Headline">한 문장. 무엇을 했나.</param>
/// <param name="Points">한 일·고친 것. 짧은 명사형 몇 줄.</param>
/// <param name="Checks">사람이 확인하거나 이어서 해야 할 것. 없을 수 있다.</param>
public sealed record AiResultSummary(
    string Headline, IReadOnlyList<string> Points, IReadOnlyList<string> Checks)
{
    /// <summary>「확인할 것」 묶음의 머리말. <see cref="ToText"/> 와 <see cref="Parse"/> 가 같이 쓴다.</summary>
    private const string ChecksLabel = "확인할 것";

    public bool IsEmpty => Headline.Length == 0 && Points.Count == 0;

    /// <summary>
    /// <c>ai_task_run.summary_text</c> 에 남길 꼴. <b>사람이 읽는 평문이어야 한다</b> —
    /// 이어가기 지시문이 이 칸을 그대로 다음 실행의 문맥으로 올려보낸다
    /// (<c>AiTaskService.BuildContinuation</c>).
    /// </summary>
    public string ToText()
    {
        var sb = new StringBuilder();

        if (Headline.Length > 0)
        {
            sb.AppendLine(Headline);
        }

        if (Points.Count > 0)
        {
            sb.AppendLine();

            foreach (var p in Points)
            {
                sb.AppendLine($"- {p}");
            }
        }

        if (Checks.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(ChecksLabel);

            foreach (var c in Checks)
            {
                sb.AppendLine($"- {c}");
            }
        }

        return sb.ToString().Trim();
    }

    /// <summary>
    /// <see cref="ToText"/> 로 남긴 것을 되읽는다. <b>같은 실행을 두 번 요약하지 않기
    /// 위해서다</b> — 모델을 한 번 더 부르는 일이고, 그 값은 이미 DB 에 있다.
    /// </summary>
    public static AiResultSummary? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var headline = string.Empty;
        var points = new List<string>();
        var checks = new List<string>();
        var inChecks = false;

        foreach (var raw in text.Replace("\r", string.Empty).Split('\n'))
        {
            var line = raw.Trim();

            if (line.Length == 0)
            {
                continue;
            }

            if (line == ChecksLabel)
            {
                inChecks = true;
                continue;
            }

            if (line.StartsWith('-'))
            {
                var item = line.TrimStart('-', ' ').Trim();

                if (item.Length == 0)
                {
                    continue;
                }

                (inChecks ? checks : points).Add(item);
                continue;
            }

            if (headline.Length == 0)
            {
                headline = line;
            }
        }

        var summary = new AiResultSummary(headline, points, checks);

        return summary.IsEmpty ? null : summary;
    }
}

/// <summary>
/// 끝난 실행의 결과문을 <b>AI 에게 한 번 더 정리시킨다.</b>
/// </summary>
/// <remarks>
/// <para>
/// <b>왜 이것이 생겼나.</b> 메일의 요약 칸은 결과문의 앞 몇 줄을 그대로 떠서
/// 보여 줬다. 결과문이 「먼저 파일을 찾아보겠습니다」 같은 말로 시작하거나
/// 표·코드로 시작하면, <b>답을 가장 잘 보여 줘야 할 자리에 서론이 앉는다.</b>
/// 결론은 대개 글 끝에 있는데 메일은 첫 화면에서 끝나야 하니 그 둘이 어긋났다.
/// </para>
/// <para>
/// <b>예전에는 이것을 일부러 하지 않았다.</b> 「메일 한 통마다 모델을 한 번 더
/// 부르고, 그 호출이 실패하면 메일이 안 나간다」가 이유였다. 그 걱정은 그대로
/// 옳아서, 세 가지로 막아 둔다.
/// </para>
/// <list type="number">
///   <item><description><b>메일이 실제로 나갈 건에만 부른다.</b> 「받기」가 꺼져
///   있거나 받는 사람이 없어 어차피 안 나갈 건은 여기까지 오지 않는다.</description></item>
///   <item><description><b>실패하면 null 이다.</b> 던지지 않는다 — 부르는 쪽이
///   옛 방식(앞 몇 줄 뜨기)으로 그대로 보낸다. <b>요약 때문에 메일이 빠지는 일은
///   없다.</b></description></item>
///   <item><description><b>시간을 짧게 건다.</b> 결과 메일은 빨리 와야 뜻이 있다.
///   기다리느니 옛 방식으로 보내는 편이 낫다.</description></item>
/// </list>
/// <para>
/// <b>보내기 전에 반드시 가린다(<see cref="SecretMask"/>).</b> 결과문에는 운영
/// 장비에서 긁힌 설정 파일이 섞여 들어올 수 있고, 이 호출은 우리 장비 밖으로
/// 나갈 수 있는 경로다(공급자가 외부 API 인 경우). 메일보다 앞서 가리는 자리다.
/// </para>
/// </remarks>
public sealed class AiResultSummarizer(
    IConfiguration configuration, IHttpClientFactory http, ILogger<AiResultSummarizer> logger)
{
    /// <summary>
    /// 정리시킬 것인가. <b>끌 수 있어야 한다</b> — AI 가 안 떠 있는 장비에서
    /// 메일마다 헛호출을 하고 시간만 버리는 일을 막는 손잡이다.
    /// </summary>
    private readonly bool _enabled =
        configuration.GetValue("AiTasks:SummarizeResult", defaultValue: true);

    /// <summary>AI 서비스 주소. 알림 서비스와 마찬가지로 같은 장비 안이라 루프백이다.</summary>
    private readonly string _aiUrl =
        configuration["AiTasks:AiUrl"] is { Length: > 0 } u ? u : "http://127.0.0.1:5029";

    /// <summary>비워 두면 AI 서비스의 기본 공급자가 받는다.</summary>
    private readonly string? _provider = configuration["AiTasks:SummaryProvider"];

    private readonly string? _model = configuration["AiTasks:SummaryModel"];

    private readonly int _timeoutSeconds =
        configuration.GetValue("AiTasks:SummaryTimeoutSeconds", defaultValue: 30);

    /// <summary>
    /// 정리시킬 글의 길이 상한. <b>넉넉히 잡으면 안 된다</b> — 무료 공급자는
    /// 분당 토큰으로도 한도를 걸어서, 긴 글 한 번이 그 몫을 다 태우고 다음
    /// 요청을 곧바로 막는다(<c>AI:Providers</c> 의 <c>MaxHistoryChars</c> 주석과 같은 이유).
    /// </summary>
    private readonly int _maxChars =
        configuration.GetValue("AiTasks:SummaryMaxChars", defaultValue: 6000);

    /// <summary>
    /// 편집자의 성격. <b>사실을 더하지 말라</b>가 이 프롬프트의 전부다 —
    /// 요약이 원문에 없는 말을 하면 그 메일은 읽을수록 해롭다.
    /// </summary>
    private const string SystemPrompt =
        "당신은 기술 보고서 편집자입니다. 개발자가 아닌 사람이 휴대폰에서 읽을 "
        + "메일에 들어갈 요약을 씁니다. 주어진 글에 있는 사실만 쓰고, 없는 것은 "
        + "절대 지어내지 마십시오. 한국어로 쓰고, 설명 없이 JSON 하나만 답합니다.";

    /// <summary>
    /// 결과문을 정리해 돌려준다. <b>어떤 이유로든 못 하면 null 이다 — 던지지 않는다.</b>
    /// </summary>
    public async Task<AiResultSummary?> SummarizeAsync(
        string? instruction, string? resultText, CancellationToken ct = default)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(resultText))
        {
            return null;
        }

        try
        {
            // **가리는 것이 먼저다.** 이 아래로는 우리 장비 밖으로 나갈 수 있다.
            var body = Clip(SecretMask.Apply(resultText), _maxChars);
            var asked = Clip(SecretMask.Apply(instruction), 1500);

            var client = http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(_timeoutSeconds);

            using var req = new HttpRequestMessage(HttpMethod.Post, $"{_aiUrl.TrimEnd('/')}/chat")
            {
                Content = JsonContent.Create(new
                {
                    provider = _provider,
                    model = _model,
                    messages = new[]
                    {
                        new { role = "system", content = SystemPrompt },
                        new { role = "user", content = Ask(asked, body) },
                    },
                }),
            };

            // 서비스 간 직접 호출이라 자기 이름을 적어 보낸다 — 알림 호출과 같다.
            req.Headers.Add("X-User-Id", "AI_TASK");

            using var res = await client.SendAsync(req, ct);

            if (!res.IsSuccessStatusCode)
            {
                logger.LogInformation(
                    "결과 요약을 건너뜁니다 — AI 서비스가 HTTP {Status} 로 답했습니다.",
                    (int)res.StatusCode);
                return null;
            }

            var reply = Unwrap(await res.Content.ReadAsStringAsync(ct));

            return reply is null ? null : FromJson(reply);
        }
        catch (Exception ex)
        {
            // **여기서 실패해도 메일은 나가야 한다.** 부르는 쪽이 옛 방식으로 보낸다.
            logger.LogInformation(ex, "결과 요약을 건너뜁니다 — AI 를 부르지 못했습니다.");
            return null;
        }
    }

    /// <summary>
    /// 편집을 시키는 한 통. <b>칸과 길이를 못 박는다</b> — 자유롭게 쓰게 두면
    /// 메일에 다시 서론이 앉는다.
    /// </summary>
    private static string Ask(string? instruction, string? resultText) => $$"""
        아래는 AI 실행기가 지시를 처리하고 남긴 결과 원문입니다.
        이것을 메일 요약 칸에 넣을 수 있게 정리해 주십시오.

        [규칙]
        - headline: 한 문장(60자 이내). 무엇을 했는지 결론부터.
        - points: 한 일·고친 것을 3~6개. 각 80자 이내, 명사형으로 짧게.
        - checks: 사람이 확인하거나 이어서 해야 할 것 0~3개. 없으면 빈 배열.
        - 원문에 없는 것을 지어내지 마십시오. 코드·로그·경로를 그대로 옮기지 마십시오.
        - 「~하겠습니다」 같은 서론은 버리고 실제로 한 것만 남기십시오.
        - 설명·머리말 없이 아래 모양의 JSON 하나만 답하십시오.

        {"headline":"...","points":["...","..."],"checks":["..."]}

        ## 시킨 것
        {{instruction ?? "(적힌 것이 없습니다)"}}

        ## 결과 원문
        {{resultText}}
        """;

    /// <summary>
    /// 공용 응답 봉투에서 답 글자만 꺼낸다 — <c>{ data: { result: ["…"] } }</c>.
    /// </summary>
    private static string? Unwrap(string? envelope)
    {
        if (string.IsNullOrWhiteSpace(envelope))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(envelope);

            if (doc.RootElement.TryGetProperty("data", out var data)
                && data.TryGetProperty("result", out var result)
                && result.ValueKind == JsonValueKind.Array
                && result.GetArrayLength() > 0)
            {
                return result[0].GetString();
            }
        }
        catch (JsonException)
        {
            // 봉투가 아니면 아래에서 null 이다.
        }

        return null;
    }

    /// <summary>
    /// 모델이 준 글에서 JSON 을 꺼내 요약으로 만든다.
    /// </summary>
    /// <remarks>
    /// <b>「JSON 만 답하라」를 지키지 않는 모델이 있다.</b> 앞뒤에 인사말이나
    /// <c>```json</c> 울타리를 붙인다. 그래서 첫 <c>{</c> 와 마지막 <c>}</c> 사이만
    /// 떠서 읽는다. 그래도 안 되면 null 이고, 메일은 옛 방식으로 나간다.
    /// </remarks>
    private AiResultSummary? FromJson(string reply)
    {
        var start = reply.IndexOf('{');
        var end = reply.LastIndexOf('}');

        if (start < 0 || end <= start)
        {
            logger.LogInformation("결과 요약을 건너뜁니다 — 모델이 JSON 을 주지 않았습니다.");
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(reply[start..(end + 1)]);
            var root = doc.RootElement;

            var headline = root.TryGetProperty("headline", out var h)
                ? Tidy(h.GetString(), 120)
                : string.Empty;

            var summary = new AiResultSummary(
                headline,
                Items(root, "points", max: 6),
                Items(root, "checks", max: 3));

            return summary.IsEmpty ? null : summary;
        }
        catch (JsonException ex)
        {
            logger.LogInformation(ex, "결과 요약을 건너뜁니다 — JSON 을 읽지 못했습니다.");
            return null;
        }
    }

    /// <summary>배열 칸 하나를 읽는다. 없거나 배열이 아니면 빈 목록이다.</summary>
    private static IReadOnlyList<string> Items(JsonElement root, string name, int max)
    {
        if (!root.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return array.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => Tidy(e.GetString(), 140))
            .Where(s => s.Length > 0)
            .Take(max)
            .ToList();
    }

    /// <summary>
    /// 한 줄을 다듬는다. <b>머리표를 뗀다</b> — 메일은 이미 점을 찍어 주므로
    /// <c>- </c> 나 <c>* </c> 가 붙으면 점이 둘이 된다.
    /// </summary>
    private static string Tidy(string? text, int max)
    {
        var t = (text ?? string.Empty)
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Trim()
            .TrimStart('-', '*', '·', '#', ' ')
            .Trim();

        return t.Length <= max ? t : t[..max] + "…";
    }

    /// <summary>
    /// 너무 긴 글을 줄인다. <b>앞뒤를 남긴다</b> — 결론은 대개 끝에 있어서
    /// 앞만 자르면 정리시킬 알맹이가 통째로 빠진다.
    /// </summary>
    private static string? Clip(string? text, int max)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length <= max)
        {
            return text?.Trim();
        }

        var head = max * 2 / 3;
        var tail = max - head;

        return $"{text[..head].Trim()}\n\n… (가운데 {text.Length - max}자 줄임) …\n\n{text[^tail..].Trim()}";
    }
}
