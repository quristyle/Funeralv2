using Dapper;
using Npgsql;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>
/// WBS · 일정표. 옛 <c>sp_proj_wbs_exec</c>(185줄)과
/// <c>sp_proj_wbs_moniter</c>(94줄)를 대신한다.
///
/// <para>
/// 두 화면이 <b>같은 표</b>를 본다 — <c>schedule_type</c> 이 <c>WBS</c> 면
/// 산출물 단위, <c>SCHEDULE</c> 이면 날짜 단위다.
/// </para>
///
/// <para>[옛 프로시저에서 고친 것]</para>
/// <list type="number">
///   <item>
///     <b>조회가 자료를 고쳤다.</b> <c>srch</c> 갈래가 목록을 내기 전에
///     <c>UPDATE dev_wbs</c> 를 돌려 빈 날짜를 <c>dev_edt</c> 로 메웠다.
///     읽기만 하려는 사람이 표를 바꾸는 셈이고, 되돌릴 수도 없다.
///     이제 <b>쓰지 않고 읽을 때만 메운다</b>(<c>COALESCE</c>) — 화면에 보이는
///     값과 거르는 기준은 그대로다.
///   </item>
///   <item>
///     <b>일정표의 기간 탭이 아무 일도 안 했다.</b> 화면은 <c>start_day</c> ·
///     <c>end_day</c> 를 실어 보내는데 프로시저의 <c>WHERE</c> 에 그 값을 보는
///     곳이 없었다. 주·월·분기를 아무리 눌러도 목록이 같았다.
///     이제 <b>계획 기간이 그 구간과 겹치는 것</b>을 낸다.
///   </item>
///   <item>
///     <b>수정이 고친 사람을 안 남겼다.</b> <c>mod_dt</c> 만 찍고
///     <c>mod_user</c> 는 그대로 두어, 등록한 사람이 끝까지 고친 사람으로
///     남았다.
///   </item>
///   <item>
///     <b>수정으로 바꿀 수 없는 칸이 많았다</b> — <c>gb1</c> · <c>gb2</c> ·
///     <c>proc_lvl</c> · <c>build_user</c> · <c>build_status</c> · <c>qc_user</c>.
///     목록에는 나오는데 고칠 길이 없었다. 이제 저장한다.
///   </item>
///   <item>
///     번호를 <c>max(wbs_id)+1</c> 로 만든다. 다른 표와 같다 — 한 문장으로
///     좁혔고 진짜 해법은 시퀀스다.
///   </item>
/// </list>
/// </summary>
public sealed class WbsService(IConfiguration configuration) {

  private readonly string _connectionString =
      configuration.GetConnectionString("jsini")
      ?? throw new InvalidOperationException("ConnectionStrings:jsini 가 없습니다.");

  /// <summary>
  /// 읽을 때 메우는 날짜. <b>표를 고치지 않는다</b>(머리말 1).
  /// 계획 날짜가 비어 있으면 개발 종료일로 본다 — 옛 조회가 UPDATE 로 하던 일이다.
  /// </summary>
  private const string Dates = """
      COALESCE(a.plan_sdt, a.dev_edt) AS PlanSdt,
      COALESCE(a.plan_edt, a.dev_edt) AS PlanEdt,
      COALESCE(a.dev_sdt,  a.dev_edt) AS DevSdt
      """;

  private const string Columns = $"""
      a.wbs_id       AS WbsId,
      a.prj_rid      AS PrjRid,
      a.proc_id      AS ProcId,
      a.gb1          AS Gb1,
      a.gb2          AS Gb2,
      a.proc_nm      AS ProcNm,
      a.proc_tp      AS ProcTp,
      a.proc_lvl     AS ProcLvl,
      a.build_user   AS BuildUser,
      a.build_status AS BuildStatus,
      a.dev_user     AS DevUser,
      {Dates},
      a.dev_edt      AS DevEdt,
      a.dev_chk      AS DevChk,
      a.build_chk    AS BuildChk,
      a.build_chk_dt AS BuildChkDt,
      a.qc_user      AS QcUser,
      a.qc_chk       AS QcChk,
      a.qc_chk_dt    AS QcChkDt,
      a.cre_user     AS CreUser,
      a.cre_dt       AS CreDt,
      a.mod_user     AS ModUser,
      a.mod_dt       AS ModDt,
      a.comm         AS Comm,
      a.schedule_type AS ScheduleType,
      (COALESCE(a.plan_edt, a.dev_edt) - COALESCE(a.plan_sdt, a.dev_edt)) + 1 AS PlanGap,
      CASE WHEN COALESCE(a.dev_sdt, a.dev_edt) IS NULL THEN 'READY'
           WHEN a.dev_edt IS NULL                      THEN 'RUNNING'
           ELSE 'COMP'
      END AS WbsState
      """;

