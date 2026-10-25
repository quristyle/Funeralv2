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

/// <summary>
/// WBS 일감 하나에 사람 하나가 <b>어느 역할로 걸렸는지</b>(개발·빌드·검수)를 쉼표로 이은 글자로 적는다.
/// <see cref="PersonMatch"/> 와 같은 모양이지만 역할 이름이 헬프데스크와 달라 자리를 나눴다 —
/// "어떤 역할로 걸렸는지" 를 글자로 묶는 일은 이 한 곳에서만 한다.
/// </summary>
public static class WbsRoleMatch
{
    /// <summary>걸린 역할만 차례대로(개발 → 빌드 → 검수) 쉼표로 잇는다. 하나도 안 걸리면 빈 글자다.</summary>
    public static string Of(bool dev, bool build, bool qc)
    {
        var parts = new List<string>(3);
        if (dev) parts.Add("개발");
        if (build) parts.Add("빌드");
        if (qc) parts.Add("검수");
        return string.Join(",", parts);
    }
}

/// <summary>
/// WBS 의 개발·빌드·검수 담당 칸(<c>dev_user</c>·<c>build_user</c>·<c>qc_user</c>)은 로그인 아이디가
/// 아니라 사람이 손으로 적은 글이다 — "박경수/완료" 처럼 뒤에 군더더기가 붙기도 한다. 그래서
/// 단순 같음 비교나 <c>LIKE '박경수%'</c> 로는 안 된다(뒤엣것은 "박경수영" 같은 다른 사람도 집는다).
/// 이 칸의 값이 후보(로그인 아이디 또는 이름)를 가리키는지 한 곳에서만 가린다.
/// </summary>
public static class WbsPersonMatch
{
    /// <summary>
    /// 칸 값이 후보와 같거나(앞뒤 공백 무시, 대소문자 무시), 후보로 시작하면서 그다음 글자가
    /// 글자·숫자가 아닐 때(구분자로 군더더기가 붙은 모양) 가리킨 것으로 본다.
    /// </summary>
    public static bool Matches(string? columnValue, string? candidate)
    {
        if (string.IsNullOrWhiteSpace(columnValue) || string.IsNullOrWhiteSpace(candidate))
            return false;

        var value = columnValue.Trim();
        var target = candidate.Trim();

        if (value.Equals(target, StringComparison.OrdinalIgnoreCase))
            return true;

        return value.Length > target.Length
               && value.StartsWith(target, StringComparison.OrdinalIgnoreCase)
               && !char.IsLetterOrDigit(value[target.Length]);
    }
}
