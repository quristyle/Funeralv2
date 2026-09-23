using System.Data;
using System.Text;
using System.Text.Json;
using Dapper;
using Npgsql;
using ProjMngServer.Models;
using static ProjMngServer.Services.WbsBoardSql;

namespace ProjMngServer.Services;

/// <summary>
/// ProjectView 캐시 — 신원(<c>workId</c>)과 현재값 스냅샷.
/// </summary>
/// <remarks>
/// <para>
/// [왜 담아 두나]
/// </para>
///
/// <para>
/// 담아 두기 전에는 보낼 때마다 일감 목록을 통째로 다시 받아 <c>코드 → workId</c>
/// 를 만들었다. 신원을 담아 두면 그 단계가 통째로 사라진다. 더 큰 이득은
/// <b>현재값</b>이다 — ProjectView 의 지금 값을 모르면 늘 전 건을 다시 보내야
/// 하는데, 스냅샷이 있으면 달라진 건만 보낸다.
/// </para>
///
/// <para>
/// [수집은 브라우저가 한다]
/// </para>
///
/// <para>
/// ProjectView 가 HTTPS 이고 사내망 안에 있어 서버가 직접 부를 수 없다. 그래서
/// 사람이 ProjectView 화면의 콘솔에 스크립트를 붙여넣어 걷고, 그 결과를 이
/// 서비스가 받는다. <b>여기는 받는 쪽만 안다</b> — 어떻게 걷었는지는 모른다.
/// </para>
/// </remarks>
public sealed class PvCacheService(IConfiguration configuration)
{
    private readonly string _connectionString =
        configuration.GetConnectionString("jsini")
        ?? throw new InvalidOperationException("연결 문자열 'jsini' 가 없습니다.");

    private IDbConnection Open() => new NpgsqlConnection(_connectionString);

    // ──────────────────────────────────────────── 현황

    public async Task<PvCacheStatus> StatusAsync(int prjRid, string? scope)
    {
        using var db = Open();

        var works = await db.QuerySingleAsync<PvWorkStat>($"""
            select count(*)::int                 as Targets
                 , count(p.pv_work_id)::int      as Identified
                 , count(p.pv_snapshot_at)::int  as Snapshot
                 , max(p.pv_seen_at)::text       as LastSeen
                 , max(p.pv_snapshot_at)::text   as LastSnapshot
                 , max(p.pv_project_id)          as ProjectId
              from projmng.wbs_work w
              left join projmng.wbs_pv p
                     on p.prj_rid = w.prj_rid and p.activity_id = w.activity_id
             where w.prj_rid = @prjRid
               and {DevWhere(scope, "w")}
            """, new { prjRid });

        var tasks = await db.QuerySingleAsync<PvTaskStat>("""
            select count(*)::int                             as Tasks
                 , coalesce(sum(pv_node_cnt), 0)::int         as Nodes
                 , coalesce(sum(pv_node_empty), 0)::int       as NodesEmpty
                 , count(pv_charger_id)::int                  as Charged
                 , count(pv_status)::int                      as Staged
                 , max(pv_seen_at)::text                      as LastSeen
                 , (select count(*) from projmng.wbs_pv_node
                     where prj_rid = @prjRid)::int            as NodesCached
              from projmng.wbs_pv_task
             where prj_rid = @prjRid
            """, new { prjRid });

        var orphan = await db.ExecuteScalarAsync<int>("""
            select count(*)::int
              from projmng.wbs_pv p
             where p.prj_rid = @prjRid
               and not exists (select 1 from projmng.wbs_work w
                                where w.prj_rid = p.prj_rid and w.activity_id = p.activity_id)
            """, new { prjRid });

        return new PvCacheStatus
        {
            Scope = scope ?? "dev",
            Works = works,
            Tasks = tasks,
            Orphan = orphan,
        };
    }

    // ──────────────────────────────────────────── 액티비티별