  /// <summary>
  /// 목록을 읽는다.
  /// </summary>
  /// <param name="prjRid">프로젝트. 이 화면들은 프로젝트 없이 보지 않는다.</param>
  /// <param name="compStat">
  /// 진행 상태로 좁힌다 — <c>READY</c> · <c>RUNNING</c> · <c>COMP</c> ·
  /// <c>DISCOMP</c>(아직 안 끝난 것 전부).
  /// </param>
  /// <param name="scheduleType"><c>WBS</c> 또는 <c>SCHEDULE</c>.</param>
  /// <param name="from">계획 기간이 이 날 뒤에 걸치는 것만(머리말 2).</param>
  /// <param name="to">계획 기간이 이 날 앞에 걸치는 것만.</param>
  /// <param name="ct">취소 토큰</param>
  public async Task<IReadOnlyList<WbsItem>> ListAsync(
      int? prjRid = null, string? compStat = null, string? scheduleType = null,
      DateOnly? from = null, DateOnly? to = null, CancellationToken ct = default) {

    await using var db = new NpgsqlConnection(_connectionString);

    var rows = await db.QueryAsync<WbsItem>(new CommandDefinition($"""
        SELECT {Columns}
          FROM projmng.dev_wbs a
         WHERE (@prjRid::int IS NULL OR a.prj_rid = @prjRid)
           AND (@scheduleType = '' OR a.schedule_type = @scheduleType)
           AND (@compStat = ''
                OR (@compStat = 'READY'
                    AND COALESCE(a.plan_sdt, a.dev_edt) IS NOT NULL
                    AND COALESCE(a.plan_edt, a.dev_edt) IS NOT NULL
                    AND COALESCE(a.dev_sdt,  a.dev_edt) IS NULL)
                OR (@compStat = 'RUNNING'
                    AND COALESCE(a.plan_sdt, a.dev_edt) IS NOT NULL
                    AND COALESCE(a.plan_edt, a.dev_edt) IS NOT NULL
                    AND COALESCE(a.dev_sdt,  a.dev_edt) IS NOT NULL
                    AND a.dev_edt IS NULL)
                OR (@compStat = 'DISCOMP'
                    AND COALESCE(a.plan_sdt, a.dev_edt) IS NOT NULL
                    AND COALESCE(a.plan_edt, a.dev_edt) IS NOT NULL
                    AND a.dev_edt IS NULL)
                OR (@compStat = 'COMP'
                    AND COALESCE(a.plan_sdt, a.dev_edt) IS NOT NULL
                    AND COALESCE(a.plan_edt, a.dev_edt) IS NOT NULL
                    AND a.dev_edt IS NOT NULL))
           -- 기간은 **겹침**으로 본다. 시작일만 보면 걸쳐 있는 긴 일감이
           -- 그 주에서 사라진다 — 그것이 일정표에서 제일 보고 싶은 줄이다.
           AND (@from::date IS NULL OR COALESCE(a.plan_edt, a.dev_edt) IS NULL
                OR COALESCE(a.plan_edt, a.dev_edt) >= @from)
           AND (@to::date   IS NULL OR COALESCE(a.plan_sdt, a.dev_edt) IS NULL
                OR COALESCE(a.plan_sdt, a.dev_edt) <= @to)
         ORDER BY a.proc_id, a.gb1, a.gb2, a.proc_tp, a.proc_nm, a.wbs_id
        """,
        new {
          prjRid,
          compStat = compStat ?? string.Empty,
          scheduleType = scheduleType ?? string.Empty,
          from,
          to,
        },
        cancellationToken: ct));

    return [.. rows];
  }

  /// <summary>한 건을 읽는다.</summary>
  public async Task<WbsItem?> GetAsync(int wbsId, CancellationToken ct = default) {
    await using var db = new NpgsqlConnection(_connectionString);

    return await db.QuerySingleOrDefaultAsync<WbsItem>(new CommandDefinition($"""
        SELECT {Columns} FROM projmng.dev_wbs a WHERE a.wbs_id = @wbsId
        """, new { wbsId }, cancellationToken: ct));
  }

