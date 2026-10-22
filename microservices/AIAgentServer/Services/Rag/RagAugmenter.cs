using System.Text;
using AIAgentServer.DTOs;

namespace AIAgentServer.Services.Rag;

/// <summary>대화에 문서를 끼워 넣는다. 찾은 것을 그대로 믿게 하지 않는다.</summary>
public sealed class RagAugmenter
{
    private readonly RagStore _store;
    private readonly EmbeddingClient _embed;
    private readonly IConfiguration _config;
    private readonly ILogger<RagAugmenter> _logger;

    public RagAugmenter(
        RagStore store, EmbeddingClient embed, IConfiguration config, ILogger<RagAugmenter> logger)
    {
        _store = store;
        _embed = embed;
        _config = config;
        _logger = logger;
    }

    private bool Enabled => _config.GetValue("Rag:AugmentChat", true);
    private int TopK => _config.GetValue("Rag:TopK", 4);

    /// <summary>「사내 자료 전용」에서 끼울 토막 수. 문턱을 안 두는 대신 모델이 가려 쓸 자리를 조금 더 준다.</summary>
    private int OnlyTopK => _config.GetValue("Rag:OnlyTopK", 6);

    /// <summary>「사내 자료 전용」에서 검색어로 이어 붙일 최근 질문 수. 「자료에서 찾아라」 같은 뒷말만으로는 찾을 것이 없다.</summary>
    private const int OnlyQueryTurns = 3;

    /// <summary>대화에서 찾아볼 카테고리. <b>비우면 전부</b>. 카테고리가 늘어나 엉뚱한 자료가 섞이기 시작하면 여기를 좁힌다.</summary>
    private string? ChatCategory
    {
        get
        {
            var v = _config["Rag:ChatCategory"];
            return string.IsNullOrWhiteSpace(v) ? null : v;
        }
    }

    /// <summary>이 점수에 못 미치는 토막은 넣지 않는다. 아주 동떨어진 것만 거른다 — 문턱을 높이면 <b>조금 비껴간 자료까지 버려서</b> 답할 수 있는 질문에 모른다고 하게 된다. 나머지 판단은 모델에게 맡긴다.</summary>
    private double MinScore => _config.GetValue("Rag:MinVectorScore", 0.40);

    /// <summary>자료를 찾아 <paramref name="messages"/> 앞에 끼운다.</summary>
    /// <param name="mode">
    /// <see cref="RagMode.Only"/> 면 점수 문턱을 안 두고(Rag:AugmentChat 도 안 본다) 자료로만 답하라고 이른다 —
    /// 사내 질문도 말투에 따라 벡터 점수가 0.5 대로 떨어져 Rag:MinVectorScore 에 걸린다.
    /// </param>
    /// <param name="scope">사람이 고른 범위. null 이면 Rag:ChatCategory(비었으면 전부)에서 찾는다.</param>
    /// <returns>끼운 결과. 끼우지 않았으면 null.</returns>
    public async Task<RagAugmentResult?> AugmentAsync(
        List<Message> messages, RagMode mode = RagMode.Auto, RagScope? scope = null,
        CancellationToken ct = default)
    {
        var only = mode == RagMode.Only;
        if (mode == RagMode.Off || (!only && !Enabled) || _store.Count == 0) return null;

        var question = only
            ? string.Join("\n", messages
                .Where(m => m.role == "user" && !string.IsNullOrWhiteSpace(m.content))
                .TakeLast(OnlyQueryTurns)
                .Select(m => m.content))
            : messages.LastOrDefault(m => m.role == "user")?.content;
        if (string.IsNullOrWhiteSpace(question)) return null;

        IReadOnlyList<RagHit> hits;
        try
        {
            float[]? vector = null;
            try
            {
                vector = await _embed.EmbedOneAsync(question, ct);
            }
            catch (Exception ex)
            {
                // 임베딩 서버가 죽어도 낱말 검색으로는 찾는다. 대화를 멈출 일이 아니다.
                _logger.LogWarning(ex, "RAG: 임베딩에 실패해 낱말 검색만 합니다.");
            }

            // 범위를 안 정하면 모든 카테고리를 함께 본다.
            hits = _store.Search(
                scope is null ? ChatCategory : scope.Category,
                question, vector, only ? OnlyTopK : TopK, scope?.DocIds);
            if (vector is not null && !only)
            {
                hits = [.. hits.Where(h => h.VectorScore >= MinScore)];
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RAG: 자료를 찾지 못해 그냥 답합니다.");
            return null;
        }

        if (hits.Count == 0) return null;

        // **이미 있는 시스템 메시지에 덧붙인다.** 새로 끼우면 두 가지가 어긋난다 —
        // EnsureSystemPrompt 가 "시스템 메시지가 있으니 됐다" 며 기본 성격을 건너뛰고,
        // Qwen 템플릿은 두 번째 시스템 메시지를 대화 중간의 turn 으로 흘려보낸다.
        var block = only ? BuildOnly(hits) : Build(hits);
        var sys = messages.FirstOrDefault(m => m.role == "system");
        if (sys is not null)
        {
            sys.content = sys.content + "\n\n" + block;
        }
        else
        {
            messages.Insert(0, new Message { role = "system", content = block });
        }

        var sources = hits.Select(h => h.Chunk.Source).Distinct(StringComparer.Ordinal).ToArray();
        var lead = (only ? "사내 자료 전용 · " : string.Empty)
                   + (scope is null ? string.Empty : $"{scope.Label} · ");
        return new RagAugmentResult(
            $"{lead}문서 {sources.Length}건을 참고했습니다 — {string.Join(", ", sources)}",
            hits.Max(h => h.VectorScore));
    }

    private static string Build(IReadOnlyList<RagHit> hits)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# 참고 자료");
        sb.AppendLine();
        sb.AppendLine("아래는 이 조직의 내부 문서에서 질문과 관련있어 보이는 대목을 찾아 온 것이다.");
        sb.AppendLine();
        sb.AppendLine("- 답할 때 이 자료를 **우선 근거로 삼는다.** 자료와 네가 아는 것이 다르면 자료를 따른다.");
        sb.AppendLine("- 쓴 자료의 **파일 이름을 답 끝에 반드시 밝힌다.**");
        sb.AppendLine("- **자료가 질문과 관련이 없으면 그냥 무시한다.** 억지로 끼워 맞추지 말고,");
        sb.AppendLine("  문서에서 찾지 못했다고 말한 뒤 아는 범위에서 답하거나 모른다고 한다.");
        sb.AppendLine();

        AppendHits(sb, hits);
        return sb.ToString();
    }