    public async Task<List<PvRow>> RowsAsync(int prjRid, string? scope)
    {
        using var db = Open();

        var rows = await db.QueryAsync<PvRow>($"""
            select w.activity_id      as ActivityId
                 , w.systemcode       as Systemcode
                 , w.menu_nm          as MenuNm
                 , w.user_bp_id       as UserBpId
                 , w.plan_sdt::text   as PlanSdt
                 , w.plan_edt::text   as PlanEdt
                 , w.plan_sdt_c::text as PlanSdtC
                 , p.pv_work_id       as PvWorkId
                 , p.pv_work_title    as PvWorkTitle
                 , p.pv_project_id    as PvProjectId
                 , p.pv_seen_at::text as PvSeenAt
                 , p.pv_finish_rate   as PvFinishRate
                 , p.pv_actual_rate   as PvActualRate
                 , p.pv_plan_sdt::text    as PvPlanSdt
                 , p.pv_plan_edt::text    as PvPlanEdt
                 , p.pv_actual_sdt::text  as PvActualSdt
                 , p.pv_actual_edt::text  as PvActualEdt
                 , p.pv_snapshot_at::text as PvSnapshotAt
                 , t.task_cnt::int    as TaskCnt
                 , t.node_cnt::int    as NodeCnt
                 , t.node_empty::int  as NodeEmpty
                 , t.task_edt::text   as PvTaskEdt
                 , t.workers          as PvWorkers
                 , t.status           as PvStatus
                 , t.status_at::text  as PvStatusAt
                 , t.status_cnt::int  as PvStatusCnt
                 , t.task_code        as PvTaskCode
              from projmng.wbs_work w
              left join {TaskRollup} t
                     on t.prj_rid = w.prj_rid and t.activity_id = w.activity_id
              left join projmng.wbs_pv p
                     on p.prj_rid = w.prj_rid and p.activity_id = w.activity_id
             where w.prj_rid = @prjRid
               and {DevWhere(scope, "w")}
             order by w.activity_id
            """, new { prjRid });

        return [.. rows];
    }

    // ──────────────────────────────────────────── 워크플로 일감

    /// <summary>
    /// 일감 목록. <paramref name="activityId"/> 를 주면 <b>그 화면의 단계까지</b> 같이 준다 —
    /// 펼쳐 볼 때만 필요한 자료라 목록 전체에는 싣지 않는다.
    /// </summary>
    public async Task<PvTaskBundle> TasksAsync(int prjRid, string? activityId)
    {
        using var db = Open();

        const string select = """
            select pv_task_id        as PvTaskId
                 , activity_id       as ActivityId
                 , pv_work_id        as PvWorkId
                 , pv_task_code      as PvTaskCode
                 , pv_task_title     as PvTaskTitle
                 , pv_plan_sdt::text as PvPlanSdt
                 , pv_plan_edt::text as PvPlanEdt
                 , pv_node_cnt       as PvNodeCnt
                 , pv_node_empty     as PvNodeEmpty
                 , pv_charger_id     as PvChargerId
                 , pv_charger_nm     as PvChargerNm
                 , pv_status         as PvStatus
                 , pv_status_at::text as PvStatusAt
                 , pv_seen_at::text   as PvSeenAt
              from projmng.wbs_pv_task
             where prj_rid = @prjRid
            """;

        var one = Nz(activityId);

        var tasks = one is null
            ? await db.QueryAsync<PvTask>(
                select + " order by activity_id, pv_task_code", new { prjRid })
            : await db.QueryAsync<PvTask>(
                select + " and activity_id = @one order by pv_task_code", new { prjRid, one });

        var bundle = new PvTaskBundle { Tasks = [.. tasks] };

        if (one is null) return bundle;

        var nodes = await db.QueryAsync<PvNode>("""
            select n.pv_task_id     as PvTaskId
                 , n.node_no        as NodeNo
                 , n.stage_nm       as StageNm
                 , n.node_dt::text  as NodeDt
                 , n.worker_id      as WorkerId
                 , n.worker_nm      as WorkerNm
              from projmng.wbs_pv_node n
              join projmng.wbs_pv_task t
                on t.prj_rid = n.prj_rid and t.pv_task_id = n.pv_task_id
             where n.prj_rid = @prjRid and t.activity_id = @one
             order by n.pv_task_id, n.node_no
            """, new { prjRid, one });

        bundle.Nodes = [.. nodes];
        return bundle;
    }

    // ──────────────────────────────────────────── 수집 결과 담기

