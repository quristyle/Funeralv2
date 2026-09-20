namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>
/// 카드 한 장이 <b>어느 AI 에게 간 건인지</b>를 말하는 한 글자 배지.
/// </summary>
/// <remarks>
/// <para>
/// [왜 한 글자인가]
/// </para>
/// <para>
/// 「빠른 지시」의 카드는 <b>반 폭</b>이고 두 층뿐이다(<c>pm-ask__item</c>).
/// 거기에 「Claude CLI」·「안티그래비티 (agy)」를 그대로 적으면 그 한 칸이
/// <b>제목이 쓰던 폭을 먹는다</b> — 무엇을 시켰는지가 먼저 잘린다.
/// 그래서 머리글자 한 자만 적고 <b>나머지는 색이 말하게</b> 했다.
/// </para>
/// <para>
/// [<c>claude</c> 와 <c>copilot</c> 이 둘 다 「C」다 — 일부러 그렇다]
/// </para>
/// <para>
/// 머리글자만으로는 그 둘이 안 갈린다. 가르는 것은 <b>색</b>이다 — 각 AI 의
/// 심볼색을 그대로 배지 바탕에 쓴다(주황 Claude · 파랑 안티그래비티 ·
/// 먹빛 Copilot). 글자는 <b>「어느 자리에 무엇이 적히는 값인지」를 알리는
/// 표시</b>이지 값 자체가 아니다. 색을 못 보는 사람을 위해 배지에
/// <c>title</c> 로 전체 이름을 달아 준다 — 그 이름은 화면이 공통코드
/// (<c>AI_MODEL</c>)에서 읽은 것을 쓴다.
/// </para>
/// <para>
/// [색을 여기 적지 않는다]
/// </para>
/// <para>
/// 여기서는 <b>수식어 이름만</b> 돌려주고 실제 색은
/// <c>projmng.css</c> 의 <c>.pm-ask__ai--*</c> 가 쥔다. 어두운 테마에서
/// 먹빛을 뒤집어야 하는데(안 그러면 어두운 판에 검은 칩이라 사라진다)
/// 그것은 C# 이 모르는 일이다.
/// </para>
/// <para>
/// [모르는 값이 와도 배지는 뜬다]
/// </para>
/// <para>
/// 고를 수 있는 AI 는 공통코드라 <b>화면을 고치지 않고 늘어난다.</b> 새 값이
/// 오면 머리글자는 그 값의 첫 자를 대문자로 쓰고 색은 흐림으로 둔다 —
/// 여기 <c>switch</c> 에 없다고 <b>배지 자리가 비는 쪽이 더 나쁘다.</b>
/// </para>
/// </remarks>
internal static class AiRunnerMark
{
    /// <summary>배지에 적을 한 글자.</summary>
    public static string Letter(string? kind) => Key(kind) switch
    {
        "claude" => "C",
        "antigravity" => "A",
        "copilot" => "C",

        // 모르는 값. 첫 자를 대문자로 쓴다. 그마저 없으면 물음표를 적어
        // **「AI 를 모른다」는 사실 자체를 보이게** 한다.
        var other => other.Length > 0 ? char.ToUpperInvariant(other[0]).ToString() : "?",
    };

    /// <summary>
    /// 배지 색 수식어(<c>pm-ask__ai--*</c>). 아는 값만 제 색을 쓰고
    /// 나머지는 흐림으로 간다.
    /// </summary>
    public static string Tone(string? kind) => Key(kind) switch
    {
        "claude" or "antigravity" or "copilot" => Key(kind),
        _ => "etc",
    };

    /// <summary>견주기 좋게 다듬은 값. 공통코드의 코드값이 그대로 온다.</summary>
    private static string Key(string? kind) => (kind ?? string.Empty).Trim().ToLowerInvariant();
}