  /// <summary>
  /// 새로 넣는다.
  ///
  /// <para>
  /// 계획 날짜를 안 주면 <b>개발 날짜로, 그것도 없으면 오늘로</b> 메운다.
  /// 옛 프로시저와 같다 — 계획이 빈 줄은 진행 상태를 매길 수 없어 어느
  /// 집계에도 안 잡힌다.
  /// </para>
  /// </summary>
  public async Task<WbsItem?> CreateAsync(WbsItem item, string userId, CancellationToken ct = default) {
    await using var db = new NpgsqlConnection(_connectionString);

    var id = await db.ExecuteScalarAsync<int>(new CommandDefinition("""
        INSERT INTO projmng.dev_wbs
             ( prj_rid, wbs_id, proc_id, gb1, gb2, proc_nm, proc_tp, proc_lvl,
               build_user, build_status, dev_user,
               plan_sdt, plan_edt, dev_sdt, dev_edt,
               qc_user, cre_user, cre_dt, mod_user, mod_dt, schedule_type, comm )
        SELECT @PrjRid, COALESCE(MAX(wbs_id), 0) + 1,
               @ProcId, @Gb1, @Gb2, @ProcNm, @ProcTp, @ProcLvl,
               @BuildUser, @BuildStatus, @DevUser,
               COALESCE(@PlanSdt, @DevSdt, CURRENT_DATE),
               COALESCE(@PlanEdt, @DevEdt, CURRENT_DATE),
               @DevSdt, @DevEdt,
               @QcUser, @userId, now(), @userId, now(), @ScheduleType, @Comm
          FROM projmng.dev_wbs
        RETURNING wbs_id
        """,
        new {
          item.PrjRid, item.ProcId, item.Gb1, item.Gb2, item.ProcNm, item.ProcTp, item.ProcLvl,
          item.BuildUser, item.BuildStatus, item.DevUser,
          item.PlanSdt, item.PlanEdt, item.DevSdt, item.DevEdt,
          item.QcUser, item.ScheduleType, item.Comm,
          userId,
        },
        cancellationToken: ct));

    return await GetAsync(id, ct);
  }

  /// <summary>
  /// 고친다. <b>고친 사람을 남긴다</b>(머리말 3) — 옛 것은 시각만 찍었다.
  /// </summary>
  public async Task<WbsItem?> UpdateAsync(WbsItem item, string userId, CancellationToken ct = default) {
    await using var db = new NpgsqlConnection(_connectionString);

    await db.ExecuteAsync(new CommandDefinition("""
        UPDATE projmng.dev_wbs
           SET prj_rid       = @PrjRid,
               proc_id       = @ProcId,
               gb1           = @Gb1,
               gb2           = @Gb2,
               proc_nm       = @ProcNm,
               proc_tp       = @ProcTp,
               proc_lvl      = @ProcLvl,
               build_user    = @BuildUser,
               build_status  = @BuildStatus,
               dev_user      = @DevUser,
               dev_sdt       = @DevSdt,
               dev_edt       = @DevEdt,
               plan_sdt      = COALESCE(@PlanSdt, @DevSdt, @DevEdt, CURRENT_DATE),
               plan_edt      = COALESCE(@PlanEdt, @DevEdt, CURRENT_DATE),
               qc_user       = @QcUser,
               schedule_type = @ScheduleType,
               comm          = @Comm,
               mod_user      = @userId,
               mod_dt        = now()
         WHERE wbs_id = @WbsId
        """,
        new {
          item.WbsId, item.PrjRid, item.ProcId, item.Gb1, item.Gb2, item.ProcNm,
          item.ProcTp, item.ProcLvl, item.BuildUser, item.BuildStatus, item.DevUser,
          item.PlanSdt, item.PlanEdt, item.DevSdt, item.DevEdt,
          item.QcUser, item.ScheduleType, item.Comm,
          userId,
        },
        cancellationToken: ct));

    return await GetAsync(item.WbsId, ct);
  }