    /// <summary>
    /// 걷어 온 것으로 <b>통째로 덮어쓴다</b> — 캐시는 늘 「지금 ProjectView 의 값」이면 된다.
    /// </summary>
    public async Task<PvIngestResult> IngestAsync(int prjRid, JsonElement body)
    {
        var projectId = Str(body, "projectId", "project_id");

        var listEl = Prop(body, "works", "items", "rows");
        if (listEl is null || listEl.Value.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException("works 배열이 필요합니다.");
        }

        var works = new List<object?[]>();
        var tasks = new List<object?[]>();
        var nodes = new List<object?[]>();

        // 단계를 받은 일감. 그 일감의 옛 단계를 지울 대상이다.
        var nodeTasks = new List<string>();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dupes = new List<string>();
        var noCode = 0;

        foreach (var w in listEl.Value.EnumerateArray())
        {
            var code = Str(w, "code", "activity_id", "activityId");
            if (string.IsNullOrWhiteSpace(code)) { noCode++; continue; }

            // 한 번만 담는다 — 같은 번호가 두 번 오면 한 문장 안에서 기본키가 부딪힌다.
            if (!seen.Add(code)) { dupes.Add(code); continue; }

            works.Add([
                prjRid,
                code,
                projectId ?? Str(w, "projectId"),
                Str(w, "id", "workId", "work_id"),
                Str(w, "title", "workTitle"),
                Dec(w, "finishRate", "finish_rate"),
                Dec(w, "actualRate", "actualProgressRate", "actual_rate"),
                Date(w, "planStartDate", "plan_sdt"),
                Date(w, "planEndDate", "plan_edt"),
                Date(w, "actualStartDate", "plan_sdt_c"),
                Date(w, "actualEndDate", "plan_edt_c"),
            ]);

            var tl = Prop(w, "tasks", "taskList");
            if (tl is null || tl.Value.ValueKind != JsonValueKind.Array) continue;

            foreach (var t in tl.Value.EnumerateArray())
            {
                var tid = Str(t, "id", "taskId", "pv_task_id");
                if (string.IsNullOrWhiteSpace(tid)) continue;

                var nl = Prop(t, "nodes", "taskNodes");
                var hasNodes = nl is not null && nl.Value.ValueKind == JsonValueKind.Array;

                var nodeCnt = Int(t, "nodeCnt", "node_cnt");
                var nodeEmpty = Int(t, "nodeEmpty", "empty", "node_empty");

                if (hasNodes)
                {
                    nodeTasks.Add(tid);

                    var no = 0;
                    var empty = 0;

                    foreach (var nd in nl!.Value.EnumerateArray())
                    {
                        var dt = Date(nd, "date", "node_dt", "endDate", "planEndDate");
                        var wid = Str(nd, "workerId", "worker_id", "chargerId");

                        if (dt is null || wid is null) empty++;

                        nodes.Add([
                            prjRid, tid, no,
                            Str(nd, "id", "nodeId", "node_id"),
                            Str(nd, "stage", "stageNm", "stage_nm", "statusName"),
                            dt, wid,
                            Str(nd, "workerNm", "worker_nm", "workerName"),
                        ]);

                        no++;
                    }

                    // 단계를 함께 받았으면 개수는 **우리가 센다** — 보내 준 값보다 정확하다.
                    nodeCnt = no;
                    nodeEmpty = empty;
                }

                tasks.Add([
                    prjRid,
                    tid,
                    code,
                    Str(w, "id", "workId", "work_id"),
                    Str(t, "code", "taskCode"),
                    Str(t, "title", "name"),
                    Date(t, "planStartDate"),
                    Date(t, "planEndDate", "endDate"),
                    nodeCnt,
                    nodeEmpty,
                    Str(t, "chargerId", "charger_id", "workerId"),
                    Str(t, "chargerNm", "charger_nm", "workerNm"),
                    Str(t, "status", "statusName", "stage"),
                    Date(t, "statusAt", "status_at"),
                ]);
            }
        }

        using var db = Open();

        var nWork = await BulkAsync(db,
            """
            insert into projmng.wbs_pv
              (prj_rid, activity_id, pv_project_id, pv_work_id, pv_work_title,
               pv_finish_rate, pv_actual_rate,
               pv_plan_sdt, pv_plan_edt, pv_actual_sdt, pv_actual_edt,
               pv_seen_at, pv_snapshot_at, updated_at)
            """,
            """
            on conflict (prj_rid, activity_id) do update set
                pv_project_id  = coalesce(excluded.pv_project_id, wbs_pv.pv_project_id)
              , pv_work_id     = coalesce(excluded.pv_work_id,    wbs_pv.pv_work_id)
              , pv_work_title  = coalesce(excluded.pv_work_title, wbs_pv.pv_work_title)
              , pv_finish_rate = excluded.pv_finish_rate
              , pv_actual_rate = excluded.pv_actual_rate
              , pv_plan_sdt    = excluded.pv_plan_sdt
              , pv_plan_edt    = excluded.pv_plan_edt
              , pv_actual_sdt  = excluded.pv_actual_sdt
              , pv_actual_edt  = excluded.pv_actual_edt
              , pv_seen_at     = now()
              , pv_snapshot_at = now()
              , updated_at     = now()
            """,
            ["::int", "::text", "::text", "::text", "::text",
             "::numeric", "::numeric",
             "::date", "::date", "::date", "::date"],
            works, "now(), now(), now()");

        var nTask = tasks.Count == 0 ? 0 : await BulkAsync(db,
            """
            insert into projmng.wbs_pv_task
              (prj_rid, pv_task_id, activity_id, pv_work_id, pv_task_code, pv_task_title,
               pv_plan_sdt, pv_plan_edt, pv_node_cnt, pv_node_empty,
               pv_charger_id, pv_charger_nm, pv_status, pv_status_at,
               pv_seen_at, updated_at)
            """,
            """
            on conflict (prj_rid, pv_task_id) do update set
                activity_id   = excluded.activity_id
              , pv_work_id    = coalesce(excluded.pv_work_id,    wbs_pv_task.pv_work_id)
              , pv_task_code  = coalesce(excluded.pv_task_code,  wbs_pv_task.pv_task_code)
              , pv_task_title = coalesce(excluded.pv_task_title, wbs_pv_task.pv_task_title)
              , pv_plan_sdt   = excluded.pv_plan_sdt
              , pv_plan_edt   = excluded.pv_plan_edt
              , pv_node_cnt   = coalesce(excluded.pv_node_cnt,   wbs_pv_task.pv_node_cnt)
              , pv_node_empty = coalesce(excluded.pv_node_empty, wbs_pv_task.pv_node_empty)
              , pv_charger_id = coalesce(excluded.pv_charger_id, wbs_pv_task.pv_charger_id)
              , pv_charger_nm = coalesce(excluded.pv_charger_nm, wbs_pv_task.pv_charger_nm)
              -- 단계는 걷어 왔으면 그 값이 이긴다. **되돌아가는 경우가 있어서**
              -- coalesce 로 지키면 안 된다. 이번에 못 걷은 것(null)만 옛 값을 둔다.
              , pv_status     = coalesce(excluded.pv_status,    wbs_pv_task.pv_status)
              , pv_status_at  = coalesce(excluded.pv_status_at, wbs_pv_task.pv_status_at)
              , pv_seen_at    = now()
              , updated_at    = now()
            """,
            ["::int", "::text", "::text", "::text", "::text", "::text",
             "::date", "::date", "::int", "::int",
             "::text", "::text", "::text", "::date"],
            tasks, "now(), now()");

        var nNode = 0;

        if (nodeTasks.Count > 0)
        {
            // 단계는 일감 단위로 **통째로 갈아 끼운다.** 단계가 줄었을 때
            // (중간 단계를 지운 경우) 옛 줄이 남으면 「빈 단계」로 계속 잡힌다.
            for (var at = 0; at < nodeTasks.Count; at += 200)
            {
                var part = nodeTasks.GetRange(at, Math.Min(200, nodeTasks.Count - at));

                await db.ExecuteAsync("""
                    delete from projmng.wbs_pv_node
                     where prj_rid = @prjRid and pv_task_id = any(@ids)
                    """, new { prjRid, ids = part.ToArray() });
            }

            nNode = await BulkAsync(db,
                """
                insert into projmng.wbs_pv_node
                  (prj_rid, pv_task_id, node_no, node_id, stage_nm, node_dt,
                   worker_id, worker_nm, updated_at)
                """,
                "on conflict (prj_rid, pv_task_id, node_no) do nothing",
                ["::int", "::text", "::int", "::text", "::text", "::date", "::text", "::text"],
                nodes, "now()");
        }

        var unknown = await db.QueryAsync<string>("""
            select p.activity_id
              from projmng.wbs_pv p
             where p.prj_rid = @prjRid
               and not exists (select 1 from projmng.wbs_work w
                                where w.prj_rid = p.prj_rid and w.activity_id = p.activity_id)
             order by p.activity_id
             limit 100
            """, new { prjRid });

        var unknownList = unknown.ToList();

        return new PvIngestResult
        {
            Works = nWork,
            Tasks = nTask,
            Nodes = nNode,
            SkippedNoCode = noCode,
            Duplicated = dupes.Count,
            DuplicatedCodes = [.. dupes.Take(20)],
            UnknownCount = unknownList.Count,
            Unknown = unknownList,
            ProjectId = projectId,
        };
    }

