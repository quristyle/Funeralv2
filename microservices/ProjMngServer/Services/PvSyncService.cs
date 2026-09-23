using System.Data;
using Dapper;
using Npgsql;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>
/// ProjectView 의 날짜를 원장에 반영한다 — <b>읽기 방향 하나뿐</b>이다.
/// </summary>
/// <remarks>
/// <para>
/// ProjectView 는 조회만 하고 고치지 않는다. 날짜의 기준이 그쪽이라 이쪽이
/// 따라가는 것이고, 그래서 <see cref="PvSyncRequest.ClearEmpty"/> 의 기본이
/// 참이다 — 그쪽이 비었으면 이쪽도 비운다.
/// </para>
///
/// <para>
/// [원본의 엑셀 경로는 옮기지 않았다]
/// </para>
///
/// <para>
/// 사내 대시보드는 값을 받는 길이 둘이었다 — ① ProjectView 의 Excel Export
/// 파일 ② ProjectView 콘솔에서 걷어 붙여넣은 JSON. <b>①은 옮길 수 없다.</b>
/// 그 엑셀은 HHI DRM 으로 암호화돼 있어 프로그램이 직접 못 열고, 원본은
/// Windows 의 Excel COM(<c>tools/export2csv.ps1</c>)으로 CSV 를 만든 뒤
/// <c>%USERPROFILE%\Downloads</c> 를 뒤졌다. 포털은 리눅스 컨테이너에서 돌고
/// 브라우저의 내려받기 폴더 같은 것이 없다.
/// </para>
///
/// <para>
/// ②만 남겼고, 그것이 [워크플로 채우기]·[수집]이 이미 쓰던 길이라 화면에서
/// 달라지는 것은 「파일로 읽기」 단추가 없다는 것뿐이다.
/// </para>
/// </remarks>
public sealed class PvSyncService(IConfiguration configuration)
{
    private readonly string _connectionString =
        configuration.GetConnectionString("jsini")
        ?? throw new InvalidOperationException("연결 문자열 'jsini' 가 없습니다.");

    private IDbConnection Open() => new NpgsqlConnection(_connectionString);

    /// <summary>ProjectView 의 값 → 원장의 칸.</summary>
    private static readonly (string Pv, string Col, string Label)[] MapCols =
    [
        ("planStartDate", "plan_sdt", "시작일"),
        ("planEndDate", "plan_edt", "종료일"),
        ("actualStartDate", "plan_sdt_c", "실적시작"),
        ("actualEndDate", "plan_edt_c", "실적종료"),
    ];

    /// <summary>원장에 지금 들어 있는 값.</summary>
    private sealed class Current
    {
        public string ActivityId { get; set; } = "";
        public string? Systemcode { get; set; }
        public string? MenuNm { get; set; }
        public string? PlanSdt { get; set; }
        public string? PlanEdt { get; set; }
        public string? PlanSdtC { get; set; }
        public string? PlanEdtC { get; set; }

        public string? Get(string col) => col switch
        {
            "plan_sdt" => PlanSdt,
            "plan_edt" => PlanEdt,
            "plan_sdt_c" => PlanSdtC,
            _ => PlanEdtC,
        };
    }

    private async Task<Dictionary<string, Current>> LoadAsync(int prjRid)
    {
        using var db = Open();

        // 원장에 기본키가 생겨서 액티비티마다 한 줄이다. 원본은 기본키가 없어
        // `group by` 로 묶고 겹친 줄 수까지 세어 화면에 보여 주고 있었다.
        var rows = await db.QueryAsync<Current>("""
            select activity_id      as ActivityId
                 , systemcode       as Systemcode
                 , menu_nm          as MenuNm
                 , plan_sdt::text   as PlanSdt
                 , plan_edt::text   as PlanEdt
                 , plan_sdt_c::text as PlanSdtC
                 , plan_edt_c::text as PlanEdtC
              from projmng.wbs_work
             where prj_rid = @prjRid
            """, new { prjRid });

        var map = new Dictionary<string, Current>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in rows) map[r.ActivityId] = r;

