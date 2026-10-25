namespace AIAgentServer.Services.Portal;

/// <summary>업무 비서 도구가 조회한 줄들을 모델이 읽을 글 한 장으로 다듬는다. 몇 건인지와 상한에 잘렸는지를
/// 머리글에 적는다 — 잘린 것을 안 적으면 모델이 "전체" 로 오해해 자료가 더 있는데도 다 봤다고 답한다.</summary>
public static class PortalToolResult
{
    /// <summary>
    /// <paramref name="lines"/> 는 실제로 보여 줄 줄(이미 <paramref name="cap"/> 만큼 잘려 있다),
    /// <paramref name="total"/> 은 자르기 전 전체 건수다. <paramref name="total"/> 이 <paramref name="cap"/> 을
    /// 넘으면 "총 N건 중 M건"으로, 안 넘으면 "M건"만 적는다. 자료가 없을 때도 빈 글자를 돌려주지 않는다 —
    /// 빈 글자를 주면 모델이 그 빈칸을 다른 도구의 답으로 메워 엉뚱한 소리를 하게 된다.
    /// </summary>
    public static string Render(string title, IReadOnlyList<string> lines, int total, int cap)
    {
        var shown = lines.Count;
        var truncated = total > cap;

        var header = truncated
            ? $"{title} — 총 {total}건 중 {shown}건"
            : $"{title} — {shown}건";

        if (shown == 0)
            return $"{header} (해당하는 자료가 없습니다)";

        return header + "\n" + string.Join("\n", lines);
    }
}
