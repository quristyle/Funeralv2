using System.Data;
using System.Text.Json;
using Dapper;
using Npgsql;
using ProjMngServer.Models;
using static ProjMngServer.Services.WbsBoardSql;

namespace ProjMngServer.Services;

/// <summary>상세 목록의 대상(원장 한 줄) 추가·수정·ID 지정·삭제 — <c>projmng.wbs_work</c>.</summary>
/// 원장의 키가 (prj_rid, activity_id) 라 ID 가 아직 없는 줄은 NOID- 임시 번호로 세운다. 일감은 외래키의 ON UPDATE CASCADE 로 ID 를 따라간다.
public sealed class WbsBoardRowService(IConfiguration configuration)
{
    private readonly string _connectionString =
        configuration.GetConnectionString("jsini")
        ?? throw new InvalidOperationException("연결 문자열 'jsini' 가 없습니다.");

    private NpgsqlConnection Open() => new(_connectionString);

    /// <summary>한 줄의 모든 칸. 목록에는 긴 글(화면 설명·메모)이 안 실리므로 수정 창이 따로 읽는다.</summary>
    public async Task<List<WbsBoardField>?> OneAsync(int prjRid, string activityId)
    {
        using var db = Open();

        var cols = string.Join(", ", FormColumns.Select(c => $"{c}::text as {c}"));
        var row = await db.QuerySingleOrDefaultAsync($"""
            select activity_id, {cols}
              from projmng.wbs_work
             where prj_rid = @prjRid and activity_id = @activityId
            """, new { prjRid, activityId });

        if (row is null) return null;

        // 칸 이름 · 값 짝의 목록으로 준다. 응답 봉투가 사전을 그런 모양으로 굳히므로 처음부터 그 모양이다.
        return [.. ((IDictionary<string, object?>)row).Select(kv => new WbsBoardField { Key = kv.Key, Value = kv.Value as string })];
    }

    /// <summary>새 대상. ID 를 비우면 임시 번호를 붙인다.</summary>
    /// <returns>만든 줄의 Activity ID. 겹치면 <see cref="WbsRowConflict"/> 를 던진다.</returns>
    public async Task<string> CreateAsync(int prjRid, string? activityId, IDictionary<string, JsonElement> body)
    {
        var values = Clean(body);
        var sys = Nz(values.GetValueOrDefault("systemcode") as string);
        var menu = Nz(values.GetValueOrDefault("menu_nm") as string);

        if (sys is null || menu is null) throw new WbsRowConflict("모듈 코드와 메뉴명은 비울 수 없습니다.");

        await using var db = Open();
        await db.OpenAsync();
        await using var tx = await db.BeginTransactionAsync();

        await EnsureNameFreeAsync(db, prjRid, sys, menu, null);

        var id = Nz(activityId)?.Trim();
        if (id is not null)
        {
            if (id.StartsWith(NoIdPrefix, StringComparison.OrdinalIgnoreCase))
                throw new WbsRowConflict($"{NoIdPrefix} 로 시작하는 번호는 임시 번호 자리라 쓸 수 없습니다.");

            await EnsureIdFreeAsync(db, prjRid, id);
        }
        else
        {
            id = await db.ExecuteScalarAsync<string>($"""
                select '{NoIdPrefix}' || lpad((coalesce(max(substr(activity_id, {NoIdPrefix.Length + 1})::int), 0) + 1)::text, 4, '0')
                  from projmng.wbs_work
                 where prj_rid = @prjRid and activity_id ~ '^{NoIdPrefix}[0-9]+$'
                """, new { prjRid }, tx);
        }

        var cols = values.Keys.ToList();
        var args = new DynamicParameters();
        args.Add("prjRid", prjRid);
        args.Add("activityId", id);

        var names = new List<string>();
        for (var i = 0; i < cols.Count; i++)
        {
            args.Add($"p{i}", values[cols[i]]);
            names.Add($"@p{i}::{(DateColumns.Contains(cols[i]) ? "date" : "text")}");
        }

        await db.ExecuteAsync($"""
            insert into projmng.wbs_work (prj_rid, activity_id{string.Concat(cols.Select(c => ", " + c))})
            values (@prjRid, @activityId{string.Concat(names.Select(n => ", " + n))})
            """, args, tx);

        await tx.CommitAsync();
        return id!;
    }