        return map;
    }

    /// <summary>고칠 칸. 준 것이 없거나 모르는 이름뿐이면 넷 전부.</summary>
    private static List<string> Fields(IReadOnlyList<string> asked)
    {
        var cols = asked
            .Where(f => MapCols.Any(m => m.Col == f))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return cols.Count > 0 ? cols : [.. MapCols.Select(m => m.Col)];
    }

    /// <summary><c>2026-08-31T00:00:00</c> → <c>2026-08-31</c>.</summary>
    private static string? Day(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;

        s = s.Trim();
        if (s.Length > 10) s = s[..10];

        return DateOnly.TryParse(s, out var d) ? d.ToString("yyyy-MM-dd") : null;
    }

    private static string? Value(PvSyncItem item, string pv) => Day(pv switch
    {
        "planStartDate" => item.PlanStartDate,
        "planEndDate" => item.PlanEndDate,
        "actualStartDate" => item.ActualStartDate,
        _ => item.ActualEndDate,
    });

    /// <summary>
    /// 보내온 것과 원장을 견준다. <b>미리보기와 반영이 같은 계산을 쓴다</b> —
    /// 갈라 두면 「보여 준 것과 다른 것이 바뀐다」가 된다.
    /// </summary>
    private static (List<PvSyncDiffRow> Rows, List<string> Unknown) Diff(
        IReadOnlyList<PvSyncItem> items, List<string> cols, bool clearEmpty,
        Dictionary<string, Current> current)
    {
        var rows = new List<PvSyncDiffRow>();
        var unknown = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            var code = item.Code?.Trim();
            if (string.IsNullOrWhiteSpace(code)) continue;
            if (!seen.Add(code)) continue;

            if (!current.TryGetValue(code, out var cur))
            {
                unknown.Add(code);
                continue;
            }

            var changes = new List<PvSyncChange>();

            foreach (var (pv, col, label) in MapCols)
            {
                if (!cols.Contains(col, StringComparer.OrdinalIgnoreCase)) continue;

                var to = Value(item, pv);

                // 그쪽이 비었는데 비우지 않기로 했으면 건드리지 않는다.
                if (to is null && !clearEmpty) continue;

                var from = cur.Get(col);
                if (string.Equals(from, to, StringComparison.Ordinal)) continue;

                changes.Add(new PvSyncChange { Col = col, Label = label, From = from, To = to });
            }

            rows.Add(new PvSyncDiffRow
            {
                ActivityId = cur.ActivityId,
                Systemcode = cur.Systemcode,
                MenuNm = cur.MenuNm,
                Changes = changes,
            });
        }

        return (rows, unknown);
    }

    /// <summary>무엇이 바뀌는지 먼저 보여 준다. <b>DB 는 건드리지 않는다.</b></summary>
    public async Task<PvSyncPreview> PreviewAsync(int prjRid, PvSyncRequest request)
    {
        var cols = Fields(request.Fields);
        var current = await LoadAsync(prjRid);
        var (rows, unknown) = Diff(request.Items, cols, request.ClearEmpty, current);

        var sent = rows.Select(r => r.ActivityId!).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 원장에는 있는데 이번에 안 온 것. 「빠뜨린 것 아닌가」를 보여 주는 값이라
        // 오류가 아니다 — 범위를 좁혀 걷으면 늘 생긴다.
        var missing = current.Values
            .Where(r => !sent.Contains(r.ActivityId))
            .OrderBy(r => r.ActivityId, StringComparer.Ordinal)
            .Select(r => new PvSyncDiffRow { ActivityId = r.ActivityId, MenuNm = r.MenuNm })
            .ToList();

        return new PvSyncPreview
        {
            Fields = cols,
            ClearEmpty = request.ClearEmpty,
            Read = request.Items.Count,
            Matched = rows.Count,
            Changed = rows.Count(r => r.Changes.Count > 0),
            Same = rows.Count(r => r.Changes.Count == 0),
            UnknownCount = unknown.Count,
            Unknown = [.. unknown.Take(50)],
            MissingCount = missing.Count,
            Missing = [.. missing.Take(300)],
            Rows = [.. rows.Where(r => r.Changes.Count > 0)],
        };
    }

    /// <summary>실제로 고친다. <b>바뀌는 칸만</b> 건드린다.</summary>
    public async Task<PvSyncApplied> ApplyAsync(int prjRid, PvSyncRequest request)
    {
        var cols = Fields(request.Fields);
        var current = await LoadAsync(prjRid);
        var (rows, unknown) = Diff(request.Items, cols, request.ClearEmpty, current);

        using var db = Open();

        var applied = 0;
        var done = new List<PvSyncDiffRow>();

        foreach (var r in rows)
        {
            if (r.Changes.Count == 0) continue;

            var sets = new List<string>();
            var args = new DynamicParameters();
            var i = 0;

            foreach (var ch in r.Changes)
            {
                var name = $"p{i++}";
                args.Add(name, ch.To);
                sets.Add($"{ch.Col} = @{name}::date");
            }

            args.Add("prjRid", prjRid);
            args.Add("activityId", r.ActivityId);

            await db.ExecuteAsync($"""
                update projmng.wbs_work set {string.Join(", ", sets)}
                 where prj_rid = @prjRid and activity_id = @activityId
                """, args);

            applied++;
            done.Add(r);
        }

        return new PvSyncApplied
        {
            Matched = rows.Count,
            Applied = applied,
            UnknownCount = unknown.Count,
            Done = done,
        };
    }
}
