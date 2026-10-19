using System.Text;
using AIAgentServer.DTOs;

namespace AIAgentServer.Services.Mcp;

/// <summary>보통 대화에 웹에서 찾은 것을 끼워 넣는다. 와 같은 자리, 같은 방식이다</summary>
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
    /// <param name="ragTopScore">사내 문서 검색 점수(0~1). 확실하면 웹을 뒤지지 않는다. 점수로 가르는 것은 말투에 따라 0.1 쯤 흔들려 경계 질문에서 틀릴 수 있다.</param>
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
