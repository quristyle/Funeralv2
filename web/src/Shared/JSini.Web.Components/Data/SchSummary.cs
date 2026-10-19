using System.Globalization;

namespace JSini.Web.Components.Data;

/// <summary>접힌 조회줄에 적을 글자를 만든다(CommSch.MobileSummary). 화면마다 손으로 이으면 쉼표·「없음」·빈 칸 처리가 갈라지므로 잇는 방법을 여기 한 곳에 둔다. 값을 만드는 것은 화면이다.</summary>
public static class SchSummary
{
    /// <summary>고르지 않은 칸을 부르는 이름. 「없음」이 아니라 「전체」다 — 안 고르면 다 보이기 때문이다.</summary>
    public const string Any = "전체";

    /// <summary>조각들을 가운뎃점으로 잇는다. 빈 조각은 버린다 — 안 고른 칸이 자리를 차지하면 고른 것이 잘린다.</summary>
    public static string Of(params string?[] parts) =>
        string.Join(" · ", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));

    /// <summary>값이 비었으면 empty(기본은 Any)를 쓴다.</summary>
    public static string Or(string? value, string empty = Any) =>
        string.IsNullOrWhiteSpace(value) ? empty : value.Trim();

    /// <summary>켠 스위치만 적는다. 끈 것은 null 이라 Of 에서 사라진다 — 「안 읽은 것만」은 켰을 때만 뜻이 있다.</summary>
    public static string? On(bool value, string text) => value ? text : null;

    /// <summary>기간. 한쪽만 있으면 그쪽만 적는다. 올해면 해를 뗀다 — 머리줄은 한 줄이라 넉 자가 아깝다.</summary>
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

    /// <summary>날짜 하나. 올해면 해를 뗀다. 값이 없으면 null 이라 Of 에서 사라진다.</summary>
    public static string? Day(DateTime? day) => day is null ? null : Day(day.Value);

    /// <inheritdoc cref="Day(DateTime?)"/>
    public static string Day(DateTime day) =>
        day.Year == AppTime.Today.Year
            ? day.ToString("M.d", CultureInfo.InvariantCulture)
            : day.ToString("yyyy.M.d", CultureInfo.InvariantCulture);

    /// <summary>목록에서 값에 맞는 줄의 이름을 찾는다. 못 찾으면 empty — 고르개가 아직 안 읽혔을 때도 여기로 온다.</summary>
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
