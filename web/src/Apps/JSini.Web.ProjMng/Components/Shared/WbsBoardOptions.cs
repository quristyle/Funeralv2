namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>
/// WBS 대시보드 화면들이 나눠 쓰는 고르개 항목.
/// </summary>
/// <remarks>
/// <para>
/// 원본에서는 이 셋이 <b>사이드바 아래에 상주</b>했다 — 어느 화면으로 옮겨도
/// 고른 값이 그대로 따라갔다. 포털의 사이드바는 메뉴 나무라 그 자리가 없으므로
/// 화면마다 조건줄에 둔다.
/// </para>
///
/// <para>
/// <b>글자를 한 곳에 모아 둔다.</b> 화면이 일곱인데 「계획종료일」을 각자 적으면
/// 어느 화면은 「종료일」이 되고, 같은 값을 고르는 자리마다 이름이 달라진다.
/// </para>
/// </remarks>
public static class WbsBoardOptions
{
    /// <summary>
    /// 집계 기준 날짜. 기본이 계획종료일인 것은 대시보드가 <b>「언제까지」</b>를
    /// 묻는 물건이어서다.
    /// </summary>
    public static readonly object[] Basis =
    [
        new { Text = "계획종료일", Value = "edt" },
        new { Text = "계획시작일", Value = "sdt" },
    ];

    /// <summary>
    /// 조회 범위.
    /// </summary>
    /// <remarks>
    /// 사내에서는 전 건이 개발 대상이라 둘의 결과가 같았고, 그래서 원본은 이
    /// 고르개를 화면에서 걷어냈다(서버 조건은 남겨 두었다). 여기서는 다시
    /// 보인다 — 프로젝트가 여럿이 되면 대상 아닌 줄이 생긴다.
    /// </remarks>
    public static readonly object[] Scope =
    [
        new { Text = "개발 대상", Value = "dev" },
        new { Text = "전체", Value = "all" },
    ];

    /// <summary>
    /// 사람을 가르는 잣대. 담당자(계획)와 개발자(실제)가 다른 줄이 있다.
    /// </summary>
    public static readonly object[] Who =
    [
        new { Text = "담당자", Value = "plan" },
        new { Text = "개발자", Value = "real" },
    ];

    /// <summary>비율 글자. 분모가 0 이면 <c>0%</c> 가 아니라 <b>줄표</b>다.</summary>
    public static string Percent(int part, int whole) =>
        whole == 0 ? "—" : $"{part * 100.0 / whole:0.#}%";

    /// <inheritdoc cref="Percent(int, int)"/>
    public static string Percent(decimal? rate) =>
        rate is null ? "—" : $"{rate.Value:0.#}%";
}