    private static string BuildOnly(IReadOnlyList<RagHit> hits)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# 사내 자료");
        sb.AppendLine();
        sb.AppendLine("아래는 이 조직의 내부 문서에서 사용자의 질문으로 찾아 온 대목이다.");
        sb.AppendLine("사용자가 **사내 자료로만 답하라**고 골랐다.");
        sb.AppendLine();
        sb.AppendLine("- 질문의 낱말은 회사·고객·프로젝트 이름일 수 있다. 표현이 자료와 조금 달라도(줄임말, 다른 이름)");
        sb.AppendLine("  같은 일을 다루는 자료면 그것으로 답한다.");
        sb.AppendLine("- 자료에 적힌 숫자·표·목록을 그대로 옮겨 정리해 답한다. 웹 검색이나 네가 따로 아는 것을 보태지 않는다.");
        sb.AppendLine("- 아래 자료 어디에도 질문과 이어지는 내용이 없을 때만 「사내 자료에서 찾지 못했습니다」라고 답한다.");
        sb.AppendLine("  자료로 답했으면 그 말을 덧붙이지 않는다.");
        sb.AppendLine("- 쓴 자료의 **파일 이름을 답 끝에 밝힌다.**");
        sb.AppendLine("- 앞선 대화에서 사내 자료를 볼 수 없다고 했거나 웹으로 답했더라도, 지금은 아래 자료로 다시 답한다.");
        sb.AppendLine();

        AppendHits(sb, hits);
        return sb.ToString();
    }

    private static void AppendHits(StringBuilder sb, IReadOnlyList<RagHit> hits)
    {
        foreach (var h in hits)
        {
            sb.AppendLine("---");
            sb.AppendLine($"출처: {h.Chunk.Source}");
            if (h.Chunk.Heading.Length > 0) sb.AppendLine($"위치: {h.Chunk.Heading}");
            sb.AppendLine();
            sb.AppendLine(h.Chunk.Text);
            sb.AppendLine();
        }
    }
}

/// <summary>자료를 끼운 결과.</summary>
/// <param name="Notice">사람에게 보여 줄 안내.</param>
/// <param name="TopScore">
/// 가장 잘 맞은 토막의 점수. <b>"찾았다" 와 "확실하다" 는 다르다</b> —
/// 검색은 아무 질문에나 뭔가를 돌려주므로, 웹을 더 찾을지 가르려면 이 값이 필요하다.
/// 실측에서 사내 질문은 0.57 이상, 바깥 질문은 0.46 이하로 깨끗이 갈렸다.
/// </param>
public sealed record RagAugmentResult(string Notice, double TopScore);