  /// <summary>지운다.</summary>
  public async Task<bool> DeleteAsync(int wbsId, CancellationToken ct = default) {
    await using var db = new NpgsqlConnection(_connectionString);

    var rows = await db.ExecuteAsync(new CommandDefinition(
        "DELETE FROM projmng.dev_wbs WHERE wbs_id = @wbsId",
        new { wbsId }, cancellationToken: ct));

    return rows > 0;
  }

  /// <summary>
  /// 진척 집계. 옛 <c>sp_proj_wbs_moniter</c> 를 그대로 옮겼다.
  ///
  /// <para>
  /// <b>세는 기준은 손대지 않았다.</b> 「끝냈다」는 개발 종료일이 있는 것,
  /// 「늦었다」는 계획 종료일이 지났는데 개발 종료일이 없는 것이다. 비율의
  /// 분모는 언제나 전체 건수다 — 그래서 진행 중·미시작·지연을 더해도 100 이
  /// 되지 않는다(계획 날짜가 빈 줄이 어디에도 안 잡힌다). 그 성질이 화면의
  /// 숫자와 묶여 있어 여기서 바꾸지 않는다.
  /// </para>
  /// </summary>
  public async Task<WbsSummary> SummaryAsync(int prjRid, CancellationToken ct = default) {
    await using var db = new NpgsqlConnection(_connectionString);

    var summary = await db.QuerySingleOrDefaultAsync<WbsSummary>(new CommandDefinition("""
        WITH base AS (
            SELECT * FROM projmng.dev_wbs WHERE prj_rid = @prjRid
        ),
        counts AS (
            SELECT
                COUNT(*)::decimal AS total_task_count,
                COUNT(*) FILTER (WHERE dev_edt IS NOT NULL) AS completed_task_count,
                COUNT(*) FILTER (WHERE dev_edt IS NOT NULL
                                   AND dev_edt <= plan_edt) AS completed_within_plan_count,
                COUNT(*) FILTER (WHERE dev_edt IS NULL
                                   AND plan_edt < CURRENT_DATE) AS delayed_task_count,
                COUNT(*) FILTER (WHERE dev_edt IS NULL
                                   AND plan_sdt <= CURRENT_DATE
                                   AND plan_edt >= CURRENT_DATE) AS in_progress_task_count,
                COUNT(*) FILTER (WHERE dev_edt IS NULL
                                   AND plan_sdt > CURRENT_DATE) AS not_started_yet_task_count,
                COUNT(*) FILTER (WHERE plan_sdt <= CURRENT_DATE) AS planneds_until_now_count,
                COUNT(*) FILTER (WHERE plan_edt <= CURRENT_DATE) AS planned_until_now_count
              FROM base
        )
        SELECT total_task_count             AS TotalTaskCount,
               completed_task_count         AS CompletedTaskCount,
               ROUND(completed_task_count * 100.0 / NULLIF(total_task_count, 0), 1) AS CompletedTaskPct,
               completed_task_count + in_progress_task_count AS CompAndIngCnt,
               completed_within_plan_count  AS CompletedWithinPlanCount,
               ROUND(completed_within_plan_count * 100.0 / NULLIF(total_task_count, 0), 1) AS CompletedWithinPlanPct,
               delayed_task_count           AS DelayedTaskCount,
               ROUND(delayed_task_count * 100.0 / NULLIF(total_task_count, 0), 1) AS DelayedTaskPct,
               in_progress_task_count       AS InProgressTaskCount,
               ROUND(in_progress_task_count * 100.0 / NULLIF(total_task_count, 0), 1) AS InProgressTaskPct,
               not_started_yet_task_count   AS NotStartedYetTaskCount,
               ROUND(not_started_yet_task_count * 100.0 / NULLIF(total_task_count, 0), 1) AS NotStartedYetPct,
               planneds_until_now_count     AS PlannedsUntilNowCount,
               ROUND(planneds_until_now_count * 100.0 / NULLIF(total_task_count, 0), 1) AS PlannedsUntilNowPct,
               planned_until_now_count      AS PlannedUntilNowCount,
               ROUND(planned_until_now_count * 100.0 / NULLIF(total_task_count, 0), 1) AS PlannedUntilNowPct
          FROM counts
        """, new { prjRid }, cancellationToken: ct));

    // 줄이 하나도 없는 프로젝트여도 빈 집계를 준다 — 화면의 타일이 자리를
    // 지키게 하려는 것이다. 옛 것은 0 행이라 화면이 빈칸을 그렸다.
    return summary ?? new WbsSummary();
  }
}
