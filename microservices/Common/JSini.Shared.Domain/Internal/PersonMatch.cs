namespace JSini.Shared.Internal;

/// <summary>
/// 사람 하나가 글 하나에 <b>어떤 길로 걸렸는지</b>(쓴 글·맡은 글·댓글)를 쉼표로 이은 글자로 적는다.
/// 업무 비서가 "왜 이 글이 나왔는지" 를 답에 함께 보일 때 쓴다.
/// </summary>
public static class PersonMatch
{
    /// <summary>걸린 길만 차례대로(쓴 글 → 맡은 글 → 댓글) 쉼표로 잇는다. 하나도 안 걸리면 빈 글자다.</summary>
    public static string Of(bool wrote, bool assigned, bool commented)
    {
        var parts = new List<string>(3);
        if (wrote) parts.Add("쓴 글");
        if (assigned) parts.Add("맡은 글");
        if (commented) parts.Add("댓글");
        return string.Join(",", parts);
    }
}
