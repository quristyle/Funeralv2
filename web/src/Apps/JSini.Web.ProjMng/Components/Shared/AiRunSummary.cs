namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>AI 가 다시 써 준 「처리 요약」 한 벌. 지시와 결과 사이에 놓인다. 서버가 끝난 실행마다 summary_text 에 적어 둔 평문을 되읽는다 — 화면이 모델을 다시 부르면 메일의 요약과 다른 말을 한다. 요약이 없는 건은 빈 상자 없이 지시와 결과만 보인다.</summary>
/// <param name="Headline">한 문장. 무엇을 했나.</param>
/// <param name="Points">한 일·고친 것. 짧은 명사형 몇 줄.</param>
/// <param name="Checks">사람이 확인하거나 이어서 해야 할 것. 없을 수 있다.</param>
internal sealed record AiRunSummary(
    string Headline, IReadOnlyList<string> Points, IReadOnlyList<string> Checks)
{
    /// <summary>「확인할 것」 묶음의 머리말. 서버의 AiResultSummary 와 같은 글자여야 한다 — 한쪽만 고치면 확인 항목이 한 일 목록에 섞인다.</summary>
    private const string ChecksLabel = "확인할 것";

    /// <summary>적을 것이 하나도 없나.</summary>
    public bool IsEmpty => Headline.Length == 0 && Points.Count == 0 && Checks.Count == 0;

    /// <summary>summary_text 에 적힌 평문을 되읽는다. 읽을 것이 없으면 null.</summary>
    public static AiRunSummary? Parse(string? text)
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

            // 머리표가 없는 첫 줄이 결론이다. 그 뒤의 맨줄은 결론의 이어진 줄이라 붙인다 — 버리면 문장이 잘린다.
            if (headline.Length == 0)
            {
                headline = line;
            }
            else if (!inChecks && points.Count == 0)
            {
                headline = $"{headline} {line}";
            }
        }

        var summary = new AiRunSummary(headline, points, checks);

        return summary.IsEmpty ? null : summary;
    }
}