    /// <param name="what"><c>all</c> · <c>works</c> · <c>tasks</c>.</param>
    public async Task<PvClearResult> ClearAsync(int prjRid, string? what)
    {
        var w = (what ?? "all").ToLowerInvariant();

        using var db = Open();

        var works = w is "all" or "works"
            ? await db.ExecuteAsync(
                "delete from projmng.wbs_pv where prj_rid = @prjRid", new { prjRid })
            : 0;

        // 단계는 일감에 매달린 것이라 일감을 지우면 외래키가 함께 지운다.
        // 그래도 개수를 보여 주려고 먼저 센다.
        var nodes = 0;
        var tasks = 0;

        if (w is "all" or "tasks")
        {
            nodes = await db.ExecuteScalarAsync<int>(
                "select count(*)::int from projmng.wbs_pv_node where prj_rid = @prjRid",
                new { prjRid });

            tasks = await db.ExecuteAsync(
                "delete from projmng.wbs_pv_task where prj_rid = @prjRid", new { prjRid });
        }

        return new PvClearResult { Works = works, Tasks = tasks, Nodes = nodes };
    }

    // ──────────────────────────────────────────── 묶음 넣기

    /// <summary>
    /// 값을 100줄씩 끊어 한 문장으로 넣는다.
    /// </summary>
    /// <remarks>
    /// 211건을 211번 왕복하지 않으려는 것이다. 끊는 까닭은 PostgreSQL 의
    /// 매개변수 상한(65535)이다 — 칸이 열넷인 일감을 안 끊고 넣으면
    /// <b>4,681줄에서 넘친다.</b>
    /// </remarks>
    private static async Task<int> BulkAsync(
        IDbConnection db, string head, string tail, string[] casts,
        List<object?[]> rows, string? literals = null, int chunk = 100)
    {
        var total = 0;

        for (var at = 0; at < rows.Count; at += chunk)
        {
            var part = rows.GetRange(at, Math.Min(chunk, rows.Count - at));

            var sb = new StringBuilder(head).Append(" values ");
            var args = new DynamicParameters();
            var n = 0;

            for (var i = 0; i < part.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append('(');

                for (var c = 0; c < casts.Length; c++)
                {
                    if (c > 0) sb.Append(", ");

                    var name = $"p{n++}";
                    args.Add(name, part[i][c]);
                    sb.Append('@').Append(name).Append(casts[c]);
                }

                if (!string.IsNullOrEmpty(literals)) sb.Append(", ").Append(literals);
                sb.Append(')');
            }

            sb.Append(' ').Append(tail);

            total += await db.ExecuteAsync(sb.ToString(), args);
        }

        return total;
    }

