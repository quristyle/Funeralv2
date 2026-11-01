using Microsoft.AspNetCore.Components;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Shared;

public partial class WbsHeatMatrix
{
    [Parameter] public IReadOnlyList<WbsBoardUserBucketDto> Rows { get; set; } = [];

    /// <summary>주 단위면 참 — 머리글을 「W33」 + 그 주 마지막 날(월.일)로 줄여 적는다.</summary>
    [Parameter] public bool Weekly { get; set; }

    /// <summary>바닥 줄 이름(「월 합계」·「주 합계」).</summary>
    [Parameter] public string ColHead { get; set; } = "월";

    [Parameter] public EventCallback<(string User, string Bucket)> OnPick { get; set; }

    private IReadOnlyList<UserKey> _users = [];
    private IReadOnlyList<string> _buckets = [];
    private Dictionary<(string, string), (int Cnt, int Done)> _cells = [];
    private Dictionary<string, WbsBoardUserBucketDto> _heads = [];
    private int _peak = 1;
    private int _grand;
    private int _grandDone;

    private IReadOnlyList<WbsBoardUserBucketDto>? _seen;

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(_seen, Rows)) return;
        _seen = Rows;

        var rows = Rows.Where(r => r.Bucket is not null).ToList();

        _buckets = [.. rows.Select(r => r.Bucket!).Distinct().Order(StringComparer.Ordinal)];

        // 성명순. 계정과 안 이어진 사람은 이름 자리에 아이디가 그대로 온다.
        _users =
        [
            .. rows.GroupBy(r => r.UserBpId ?? "")
                   .Select(g => new UserKey(g.Key, g.First().UserNm ?? g.Key))
                   .OrderBy(u => u.Name, StringComparer.Create(new System.Globalization.CultureInfo("ko-KR"), false))
                   .ThenBy(u => u.Id, StringComparer.Ordinal)
        ];

        _cells = rows.GroupBy(r => (r.UserBpId ?? "", r.Bucket!))
                     .ToDictionary(g => g.Key, g => (g.Sum(x => x.Cnt), g.Sum(x => x.Done)));

        _heads = rows.GroupBy(r => r.Bucket!).ToDictionary(g => g.Key, g => g.First());

        _peak = Math.Max(1, _cells.Values.Select(v => v.Cnt).DefaultIfEmpty(0).Max());
        _grand = rows.Sum(r => r.Cnt);
        _grandDone = rows.Sum(r => r.Done);
    }

    private (int Cnt, int Done) Cell(string user, string bucket) =>
        _cells.TryGetValue((user, bucket), out var v) ? v : (0, 0);

    private int RowCnt(string user) => _buckets.Sum(b => Cell(user, b).Cnt);
    private int RowDone(string user) => _buckets.Sum(b => Cell(user, b).Done);
    private int ColCnt(string bucket) => _users.Sum(u => Cell(u.Id, bucket).Cnt);
    private int ColDone(string bucket) => _users.Sum(u => Cell(u.Id, bucket).Done);

    private string HeadText(string bucket) =>
        Weekly && bucket.IndexOf('-') is var i and >= 0 ? bucket[(i + 1)..] : bucket;

    private string? HeadSub(string bucket) =>
        Weekly && _heads.TryGetValue(bucket, out var h) && h.WeekEnd is { Length: >= 10 } end
            ? end[5..].Replace('-', '.')
            : null;

    private string HeadTitle(string bucket) =>
        Weekly && _heads.TryGetValue(bucket, out var h)
            ? $"{bucket} ({h.WeekStart} ~ {h.WeekEnd})"
            : bucket;

    /// <summary>가장 많은 칸을 기준으로 짙기를 정한다. 바닥 8% 는 한 건짜리 칸이 빈칸과 구분되게 하려는 것이다.</summary>
    private string? Shade(int cnt)
    {
        if (cnt == 0) return null;

        var ratio = (0.08 + (double)cnt / _peak * 0.62) * 100;
        var strong = (double)cnt / _peak > 0.55;

        return $"background: color-mix(in srgb, var(--jsini-accent) {ratio:0}%, transparent);"
               + (strong ? " color: #fff;" : null);
    }

    private Task PickAsync(string user, string bucket, int cnt) =>
        cnt == 0 ? Task.CompletedTask : OnPick.InvokeAsync((user, bucket));

    private sealed record UserKey(string Id, string Name);
}
