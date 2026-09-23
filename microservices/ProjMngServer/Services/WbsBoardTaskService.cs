using System.Data;
using Dapper;
using Npgsql;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>
/// 화면별 일감 — <c>projmng.wbs_task</c>. 원장 1 : N 일감이다.
/// </summary>
/// <remarks>
/// 옮겨 오면서 <b>부모를 외래키로 걸었다</b>. 원본은 원장에 기본키가 없어서 걸
/// 대상이 없었고, 그래서 서비스가 손으로 「그 액티비티가 있나」를 물어보고
/// 있었다. 그 확인은 남겨 둔다 — 외래키가 막아 주는 것은 같지만, 그때 나오는
/// 것은 <c>23503</c> 이라 화면이 사람에게 보여 줄 말이 못 된다.
/// </remarks>
public sealed class WbsBoardTaskService(IConfiguration configuration)
{
    private readonly string _connectionString =
        configuration.GetConnectionString("jsini")
        ?? throw new InvalidOperationException("연결 문자열 'jsini' 가 없습니다.");

    private IDbConnection Open() => new NpgsqlConnection(_connectionString);

    /// <summary>화면별 건수. 상세 목록의 「일감」 칸이 한 번에 읽는다.</summary>
    public async Task<List<WbsBoardTaskCount>> CountsAsync(int prjRid)
    {
        using var db = Open();

        var rows = await db.QueryAsync<WbsBoardTaskCount>("""
            select activity_id                              as ActivityId
                 , count(*)::int                            as Cnt
                 , count(*) filter (where done_yn = 'o')::int as Done
              from projmng.wbs_task
             where prj_rid = @prjRid
             group by activity_id
            """, new { prjRid });

        return [.. rows];
    }

    /// <summary>한 화면의 일감. <b>미완료가 먼저</b> 온다.</summary>
    public async Task<List<WbsBoardTask>> ListAsync(int prjRid, string activityId)
    {
        using var db = Open();

        var rows = await db.QueryAsync<WbsBoardTask>("""
            select task_id     as TaskId
                 , activity_id as ActivityId
                 , task_div    as TaskDiv
                 , memo        as Memo
                 , done_yn     as DoneYn
                 , sort_order  as SortOrder
                 , to_char(created_at, 'YYYY-MM-DD HH24:MI') as CreatedAt
                 , to_char(updated_at, 'YYYY-MM-DD HH24:MI') as UpdatedAt
              from projmng.wbs_task
             where prj_rid = @prjRid and activity_id = @activityId
             order by coalesce(done_yn, ''), sort_order, task_id
            """, new { prjRid, activityId });

        return [.. rows];
    }

    /// <summary>그 액티비티가 원장에 있나.</summary>
    public async Task<bool> ParentExistsAsync(int prjRid, string activityId)
    {
        using var db = Open();

        return await db.ExecuteScalarAsync<bool>("""
            select exists (select 1 from projmng.wbs_work
                            where prj_rid = @prjRid and activity_id = @activityId)
            """, new { prjRid, activityId });
    }

    /// <summary>
    /// 새 일감. 차례는 <b>그 화면 안에서 맨 뒤</b>로 붙인다(10 씩 띄운다 —
    /// 사이에 끼워 넣을 자리를 남겨 둔다).
    /// </summary>
    public async Task<int> CreateAsync(int prjRid, string activityId, string? taskDiv, string? memo)
    {
        using var db = Open();

        return await db.ExecuteScalarAsync<int>("""
            insert into projmng.wbs_task (prj_rid, activity_id, task_div, memo, sort_order)
            values (@prjRid, @activityId, @taskDiv, @memo,
                    coalesce((select max(sort_order) + 10 from projmng.wbs_task
                               where prj_rid = @prjRid and activity_id = @activityId), 10))
            returning task_id
            """, new
        {
            prjRid,
            activityId,
            taskDiv = string.IsNullOrWhiteSpace(taskDiv) ? null : taskDiv,
            memo = string.IsNullOrWhiteSpace(memo) ? null : memo,
        });
    }

    /// <summary>
    /// 본문에 담겨 온 것만 고친다. <b>안 담긴 칸은 건드리지 않는다</b> —
    /// 화면이 메모만 고칠 때 완료 표시가 딸려 지워지면 안 된다.
    /// </summary>
    /// <returns>고친 줄 수. 고칠 것이 하나도 없으면 <c>-1</c>.</returns>
    public async Task<int> UpdateAsync(int prjRid, int taskId, IDictionary<string, object?> patch)
    {
        var sets = new List<string>();
        var args = new DynamicParameters();
        var i = 0;

        foreach (var col in (string[])["task_div", "memo", "done_yn"])
        {
            if (!patch.TryGetValue(col, out var raw)) continue;

            var val = raw is string s && string.IsNullOrWhiteSpace(s) ? null : raw;
            var name = $"p{i++}";
            args.Add(name, val);
            sets.Add($"{col} = @{name}::text");
        }

        if (sets.Count == 0) return -1;

        sets.Add("updated_at = now()");
        args.Add("prjRid", prjRid);
        args.Add("taskId", taskId);

        using var db = Open();

        return await db.ExecuteAsync($"""
            update projmng.wbs_task
               set {string.Join(", ", sets)}
             where prj_rid = @prjRid and task_id = @taskId
            """, args);
    }

    public async Task<bool> DeleteAsync(int prjRid, int taskId)
    {
        using var db = Open();

        var affected = await db.ExecuteAsync(
            "delete from projmng.wbs_task where prj_rid = @prjRid and task_id = @taskId",
            new { prjRid, taskId });

        return affected > 0;
    }
}