    // ──────────────────────────────────────────── 값 꺼내기
    //
    // 수집 스크립트가 보내는 이름이 한 가지가 아니다(ProjectView 화면마다
    // 다르고, 손으로 만든 JSON 도 온다). 그래서 **이름을 여러 개 받아
    // 먼저 맞는 것**을 쓴다.

    private static JsonElement? Prop(JsonElement o, params string[] names)
    {
        if (o.ValueKind != JsonValueKind.Object) return null;

        foreach (var n in names)
        {
            if (o.TryGetProperty(n, out var v)) return v;
        }

        return null;
    }

    private static string? Str(JsonElement o, params string[] names)
    {
        var v = Prop(o, names);
        if (v is null || v.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;

        var s = v.Value.ValueKind == JsonValueKind.String ? v.Value.GetString() : v.Value.ToString();
        return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }

    /// <summary><c>2026-08-31T00:00:00</c> → <c>2026-08-31</c>. 못 읽으면 <c>null</c>.</summary>
    private static string? Date(JsonElement o, params string[] names)
    {
        var s = Str(o, names);
        if (s is null) return null;

        if (s.Length > 10) s = s[..10];
        return DateOnly.TryParse(s, out var d) ? d.ToString("yyyy-MM-dd") : null;
    }

    private static decimal? Dec(JsonElement o, params string[] names)
    {
        var v = Prop(o, names);
        if (v is null || v.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;

        if (v.Value.ValueKind == JsonValueKind.Number)
        {
            return v.Value.TryGetDecimal(out var d) ? d : null;
        }

        var s = v.Value.ValueKind == JsonValueKind.String ? v.Value.GetString() : v.Value.ToString();

        return decimal.TryParse(s, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : null;
    }

    private static int? Int(JsonElement o, params string[] names)
    {
        var d = Dec(o, names);
        return d is null ? null : (int)d.Value;
    }
}
