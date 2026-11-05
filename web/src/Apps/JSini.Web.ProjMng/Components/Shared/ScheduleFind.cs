using JSini.Web.Components.Data;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>일정 찾기에서 낱말이 걸린 칸. 찾은 줄마다 어디서 걸렸는지 붙여 보인다.</summary>
[Flags]
public enum FindField
{
    None = 0,
    Title = 1,
    Memo = 2,
    Original = 4,
    Kind = 8,
    Assignee = 16,
    Requester = 32,
    All = Title | Memo | Original | Kind | Assignee | Requester,
}

/// <summary>일정 찾기 조건. 칸별 조건끼리는 모두 맞아야 하고(그리고), 검색어는 고른 칸 중 어디든 걸리면 된다.</summary>
public sealed class ScheduleFindQuery
{
    /// <summary>검색어. 띄어 쓴 낱말은 모두 들어 있어야 한다 — 낱말마다 다른 칸에 있어도 된다.</summary>
    public string? Keyword { get; set; }

    /// <summary>검색어를 볼 칸. 아무것도 안 고르면 전부 본다.</summary>
    public FindField Targets { get; set; } = FindField.All;

    public string? Title { get; set; }

    public string? Memo { get; set; }

    public string? Original { get; set; }

    public string? Kind { get; set; }

    /// <summary>담당자. 아이디와 이름 어느 쪽으로도 찾는다 — 칸에 이름과 아이디가 섞여 들어 있다.</summary>
    public string? Assignee { get; set; }

    public string? Requester { get; set; }

    /// <summary>기간을 무엇에 대어 볼지(<see cref="ScheduleFind.Bases"/>).</summary>
    public string Basis { get; set; } = ScheduleFind.Overlap;

    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }

    /// <summary>칸별 조건이 하나라도 있나. 상세 조건을 접어 두었을 때 「걸려 있음」을 알린다.</summary>
    public bool HasFieldConditions =>
        !string.IsNullOrWhiteSpace(Title) || !string.IsNullOrWhiteSpace(Memo)
        || !string.IsNullOrWhiteSpace(Original) || !string.IsNullOrWhiteSpace(Kind)
        || !string.IsNullOrWhiteSpace(Assignee) || !string.IsNullOrWhiteSpace(Requester);
}

/// <summary>일정 찾기의 맞춤 계산. 화면 없이 테스트하려고 페이지에서 떼어 냈다.</summary>
public static class ScheduleFind
{
    public const string Overlap = "overlap";
    public const string Start = "start";
    public const string End = "end";

    /// <summary>기간 기준. 서버는 늘 「걸치는 것」으로 받아 오고, 시작·종료 기준은 그 안에서 다시 거른다.</summary>
    public static readonly SchOption[] Bases =
    [
        new(Overlap, "기간에 걸침"),
        new(Start, "시작일 기준"),
        new(End, "종료일 기준"),
    ];

    /// <summary>칸 이름. 찾은 줄의 꼬리표와 검색 대상 체크가 같은 글자를 쓴다.</summary>
    public static readonly (FindField Field, string Name)[] Fields =
    [
        (FindField.Title, "제목"),
        (FindField.Memo, "메모"),
        (FindField.Original, "원본"),
        (FindField.Kind, "종류"),
        (FindField.Assignee, "담당자"),
        (FindField.Requester, "요청자"),
    ];

    public static string NameOf(FindField field) =>
        Array.Find(Fields, f => f.Field == field).Name ?? field.ToString();

    /// <summary>
    /// 조건에 맞으면 걸린 칸을, 안 맞으면 null 을 돌려준다. 조건이 하나도 없으면 <see cref="FindField.None"/> 으로 맞다.
    /// <paramref name="displayName"/> 은 아이디를 이름으로 바꾼다 — 담당자·요청자를 이름으로 찾게.
    /// </summary>
    public static FindField? Match(WbsItemDto w, ScheduleFindQuery q, Func<string?, string?>? displayName = null)
    {
        if (!InPeriod(w, q))
        {
            return null;
        }

        string?[] Texts(FindField f) => f switch
        {
            FindField.Title => [w.ProcNm],
            FindField.Memo => [w.Comm],
            FindField.Original => [w.BodyText],
            FindField.Kind => [w.ProcTp],
            FindField.Assignee => [w.DevUser, displayName?.Invoke(w.DevUser)],
            FindField.Requester => [w.ReqUser, displayName?.Invoke(w.ReqUser)],
            _ => [],
        };

        bool Has(FindField f, string word) =>
            Texts(f).Any(s => s?.Contains(word, StringComparison.OrdinalIgnoreCase) == true);

        var hit = FindField.None;

        foreach (var (field, cond) in new[]
                 {
                     (FindField.Title, q.Title), (FindField.Memo, q.Memo), (FindField.Original, q.Original),
                     (FindField.Kind, q.Kind), (FindField.Assignee, q.Assignee), (FindField.Requester, q.Requester),
                 })
        {
            var words = Words(cond);
            if (words.Length == 0)
            {
                continue;
            }

            if (!words.All(word => Has(field, word)))
            {
                return null;
            }

            hit |= field;
        }

        var targets = q.Targets == FindField.None ? FindField.All : q.Targets;
        foreach (var word in Words(q.Keyword))
        {
            var found = Fields.Select(f => f.Field)
                .Where(f => targets.HasFlag(f) && Has(f, word))
                .Aggregate(FindField.None, (a, f) => a | f);

            if (found == FindField.None)
            {
                return null;
            }

            hit |= found;
        }

        return hit;
    }

    /// <summary>
    /// 기간 기준에 맞나. 기간이 없는 일정은 기간 조건이 있으면 빠진다 — 서버도 그렇게 거른다.
    /// 종료가 비면 시작 하루짜리로, 시작이 비면 종료 하루짜리로 본다(달력이 그리는 대로).
    /// </summary>
    public static bool InPeriod(WbsItemDto w, ScheduleFindQuery q)
    {
        if (q.From is null && q.To is null)
        {
            return true;
        }

        var (from, to) = q.From is { } a && q.To is { } b && a > b ? (q.To, q.From) : (q.From, q.To);

        var startAt = w.PlanSdt ?? w.PlanEdt;
        var endAt = w.PlanEdt ?? w.PlanSdt;
        if (startAt is null || endAt is null)
        {
            return false;
        }

        var start = DateOnly.FromDateTime(startAt.Value);
        var end = DateOnly.FromDateTime(endAt.Value);

        bool Within(DateOnly d) => (from is null || d >= from) && (to is null || d <= to);

        return q.Basis switch
        {
            Start => Within(start),
            End => Within(end),
            _ => (from is null || end >= from) && (to is null || start <= to),
        };
    }

    /// <summary>찾은 줄들에서 칸마다 몇 건이 걸렸나. 결과 머리에 「제목 3 · 메모 5」로 적는다.</summary>
    public static IReadOnlyList<(string Name, int Count)> CountByField(IEnumerable<FindField> hits)
    {
        var list = hits.ToList();
        return [.. Fields
            .Select(f => (f.Name, Count: list.Count(h => h.HasFlag(f.Field))))
            .Where(x => x.Count > 0)];
    }

    private static string[] Words(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? []
            : text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
