using System.Globalization;

namespace JSini.Web.Components.Data;

/// <summary>
/// 접힌 조회줄에 적을 글자를 만든다(<c>CommSch.MobileSummary</c>).
///
/// <para>
/// [화면마다 손으로 이으면 갈라진다]
/// </para>
///
/// <para>
/// 휴대폰에서 조회 판이 접히는 화면이 여든이 넘는다. 그 머리줄은 모두
/// 「전체 · 개발팀 · 9 월」 같은 한 줄인데, 이것을 화면마다
/// <c>string.Join</c> 으로 적으면 반드시 갈라진다 — 어떤 화면은 쉼표로 잇고,
/// 어떤 화면은 고르지 않은 칸을 「없음」이라 부르고, 어떤 화면은 빈 칸을
/// 안 거르고 이어 「 ·  · 9 월」 이 된다.
/// </para>
///
/// <para>
/// 값을 <b>화면이 만드는 것</b>은 그대로다(그 까닭은 <c>CommSch</c> 머리말에
/// 있다). 여기 있는 것은 <b>잇는 방법</b>뿐이다.
/// </para>
/// </summary>
public static class SchSummary
{
    /// <summary>고르지 않은 칸을 부르는 이름. 「없음」이 아니라 「전체」다 —
    /// 안 고르면 다 보이기 때문이다.</summary>
    public const string Any = "전체";

    /// <summary>
    /// 조각들을 가운뎃점으로 잇는다. <b>빈 조각은 버린다</b> — 안 고른 칸이
    /// 자리만 차지하면 정작 고른 것이 잘려 나간다(머리줄은 한 줄로 자른다).
    /// </summary>
    public static string Of(params string?[] parts) =>
        string.Join(" · ", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));

    /// <summary>
    /// 값이 비었으면 <paramref name="empty"/>(기본은 <see cref="Any"/>)를 쓴다.
    /// </summary>
    public static string Or(string? value, string empty = Any) =>
        string.IsNullOrWhiteSpace(value) ? empty : value.Trim();

    /// <summary>
    /// 켠 스위치만 적는다. 끈 것은 <c>null</c> 이라 <see cref="Of"/> 에서
    /// 사라진다 — 「안 읽은 것만」은 켰을 때만 뜻이 있는 말이다.
    /// </summary>
    public static string? On(bool value, string text) => value ? text : null;

    /// <summary>
    /// 기간. 한쪽만 있으면 그쪽만 적는다. 올해면 해를 뗀다 —
    /// 머리줄은 한 줄이라 넉 자가 아깝다.
    /// </summary>
    public static string? Period(DateTime? from, DateTime? to)
    {
        if (from is null && to is null)
        {
            return null;
        }

        if (from is null)
        {
            return $"~ {Day(to!.Value)}";
        }

        if (to is null)
        {
            return $"{Day(from.Value)} ~";
        }

        return $"{Day(from.Value)} ~ {Day(to.Value)}";
    }

    /// <summary>
    /// 날짜 하나. 올해면 해를 뗀다 — 머리줄은 한 줄이라 넉 자가 아깝다.
    /// 값이 없으면 <c>null</c> 이라 <see cref="Of"/> 에서 사라진다.
    /// </summary>
    public static string? Day(DateTime? day) => day is null ? null : Day(day.Value);

    /// <inheritdoc cref="Day(DateTime?)"/>
    public static string Day(DateTime day) =>
        day.Year == DateTime.Today.Year
            ? day.ToString("M.d", CultureInfo.InvariantCulture)
            : day.ToString("yyyy.M.d", CultureInfo.InvariantCulture);

    /// <summary>
    /// 목록에서 값에 맞는 줄의 이름을 찾는다. 못 찾으면
    /// <paramref name="empty"/> — 고르개가 아직 안 읽혔을 때도 여기로 온다.
    /// </summary>
    public static string NameOf<T>(
        IEnumerable<T>? source,
        Func<T, string?> value,
        Func<T, string?> name,
        string? picked,
        string empty = Any)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(picked))
        {
            return empty;
        }

        var hit = source?.FirstOrDefault(x => string.Equals(value(x), picked, StringComparison.Ordinal));
        return hit is null ? empty : Or(name(hit), empty);
    }
}
