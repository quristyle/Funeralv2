namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>
/// AI 가 다시 써 준 「처리 요약」 한 벌. <b>지시와 결과 사이에 놓인다.</b>
/// </summary>
/// <remarks>
/// <para>
/// [이 값은 화면을 위해 새로 만든 것이 아니다]
/// </para>
/// <para>
/// 결과 메일의 「무엇을 했다나」 칸이 예전부터 이것을 썼다. 실행기의 답은
/// 「먼저 …를 찾아보겠습니다」로 시작해 결론이 끝에 있는 일이 잦아서,
/// 서버가 끝난 실행마다 AI 에게 한 번 더 정리시켜
/// <c>projmng.ai_task_run.summary_text</c> 에 적어 둔다
/// (<c>ProjMngServer/Services/AiResultSummarizer.cs</c>).
/// </para>
/// <para>
/// <b>그러니 화면이 요약을 다시 만들지 않는다.</b> 적혀 있는 것을 읽어 그린다 —
/// 모델을 한 번 더 부르면 메일에 적힌 요약과 화면에 뜬 요약이 다른 말을 하게
/// 되고, 그때 어느 쪽이 그 실행의 요약인지 가릴 방법이 없다.
/// </para>
/// <para>
/// [왜 평문을 되읽나]
/// </para>
/// <para>
/// 그 칸은 <b>사람이 읽는 평문</b>이다 — 이어가기 지시문이 이 칸을 그대로 다음
/// 실행의 문맥으로 올려보내기 때문이다(<c>AiTaskService.BuildContinuation</c>).
/// JSON 으로 담으면 다음 실행이 중괄호를 문맥으로 읽는다. 그래서 담는 쪽
/// (<c>AiResultSummary.ToText</c>)과 여기가 <b>같은 모양을 약속</b>하고 있다.
/// </para>
/// <code>
/// 한 문장 결론
///
/// - 한 일
/// - 고친 것
///
/// 확인할 것
/// - 사람이 볼 것
/// </code>
/// <para>
/// <b>못 읽으면 null 이다.</b> 요약이 없는 건(예전 것, AI 가 안 떠 있던 때,
/// 정리에 실패한 건)이 그냥 있고, 그런 건은 요약 칸 없이 지시와 결과만 보인다 —
/// <b>빈 상자를 그려 「요약이 고장났다」로 읽히게 두지 않는다.</b>
/// </para>
/// </remarks>
/// <param name="Headline">한 문장. 무엇을 했나.</param>
/// <param name="Points">한 일·고친 것. 짧은 명사형 몇 줄.</param>
/// <param name="Checks">사람이 확인하거나 이어서 해야 할 것. 없을 수 있다.</param>
internal sealed record AiRunSummary(
    string Headline, IReadOnlyList<string> Points, IReadOnlyList<string> Checks)
{
    /// <summary>
    /// 「확인할 것」 묶음의 머리말. <b>서버의 <c>AiResultSummary</c> 와 같은 글자여야
    /// 한다</b> — 한쪽만 고치면 확인 항목이 한 일 목록에 섞여 들어간다.
    /// </summary>
    private const string ChecksLabel = "확인할 것";

    /// <summary>적을 것이 하나도 없나.</summary>
    public bool IsEmpty => Headline.Length == 0 && Points.Count == 0 && Checks.Count == 0;

    /// <summary>
    /// <c>summary_text</c> 에 적힌 평문을 되읽는다. 읽을 것이 없으면 <c>null</c>.
    /// </summary>
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

            // 머리표가 없는 첫 줄이 결론이다. 그 뒤에 또 나오는 맨줄은
            // 결론의 이어진 줄이라 보고 붙인다 — 버리면 문장이 잘린다.
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