    /// <summary>모든 칸 수정. 보낸 칸만 고친다.</summary>
    /// <returns>고친 줄 수. 0 이면 없는 줄.</returns>
    public async Task<int> UpdateAsync(int prjRid, string activityId, IDictionary<string, JsonElement> body)
    {
        var values = Clean(body);
        if (values.Count == 0) return 0;

        await using var db = Open();
        await db.OpenAsync();

        var found = await db.QuerySingleOrDefaultAsync<(string? Sys, string? Menu)>("""
            select systemcode, menu_nm from projmng.wbs_work
             where prj_rid = @prjRid and activity_id = @activityId
            """, new { prjRid, activityId });

        if (found == default) return 0;

        var sys = values.TryGetValue("systemcode", out var s) ? Nz(s as string) : found.Sys;
        var menu = values.TryGetValue("menu_nm", out var m) ? Nz(m as string) : found.Menu;

        if (sys is null || menu is null) throw new WbsRowConflict("모듈 코드와 메뉴명은 비울 수 없습니다.");

        if (sys != found.Sys || menu != found.Menu)
            await EnsureNameFreeAsync(db, prjRid, sys, menu, activityId);

        var args = new DynamicParameters();
        args.Add("prjRid", prjRid);
        args.Add("activityId", activityId);

        var sets = new List<string>();
        var i = 0;
        foreach (var (col, val) in values)
        {
            args.Add($"p{i}", val);
            sets.Add($"{col} = @p{i}::{(DateColumns.Contains(col) ? "date" : "text")}");
            i++;
        }

        return await db.ExecuteAsync($"""
            update projmng.wbs_work set {string.Join(", ", sets)}
             where prj_rid = @prjRid and activity_id = @activityId
            """, args);
    }

    /// <summary>임시 번호(NOID-)로 세운 줄에 확정 ID 를 채운다. 일감은 외래키가 데려간다.</summary>
    public async Task<int> SetIdAsync(int prjRid, string activityId, string newId)
    {
        newId = newId.Trim();

        if (!activityId.StartsWith(NoIdPrefix, StringComparison.OrdinalIgnoreCase))
            throw new WbsRowConflict("이미 Activity ID 가 정해진 줄입니다.");

        if (newId.StartsWith(NoIdPrefix, StringComparison.OrdinalIgnoreCase))
            throw new WbsRowConflict($"{NoIdPrefix} 는 임시 번호 자리라 쓸 수 없습니다.");

        await using var db = Open();
        await db.OpenAsync();
        await using var tx = await db.BeginTransactionAsync();

        await EnsureIdFreeAsync(db, prjRid, newId);

        var n = await db.ExecuteAsync("""
            update projmng.wbs_work set activity_id = @newId
             where prj_rid = @prjRid and activity_id = @activityId
            """, new { prjRid, activityId, newId }, tx);

        await tx.CommitAsync();
        return n;
    }

    /// <summary>지우기 전에 함께 사라질 것을 센다.</summary>
    public async Task<WbsBoardRowDeps?> DepsAsync(int prjRid, string activityId)
    {
        using var db = Open();

        return await db.QuerySingleOrDefaultAsync<WbsBoardRowDeps>("""
            select w.activity_id as ActivityId
                 , w.systemcode  as Systemcode
                 , w.menu_nm     as MenuNm
                 , w.file_cnt    as Files
                 , (select count(*)::int from projmng.wbs_task t
                     where t.prj_rid = w.prj_rid and t.activity_id = w.activity_id) as Tasks
              from projmng.wbs_work w
             where w.prj_rid = @prjRid and w.activity_id = @activityId
            """, new { prjRid, activityId });
    }

