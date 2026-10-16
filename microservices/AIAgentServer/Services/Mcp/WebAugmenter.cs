using System.Text;
using AIAgentServer.DTOs;

namespace AIAgentServer.Services.Mcp;

/// <summary>
/// 보통 대화에 웹에서 찾은 것을 끼워 넣는다.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Rag.RagAugmenter"/> 와 같은 자리, 같은 방식이다 — 시스템 프롬프트에
/// 덧붙이고 답은 원래 길이 쓴다. 자료의 출처가 사내 문서냐 웹이냐만 다르다.
/// </para>
/// <para>
/// <b>값을 치른다.</b> 도구를 쓸지 판단하려면 모델을 한 번 더 불러야 하므로,
/// 검색이 필요 없는 질문에도 왕복이 한 번 늘어난다. 그래서 끌 수 있게 해 두었고
/// (<c>Mcp:AugmentChat</c>), 기본은 꺼 둔다 — 포털 대화는 대부분 사내 이야기라
/// 웹을 찾을 일이 드물다. 쓰는 화면에서 켜는 편이 맞다.
/// </para>
/// </remarks>
public sealed class WebAugmenter
{
    private readonly WebChatAgent _agent;
    private readonly IConfiguration _config;
    private readonly ILogger<WebAugmenter> _logger;

    public WebAugmenter(WebChatAgent agent, IConfiguration config, ILogger<WebAugmenter> logger)
    {
        _agent = agent;
        _config = config;
        _logger = logger;
    }

    public bool Enabled => _config.GetValue("Mcp:AugmentChat", false);

    /// <returns>사람에게 보여 줄 안내. 찾은 것이 없으면 null.</returns>
    /// <param name="ragTopScore">
    /// 사내 문서 검색이 얼마나 잘 맞았는지(0~1). <b>확실하면 웹을 뒤지지 않는다.</b>
    ///
    /// <para>
    /// 도구 단계는 질문만 보므로 "배포 롤백" 을 일반 기술 질문으로 읽고 웹을
    /// 뒤진다 — 사내 문서에 답이 있는데도 그렇고, 그래서 1.1초가 7~10초가 됐다.
    /// </para>
    /// <para>
    /// 처음에는 "찾았으면 건너뛴다" 로 했다가 <b>바깥 질문까지 막혔다</b> —
    /// 검색은 아무 질문에나 뭔가를 돌려주기 때문이다. 점수로 갈라야 한다.
    /// 실측: 사내 0.573~0.756, 바깥 0.407~0.511. 그 사이가 기본값(0.55)이다.
    /// </para>
    /// <para>
    /// <b>여유가 좁다.</b> 같은 주제라도 말투에 따라 점수가 0.1 쯤 흔들린다
    /// ("vLLM 최신 버전" 0.407 ↔ "vLLM 최신 버전? 한 문장." 0.511).
    /// 경계에 걸린 질문은 틀릴 수 있다 — 더 단단히 하려면 점수로 가르지 말고
    /// 도구 단계에 "사내 문서에서 이런 것을 찾았다" 를 함께 넘겨 모델이
    /// 판단하게 해야 한다. 그러면 사내 질문마다 왕복이 하나 는다.
    /// </para>
    /// </param>
    public async Task<string?> AugmentAsync(
        List<Message> messages, string? provider, double ragTopScore = 0,
        CancellationToken ct = default)
    {
        if (!Enabled) return null;
        if (ragTopScore >= _config.GetValue("Mcp:SkipWhenRagScore", 0.55)) return null;

        var question = messages.LastOrDefault(m => m.role == "user")?.content;
        if (string.IsNullOrWhiteSpace(question)) return null;

        WebEvidence? found;
        try
        {
            found = await _agent.GatherAsync(question, provider, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "웹 자료를 모으지 못해 그냥 답합니다.");
            return null;
        }

        if (found is null) return null;

        var block = Build(found);
        var sys = messages.FirstOrDefault(m => m.role == "system");
        if (sys is not null)
        {
            sys.content = sys.content + "\n\n" + block;
        }
        else
        {
            messages.Insert(0, new Message { role = "system", content = block });
        }

        var hosts = found.Sources
            .Select(s => Uri.TryCreate(s.Url, UriKind.Absolute, out var u) ? u.Host : s.Url)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToArray();

        return $"웹에서 {found.Sources.Count}건을 찾았습니다 — {string.Join(", ", hosts)}";
    }

    private static string Build(WebEvidence e)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# 웹에서 찾은 것");
        sb.AppendLine();
        sb.AppendLine("아래는 이 질문에 답하려고 방금 웹에서 찾아 온 것이다.");
        sb.AppendLine();
        sb.AppendLine("- 여기 있는 내용을 쓸 때는 **주소를 반드시 밝힌다.**");
        sb.AppendLine("- 질문과 맞지 않으면 무시하고, 찾지 못했다고 말한다. 지어내지 않는다.");
        sb.AppendLine();
        sb.Append(e.Text);

        if (e.Sources.Count > 0)
        {
            sb.AppendLine("## 찾은 곳");
            foreach (var s in e.Sources)
            {
                sb.AppendLine($"- {s.Title} — {s.Url}");
            }
        }

        return sb.ToString();
    }
}
