using JSini.Web.Components.Data;

namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>WBS 대시보드 화면들이 나눠 쓰는 고르개 항목. 글자를 한 곳에 모아 둔다 — 화면마다 적으면 같은 값의 이름이 화면마다 달라진다.</summary>
public static class WbsBoardOptions
{
    /// <summary>집계 기준 날짜. 기본이 계획종료일인 것은 대시보드가 「언제까지」를 묻는 물건이어서다.</summary>
    public static readonly SchOption[] Basis =
    [
        new("edt", "계획종료일"),
        new("sdt", "계획시작일"),
    ];

    /// <summary>조회 범위. 전 건이 개발 대상이면 둘의 결과가 같아 원본은 걷어냈다 — 프로젝트가 여럿이 되면 대상 아닌 줄이 생기므로 다시 보인다.</summary>
    public static readonly SchOption[] Scope =
    [
        new("dev", "개발 대상"),
        new("all", "전체"),
    ];

    /// <summary>사람을 가르는 잣대. 담당자(계획)와 개발자(실제)가 다른 줄이 있다.</summary>
    public static readonly SchOption[] Who =
    [
        new("plan", "담당자"),
        new("real", "개발자"),
    ];

    /// <summary>고른 값의 이름. 목록에 없는 값은 적힌 그대로 돌려준다 — 「전체」로 덮으면 값이 틀린 것인지 안 고른 것인지 가려낼 수 없다.</summary>
    public static string TextOf(SchOption[] options, string? value) =>
        options.FirstOrDefault(o => o.Value == value)?.Text ?? value ?? string.Empty;

    /// <summary>비율 글자. 분모가 0 이면 0% 가 아니라 줄표다.</summary>
    public static string Percent(int part, int whole) =>
        whole == 0 ? "—" : $"{part * 100.0 / whole:0.#}%";

    /// <inheritdoc cref="Percent(int, int)"/>
    public static string Percent(decimal? rate) =>
        rate is null ? "—" : $"{rate.Value:0.#}%";
}