    /// <summary>줄을 지운다. 줄과 일감은 <c>wbs_work_deleted</c> 에 먼저 담는다 — 되돌릴 자료가 거기 남는다.</summary>
    /// <returns>담아 둔 보관 번호와 함께 지운 일감 수. 없는 줄이면 null.</returns>
    public async Task<WbsBoardRowDeleted?> DeleteAsync(int prjRid, string activityId, string? who)
    {
        await using var db = Open();
        await db.OpenAsync();
        await using var tx = await db.BeginTransactionAsync();

        var kept = await db.QuerySingleOrDefaultAsync<WbsBoardRowDeleted>("""
            insert into projmng.wbs_work_deleted (prj_rid, activity_id, row_data, tasks, task_cnt, deleted_by)
            select w.prj_rid, w.activity_id, to_jsonb(w)
                 , (select jsonb_agg(to_jsonb(t) order by t.task_id) from projmng.wbs_task t
                     where t.prj_rid = w.prj_rid and t.activity_id = w.activity_id)
                 , (select count(*)::int from projmng.wbs_task t
                     where t.prj_rid = w.prj_rid and t.activity_id = w.activity_id)
                 , @who
              from projmng.wbs_work w
             where w.prj_rid = @prjRid and w.activity_id = @activityId
            returning del_id as DelId, activity_id as ActivityId, task_cnt as Tasks
            """, new { prjRid, activityId, who }, tx);

        if (kept is null) return null;

        // 일감은 외래키가 함께 지운다(ON DELETE CASCADE).
        await db.ExecuteAsync("""
            delete from projmng.wbs_work where prj_rid = @prjRid and activity_id = @activityId
            """, new { prjRid, activityId }, tx);

        await tx.CommitAsync();
        return kept;
    }

    /// <summary>첨부 그룹과 건수를 적는다. 파일은 FileServer 가 들고, 원장은 목록이 줄마다 묻지 않게 건수만 든다.</summary>
    public async Task<int> SetFilesAsync(int prjRid, string activityId, string? groupId, int count)
    {
        using var db = Open();

        return await db.ExecuteAsync("""
            update projmng.wbs_work
               set file_group_id = coalesce(@groupId, file_group_id), file_cnt = @count
             where prj_rid = @prjRid and activity_id = @activityId
            """, new { prjRid, activityId, groupId = Nz(groupId), count = Math.Max(0, count) });
    }

    // ──────────────────────────────────────────────────────── 거르기

    /// <summary>창이 보낸 값 가운데 원장 칸만 남긴다. 빈 글자는 null 이다 — 표시 칸이 'o' 아니면 비어 있는 식이라 빈 글자가 섞이면 「아무것도 아닌 값」이 둘이 된다.</summary>
    private static Dictionary<string, object?> Clean(IDictionary<string, JsonElement> body)
    {
        var map = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, el) in body)
        {
            var col = FormColumns.FirstOrDefault(c => c.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (col is null) continue;

            var val = JsonValue(el) switch
            {
                string s when string.IsNullOrWhiteSpace(s) => null,
                string s => s.Trim(),
                bool b => b ? "o" : null,
                null => null,
                var v => Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture),
            };

            map[col] = val;
        }

        return map;
    }

    private static async Task EnsureNameFreeAsync(
        NpgsqlConnection db, int prjRid, string sys, string menu, string? except)
    {
        var clash = await db.ExecuteScalarAsync<string?>("""
            select activity_id from projmng.wbs_work
             where prj_rid = @prjRid and systemcode = @sys and menu_nm = @menu
               and (@except::text is null or activity_id <> @except)
             limit 1
            """, new { prjRid, sys, menu, except });

        if (clash is not null)
            throw new WbsRowConflict($"같은 모듈에 같은 메뉴명이 이미 있습니다 — {sys} · {menu} ({clash})");
    }

    private static async Task EnsureIdFreeAsync(NpgsqlConnection db, int prjRid, string id)
    {
        var dup = await db.ExecuteScalarAsync<string?>("""
            select activity_id from projmng.wbs_work
             where prj_rid = @prjRid and upper(activity_id) = upper(@id)
             limit 1
            """, new { prjRid, id });

        if (dup is not null) throw new WbsRowConflict($"이미 있는 Activity ID 입니다 — {dup}");
    }
}

/// <summary>원장 규칙(키·이름 중복)에 걸렸다. 컨트롤러가 409 로 옮긴다.</summary>
public sealed class WbsRowConflict(string message) : Exception(message);
