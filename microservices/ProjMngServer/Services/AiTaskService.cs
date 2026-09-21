using System.Data;

using Dapper;
using Npgsql;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>
/// AI 작업 — <c>projmng.ai_task</c>.
/// </summary>
/// <remarks>
/// 설계는 <c>docs/ai-task-runner.md</c>.
///
/// <para>
/// <b>이 서비스는 아직 아무것도 실행하지 않는다.</b> 1단계는 「글을 쓰고
/// 요청을 누르면 상태가 준비중까지 간다」까지다. 큐에 넣는 것과 실행기는
/// 2단계다.
/// </para>
/// </remarks>
public sealed class AiTaskService(
    IConfiguration configuration, AiTargetService targets, AiTaskQueue queue)
{
    private readonly string _connectionString =
        configuration.GetConnectionString("jsini")
        ?? throw new InvalidOperationException("연결 문자열 'jsini' 가 없습니다.");

    private IDbConnection Open() => new NpgsqlConnection(_connectionString);

    private const string Columns = """
        a.task_key        AS TaskKey,
        a.title           AS Title,
        a.contents        AS Contents,
        a.content_format  AS ContentFormat,
        a.target_key      AS TargetKey,
        b.target_nm       AS TargetNm,
        b.target_path     AS TargetPath,
        COALESCE(b.allow_push, false) AS TargetAllowPush,
        b.runner_kinds    AS TargetRunnerKinds,
        a.target_ref      AS TargetRef,
        a.runner_kind     AS RunnerKind,
        a.request_flag    AS RequestFlag,
        a.task_status     AS TaskStatus,
        a.priority        AS Priority,
        a.timeout_minutes AS TimeoutMinutes,
        a.attempt_count   AS AttemptCount,
        a.attempt_max     AS AttemptMax,
        a.auto_push       AS AutoPush,
        a.user_confirmed  AS UserConfirmed,
        a.notify_email    AS NotifyEmail,
        a.notify_pwa      AS NotifyPwa,
        a.notify_to       AS NotifyTo,
        a.notify_when     AS NotifyWhen,
        a.notify_error    AS NotifyError,
        a.requested_at    AS RequestedAt,
        a.started_at      AS StartedAt,
        a.finished_at     AS FinishedAt,
        a.duration_ms     AS DurationMs,
        a.last_run_key    AS LastRunKey,
        a.last_exit_code  AS LastExitCode,
        a.last_error      AS LastError,
        a.pushed_commit   AS PushedCommit,
        a.previous_tag    AS PreviousTag,
        a.row_version     AS RowVersion,
        a.cre_id          AS CreId,
        a.cre_dt          AS CreDt,
        a.mod_id          AS ModId,
        a.mod_dt          AS ModDt
        """;

    /// <summary>
    /// 목록. 준 조건만 걸린다.
    /// </summary>
    /// <remarks>
    /// <b>본문(<c>contents</c>)까지 함께 읽는다.</b> 목록이 무거워 보이지만
    /// 이 화면은 왼쪽에서 고르면 오른쪽 편집기에 바로 붙는 구조라, 안 읽으면
    /// 고를 때마다 한 번 더 다녀와야 한다. 건수가 많아지면 그때 나눈다.
    /// </remarks>
    public async Task<List<AiTask>> ListAsync(
        string? taskStatus = null, string? requestFlag = null, long? targetKey = null,
        string? keyword = null, long? taskKey = null, bool? userConfirmed = null)
    {
        using var db = Open();

        var rows = await db.QueryAsync<AiTask>($"""
            SELECT {Columns}
              FROM projmng.ai_task a
              LEFT JOIN projmng.ai_target b ON b.target_key = a.target_key
             -- 널일 수 있는 파라미터에는 **형을 붙인다.** 안 붙이면 PostgreSQL 이
             -- 형을 못 정해 42P08 로 끊는다 — 값이 있을 때는 멀쩡하고
             -- **비었을 때만** 난다.
             WHERE a.is_deleted = false
               AND (@taskKey::bigint IS NULL OR a.task_key = @taskKey)
               AND (@taskStatus = '' OR a.task_status = @taskStatus)
               AND (@requestFlag = '' OR a.request_flag = @requestFlag)
               AND (@targetKey::bigint IS NULL OR a.target_key = @targetKey)
               AND (@userConfirmed::boolean IS NULL OR a.user_confirmed = @userConfirmed)
               AND (@keyword = '' OR a.title ILIKE '%' || @keyword || '%'
                                  OR a.contents ILIKE '%' || @keyword || '%')
             ORDER BY a.task_key DESC
            """, new
        {
            taskKey,
            taskStatus = taskStatus ?? string.Empty,
            requestFlag = requestFlag ?? string.Empty,
            targetKey,
            keyword = keyword ?? string.Empty,
            userConfirmed,
        });

        return [.. rows];
    }

    public async Task<AiTask?> GetAsync(long taskKey)
        => (await ListAsync(taskKey: taskKey)).FirstOrDefault();

    public async Task<AiTask?> CreateAsync(AiTask item, string? userId)
    {
        await NormalizeAsync(item);

        using var db = Open();

        var key = await db.ExecuteScalarAsync<long>("""
            INSERT INTO projmng.ai_task
                 ( title, contents, content_format, target_key, target_ref,
                   runner_kind, request_flag, task_status, priority,
                   timeout_minutes, attempt_max, auto_push,
                   notify_email, notify_pwa, notify_to, notify_when,
                   row_version, cre_id, cre_dt )
            VALUES ( @Title, @Contents, @ContentFormat, @TargetKey, @TargetRef,
                     @RunnerKind, 'none', 'idle', @Priority,
                     @TimeoutMinutes, @AttemptMax, @AutoPush,
                     @NotifyEmail, @NotifyPwa, @NotifyTo, @NotifyWhen,
                     1, @userId, now() )
            RETURNING task_key
            """, new
        {
            item.Title, item.Contents, item.ContentFormat, item.TargetKey, item.TargetRef,
            item.RunnerKind, item.Priority, item.TimeoutMinutes, item.AttemptMax,
            item.AutoPush, item.NotifyEmail, item.NotifyPwa, item.NotifyTo, item.NotifyWhen, userId,
        });

        return await GetAsync(key);
    }

    /// <summary>
    /// 고친다. <b>도는 중에는 본문을 고칠 수 없다</b>(<see cref="AiTaskEditResult"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 동시 편집은 <c>row_version</c> 으로 막는다. 조건에 그 값을 넣어 두면
    /// 남이 먼저 고친 경우 <c>UPDATE</c> 가 0건이 되고, 그때 「다른 사람이
    /// 먼저 고쳤습니다」를 말할 수 있다. 검사 후 갱신으로 나누면 그 사이에
    /// 끼어드는 것을 막지 못한다.
    /// </para>
    /// </remarks>
    public async Task<AiTaskEditResult> UpdateAsync(long taskKey, AiTask item, string? userId)
    {
        await NormalizeAsync(item);

        var current = await GetAsync(taskKey);

        if (current is null)
        {
            return AiTaskEditResult.NotFound();
        }

        if (AiTaskStatus.IsBusy(current.TaskStatus))
        {
            return AiTaskEditResult.Conflict(
                "지금 돌고 있는 작업입니다. 끝난 뒤에 고치거나, 먼저 취소하십시오.");
        }

        using var db = Open();

        var affected = await db.ExecuteAsync("""
            UPDATE projmng.ai_task
               SET title           = @Title,
                   contents        = @Contents,
                   content_format  = @ContentFormat,
                   target_key      = @TargetKey,
                   target_ref      = @TargetRef,
                   runner_kind     = @RunnerKind,
                   priority        = @Priority,
                   timeout_minutes = @TimeoutMinutes,
                   attempt_max     = @AttemptMax,
                   auto_push       = @AutoPush,
                   notify_email    = @NotifyEmail,
                   notify_pwa      = @NotifyPwa,
                   notify_to       = @NotifyTo,
                   notify_when     = @NotifyWhen,
                   row_version     = row_version + 1,
                   mod_id          = @userId,
                   mod_dt          = now()
             WHERE task_key    = @taskKey
               AND is_deleted  = false
               AND row_version = @RowVersion
            """, new
        {
            taskKey, item.Title, item.Contents, item.ContentFormat, item.TargetKey,
            item.TargetRef, item.RunnerKind, item.Priority, item.TimeoutMinutes,
            item.AttemptMax, item.AutoPush, item.NotifyEmail, item.NotifyPwa, item.NotifyTo,
            item.NotifyWhen, item.RowVersion, userId,
        });

        if (affected == 0)
        {
            return AiTaskEditResult.Conflict(
                "다른 사람이 먼저 고쳤습니다. 다시 읽은 뒤 저장하십시오.");
        }

        return AiTaskEditResult.Ok(await GetAsync(taskKey));
    }

    /// <summary>
    /// <b>작업을 요청한다</b> — 요청여부를 <c>requested</c>, 상태를 <c>queued</c> 로.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 여기서 <c>preparing</c> 으로 만들지 않는다. <b>준비중은 실행기가 집었을 때</b>
    /// 붙는 값이다(설계 6.5). 요청과 집어가기를 한 걸음으로 합치면
    /// 「요청했는데 아무도 없는 상태」를 표현할 수 없다.
    /// </para>
    /// <para>
    /// 조건을 <c>WHERE</c> 에 다 넣어 <b>한 문장으로</b> 바꾼다. 읽고 판단하고
    /// 쓰는 세 걸음으로 나누면 두 번 누른 사이에 상태가 바뀌어 있을 수 있다.
    /// </para>
    /// </remarks>
    public async Task<AiTaskEditResult> RequestAsync(long taskKey, string? userId)
    {
        var current = await GetAsync(taskKey);

        if (current is null)
        {
            return AiTaskEditResult.NotFound();
        }

        if (current.TargetKey is null)
        {
            return AiTaskEditResult.Conflict("대상을 먼저 고르십시오.");
        }

        if (string.IsNullOrWhiteSpace(current.Contents))
        {
            return AiTaskEditResult.Conflict("내용이 비어 있습니다.");
        }

        var target = await targets.GetAsync(current.TargetKey.Value);

        if (target is null)
        {
            return AiTaskEditResult.Conflict("고른 작업 대상을 찾을 수 없습니다.");
        }

        if (!RunnerAllowed(target.RunnerKinds, current.RunnerKind))
        {
            return AiTaskEditResult.Conflict(
                $"고른 대상은 '{current.RunnerKind}' 실행기를 허용하지 않습니다.");
        }

        if (AiTaskStatus.IsBusy(current.TaskStatus))
        {
            return AiTaskEditResult.Conflict("이미 대기 중이거나 돌고 있습니다.");
        }

        using var db = Open();

        var affected = await db.ExecuteAsync("""
            UPDATE projmng.ai_task
               SET request_flag = 'requested',
                   task_status  = 'queued',
                   requested_at = now(),
                   started_at   = NULL,
                   finished_at  = NULL,
                   duration_ms  = NULL,
                   last_error    = NULL,
                   notify_error  = NULL,
                   attempt_count = 0,
                   row_version   = row_version + 1,
                   mod_id       = @userId,
                   mod_dt       = now()
             WHERE task_key   = @taskKey
               AND is_deleted = false
               AND task_status NOT IN ('queued', 'preparing', 'running')
            """, new { taskKey, userId });

        if (affected == 0)
        {
            return AiTaskEditResult.Conflict("그 사이에 상태가 바뀌었습니다. 다시 읽으십시오.");
        }

        // **종은 맡기고 간다.** 여기서 기다리면 AMQP 연결 한 번이 사람이 누른
        // 단추에 그대로 붙는다 — 요청은 이미 DB 에 있고, 종이 늦거나 못 가도
        // 실행기의 안전망 조회가 집는다(설계 6.6).
        queue.Ring(taskKey);

        return AiTaskEditResult.Ok(await GetAsync(taskKey));
    }

    /// <summary>
    /// 취소를 요청한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>아직 아무도 안 집어 간 것(<c>queued</c>)은 그 자리에서 취소된다.</b>
    /// 이미 도는 중이면 요청여부만 <c>cancel_requested</c> 로 남기고 상태는
    /// 두는데, <b>실제로 돌고 있는 CLI 를 화면에서 지우지 않기 위해서</b>다.
    /// 실행기가 그 표시를 보고 멈춘 뒤 상태를 바꾼다(2단계).
    /// </para>
    /// </remarks>
    public async Task<AiTaskEditResult> CancelAsync(long taskKey, string? userId)
    {
        var current = await GetAsync(taskKey);

        if (current is null)
        {
            return AiTaskEditResult.NotFound();
        }

        if (AiTaskStatus.IsFinal(current.TaskStatus) && current.RequestFlag == AiTaskFlag.None)
        {
            return AiTaskEditResult.Conflict("이미 끝난 작업입니다.");
        }

        using var db = Open();

        await db.ExecuteAsync("""
            UPDATE projmng.ai_task
               SET request_flag = CASE WHEN task_status = 'queued' THEN 'none'
                                       ELSE 'cancel_requested' END,
                   task_status  = CASE WHEN task_status = 'queued' THEN 'canceled'
                                       ELSE task_status END,
                   finished_at  = CASE WHEN task_status = 'queued' THEN now()
                                       ELSE finished_at END,
                   row_version  = row_version + 1,
                   mod_id       = @userId,
                   mod_dt       = now()
             WHERE task_key = @taskKey AND is_deleted = false
            """, new { taskKey, userId });

        return AiTaskEditResult.Ok(await GetAsync(taskKey));
    }

    /// <summary>
    /// <b>끝난 작업에 이어서 지시한다</b>(설계 8-3).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 새 작업을 만들지 않는다 — <b>같은 작업에 실행이 하나 더 붙는다.</b>
    /// 「해당 요청건에 이어서」라는 말 그대로이고, 이력이 한 줄에 모인다.
    /// </para>
    /// <para>
    /// <b>요약만으로는 절반이다.</b> 요약에는 「A 를 고쳤다」고 적혀 있는데
    /// 새 작업공간에는 그 변경이 없으면, AI 는 없는 것을 있다고 믿고 쌓거나
    /// 했던 일을 다시 한다. 그래서 <b>push 했느냐</b>를 프롬프트에 함께 적는다.
    /// </para>
    /// <para>
    /// 그리고 <b>요약을 믿지 않는다.</b> 요약은 AI 의 주장이라 「다 고쳤다」고
    /// 해 놓고 한 줄도 안 바뀐 경우가 있다. 그래서 <b>기계가 잰 값</b>
    /// (종료 코드 · 바뀐 파일 · 브랜치)을 나란히 싣는다 — 어긋나면 그 어긋남이
    /// 프롬프트 안에서 바로 보인다.
    /// </para>
    /// <para>
    /// <b>이 회차부터 다른 AI 에게 맡길 수 있다</b>(<paramref name="runnerKind"/>).
    /// 한 AI 가 두 번 실패한 것을 세 번째도 같은 AI 에게 시키는 것이 흔한
    /// 헛걸음이라서다 — 이어서 지시는 지난 진행을 프롬프트에 싣고 가므로
    /// <b>갈아탄 쪽이 처음부터 다시 읽지 않는다.</b> 안 보내면 지난 회차의
    /// 실행기가 그대로 간다.
    /// </para>
    /// </remarks>
    public async Task<AiTaskEditResult> ContinueAsync(
        long taskKey, string? addition, string? runnerKind, string? userId)
    {
        if (string.IsNullOrWhiteSpace(addition))
        {
            return AiTaskEditResult.Conflict("이어서 시킬 내용을 적으십시오.");
        }

        var current = await GetAsync(taskKey);

        if (current is null)
        {
            return AiTaskEditResult.NotFound();
        }

        if (AiTaskStatus.IsBusy(current.TaskStatus))
        {
            return AiTaskEditResult.Conflict("아직 돌고 있습니다. 끝난 뒤에 이어서 시키십시오.");
        }

        // **이 회차만 다른 AI 에게 맡길 수 있다.** 안 보내면 지난 회차의
        // 실행기를 그대로 쓴다 — 화면이 늘 값을 싣는다는 보장이 없고,
        // 빈 값으로 덮으면 그 건은 아무 실행기도 집어 가지 못한다.
        var kind = string.IsNullOrWhiteSpace(runnerKind)
            ? current.RunnerKind
            : runnerKind.Trim();

        // 둘 다 비어 있을 리는 없지만(등록·수정이 <see cref="NormalizeAsync"/> 에서
        // 채운다) 비었다면 그쪽과 <b>같은 기본값</b>으로 떨어진다.
        if (string.IsNullOrWhiteSpace(kind))
        {
            kind = "claude";
        }

        // 대상이 허용하지 않는 AI 는 여기서 막는다. 뒤이어 도는
        // <see cref="RequestAsync"/> 도 같은 것을 보지만, 거기서 걸리면
        // **본문은 이미 이어붙은 채로 남고 요청만 안 나간 상태**가 된다.
        if (!string.Equals(kind, current.RunnerKind, StringComparison.OrdinalIgnoreCase)
            && !RunnerAllowed(current.TargetRunnerKinds, kind))
        {
            return AiTaskEditResult.Conflict(
                $"고른 대상은 '{kind}' 실행기를 허용하지 않습니다.");
        }

        using var db = Open();

        // 지난 진행. 오래된 것부터 읽는다 — 사람이 읽는 순서다.
        var past = (await db.QueryAsync<PastTurn>("""
            SELECT r.seq AS Seq, r.run_status AS RunStatus, r.exit_code AS ExitCode,
                   r.started_at AS StartedAt, r.diff_stat AS DiffStat,
                   r.instruction AS Instruction,
                   COALESCE(r.summary_text, r.result_text) AS Summary,
                   r.git_branch AS GitBranch
              FROM projmng.ai_task_run r
             WHERE r.task_key = @taskKey AND r.finished_at IS NOT NULL
             ORDER BY r.seq
            """, new { taskKey })).ToList();

        var body = BuildContinuation(past, addition!, current.PushedCommit);

        var affected = await db.ExecuteAsync("""
            UPDATE projmng.ai_task
               SET contents     = @body,
                   runner_kind  = @kind,
                   task_status  = 'idle',
                   request_flag = 'none',
                   last_error   = NULL,
                   notify_error = NULL,
                   row_version  = row_version + 1,
                   mod_id       = @userId,
                   mod_dt       = now()
             WHERE task_key = @taskKey AND is_deleted = false
            """, new { taskKey, body, kind, userId });

        return affected == 0
            ? AiTaskEditResult.Conflict("그 사이에 상태가 바뀌었습니다.")
            : AiTaskEditResult.Ok(await GetAsync(taskKey));
    }

    /// <summary>
    /// <b>작업을 사용자 확인 완료 처리한다.</b>
    /// </summary>
    public async Task<AiTaskEditResult> ConfirmAsync(long taskKey, string? userId)
    {
        var current = await GetAsync(taskKey);

        if (current is null)
        {
            return AiTaskEditResult.NotFound();
        }

        using var db = Open();

        var affected = await db.ExecuteAsync("""
            UPDATE projmng.ai_task
               SET user_confirmed = true,
                   row_version    = row_version + 1,
                   mod_id         = @userId,
                   mod_dt         = now()
             WHERE task_key = @taskKey AND is_deleted = false
            """, new { taskKey, userId });

        return affected == 0
            ? AiTaskEditResult.Conflict("그 사이에 상태가 바뀌었습니다.")
            : AiTaskEditResult.Ok(await GetAsync(taskKey));
    }

    /// <summary>
    /// <b>실패한 작업을 수동으로 다시 요청한다.</b>
    /// 추가 지시사항이 있으면 본문 뒤에 덧붙이고, attempt_count 를 0으로 되돌려 다시 queued 로 넣는다.
    /// </summary>
    public async Task<AiTaskEditResult> RetryAsync(long taskKey, string? addition, string? userId)
    {
        var current = await GetAsync(taskKey);

        if (current is null)
        {
            return AiTaskEditResult.NotFound();
        }

        if (AiTaskStatus.IsBusy(current.TaskStatus))
        {
            return AiTaskEditResult.Conflict("이미 대기 중이거나 돌고 있습니다.");
        }

        var newContents = current.Contents;

        if (!string.IsNullOrWhiteSpace(addition))
        {
            newContents = string.IsNullOrWhiteSpace(current.Contents)
                ? addition.Trim()
                : $"{current.Contents.TrimEnd()}\n\n---\n## 재시도 추가 지시\n\n{addition.Trim()}";
        }

        using var db = Open();

        var affected = await db.ExecuteAsync("""
            UPDATE projmng.ai_task
               SET contents      = @newContents,
                   request_flag  = 'requested',
                   task_status   = 'queued',
                   requested_at  = now(),
                   started_at    = NULL,
                   finished_at   = NULL,
                   duration_ms   = NULL,
                   last_error    = NULL,
                   notify_error  = NULL,
                   attempt_count = 0,
                   row_version   = row_version + 1,
                   mod_id        = @userId,
                   mod_dt        = now()
             WHERE task_key   = @taskKey
               AND is_deleted = false
               AND task_status NOT IN ('queued', 'preparing', 'running')
            """, new { taskKey, newContents, userId });

        if (affected == 0)
        {
            return AiTaskEditResult.Conflict("그 사이에 상태가 바뀌었습니다. 다시 읽으십시오.");
        }

        queue.Ring(taskKey);

        return AiTaskEditResult.Ok(await GetAsync(taskKey));
    }

    /// <summary>
    /// 이어가기 지시문을 조립한다.
    /// </summary>
    /// <remarks>
    /// <b>길이가 터지는 것을 막는다.</b> 요약을 계속 앞에 붙이면 다섯 턴째에
    /// 지난 이야기가 새 지시보다 커지고, 그러면 AI 가 새 지시보다 지난
    /// 이야기에 무게를 둔다. 최근 셋은 전문, 그 이전은 한 줄로 접고
    /// <b>접었다는 것을 적는다</b> — 조용히 자르면 그것이 전부인 줄 안다.
    /// </remarks>
    private static string BuildContinuation(
        List<PastTurn> past, string addition, string? pushedCommit)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("## 이전까지의 진행");
        sb.AppendLine();

        if (past.Count == 0)
        {
            sb.AppendLine("(지난 실행이 없습니다.)");
            sb.AppendLine();
        }

        var folded = Math.Max(0, past.Count - 3);

        if (folded > 0)
        {
            sb.AppendLine($"> 앞의 {folded}건은 줄여서 적었습니다.");
            sb.AppendLine();

            foreach (var t in past.Take(folded))
            {
                sb.AppendLine($"- {t.Seq}차 ({t.RunStatus}): {FirstLine(t.Instruction)}");
            }

            sb.AppendLine();
        }

        foreach (var t in past.Skip(folded))
        {
            sb.AppendLine($"### {t.Seq}차 ({t.StartedAt:yyyy-MM-dd HH:mm}, {t.RunStatus})");
            sb.AppendLine();
            sb.AppendLine($"지시: {FirstLine(t.Instruction)}");
            sb.AppendLine();
            sb.AppendLine("AI 요약:");
            sb.AppendLine(Clip(t.Summary, 1500));
            sb.AppendLine();

            // **기계가 잰 값.** AI 가 쓴 것이 아니다 — 요약과 어긋나면
            // 그 어긋남이 여기서 바로 보인다.
            sb.AppendLine("기록된 사실:");
            sb.AppendLine($"  · 종료 코드 {t.ExitCode}");
            sb.AppendLine(string.IsNullOrWhiteSpace(t.DiffStat)
                ? "  · 바뀐 파일 없음"
                : $"  · {LastLine(t.DiffStat)}");

            if (!string.IsNullOrWhiteSpace(t.GitBranch))
            {
                sb.AppendLine($"  · 브랜치 {t.GitBranch}");
            }

            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(pushedCommit))
        {
            var head = pushedCommit[..Math.Min(8, pushedCommit.Length)];

            sb.AppendLine($"> 지난 결과는 이미 올라갔습니다({head}). 지금 작업 폴더에 그 변경이 들어 있습니다.");
            sb.AppendLine();
        }

        sb.AppendLine("## 이번에 할 일");
        sb.AppendLine();
        sb.AppendLine(addition.Trim());

        return sb.ToString();
    }

    private static string FirstLine(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "(없음)";
        }

        var line = text.Split('\n').FirstOrDefault(l => l.Trim().Length > 0)?.Trim();
        return Clip(line, 120);
    }

    private static string LastLine(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : text.TrimEnd().Split('\n')[^1].Trim();

    private static string Clip(string? text, int max) =>
        string.IsNullOrWhiteSpace(text) ? "(없음)"
        : text.Length <= max ? text.Trim()
        : text[..max].Trim() + " …(줄임)";

    /// <summary>지난 실행 한 번. 이어가기 프롬프트를 짤 때만 쓴다.</summary>
    private sealed class PastTurn
    {
        public int Seq { get; set; }
        public string? RunStatus { get; set; }
        public int? ExitCode { get; set; }
        public DateTime? StartedAt { get; set; }
        public string? DiffStat { get; set; }
        public string? Instruction { get; set; }
        public string? Summary { get; set; }
        public string? GitBranch { get; set; }
    }

    /// <summary>
    /// 지운다. <b>행을 없애지 않는다</b> — 실행 이력이 그 번호를 가리키고 있다.
    /// </summary>
    public async Task<AiTaskEditResult> DeleteAsync(long taskKey, string? userId)
    {
        var current = await GetAsync(taskKey);

        if (current is null)
        {
            return AiTaskEditResult.NotFound();
        }

        if (AiTaskStatus.IsBusy(current.TaskStatus))
        {
            return AiTaskEditResult.Conflict("돌고 있는 작업은 지울 수 없습니다. 먼저 취소하십시오.");
        }

        using var db = Open();

        await db.ExecuteAsync("""
            UPDATE projmng.ai_task
               SET is_deleted = true, mod_id = @userId, mod_dt = now()
             WHERE task_key = @taskKey
            """, new { taskKey, userId });

        return AiTaskEditResult.Ok(null);
    }

    // ── 안쪽 ────────────────────────────────────────────────

    /// <summary>
    /// 빈 값을 채우고 말이 안 되는 조합을 바로잡는다.
    /// </summary>
    /// <remarks>
    /// <b>제목을 서버가 만든다.</b> 비워 두고 저장할 수 있어야 편집기에 바로
    /// 쓰기 시작할 수 있는데, 목록에 「제목 없음」이 쌓이면 고르지 못한다.
    /// 본문 첫 제목 줄 → 첫 줄 → 날짜 순으로 떨어진다.
    /// (LLM 으로 한 줄 요약을 받는 것은 설계 8장이고, 그것은 나중에 붙인다.)
    /// </remarks>
    private async Task NormalizeAsync(AiTask item)
    {
        item.Title = string.IsNullOrWhiteSpace(item.Title)
            ? MakeTitle(item.Contents)
            : item.Title.Trim();

        if (item.Title.Length > 200)
        {
            item.Title = item.Title[..200];
        }

        if (string.IsNullOrWhiteSpace(item.ContentFormat))
        {
            item.ContentFormat = "markdown";
        }

        if (string.IsNullOrWhiteSpace(item.RunnerKind))
        {
            item.RunnerKind = "claude";
        }

        if (string.IsNullOrWhiteSpace(item.NotifyWhen))
        {
            item.NotifyWhen = "always";
        }

        item.TimeoutMinutes = Math.Clamp(item.TimeoutMinutes, 1, 24 * 60);
        item.AttemptMax = item.AttemptMax <= 0 ? 3 : Math.Clamp(item.AttemptMax, 1, 5);

        // **대상이 허용하지 않으면 push 를 켤 수 없다.** 화면에서도 막지만
        // 여기서 한 번 더 본다 — 이 값 하나가 운영 배포를 일으킨다.
        if (item.AutoPush && item.TargetKey is { } key)
        {
            var target = await targets.GetAsync(key);

            if (target is null || !target.AllowPush)
            {
                item.AutoPush = false;
            }
        }
        else if (item.TargetKey is null)
        {
            item.AutoPush = false;
        }
    }

    /// <summary>본문에서 제목을 만든다. 첫 <c>#</c> 줄 → 첫 줄 → 날짜.</summary>
    private static string MakeTitle(string? contents)
    {
        if (!string.IsNullOrWhiteSpace(contents))
        {
            foreach (var raw in contents.Split('\n'))
            {
                var line = raw.Trim();

                if (line.Length == 0)
                {
                    continue;
                }

                // 마크다운 제목 줄이면 `#` 를 턴다.
                var text = line.TrimStart('#', ' ', '\t').Trim();

                if (text.Length > 0)
                {
                    return text.Length > 60 ? text[..60] : text;
                }
            }
        }

        return $"제목 없는 작업 {DateTime.Now:yyyy-MM-dd HH:mm}";
    }

    private static bool RunnerAllowed(string? kinds, string? runnerKind)
    {
        if (string.IsNullOrWhiteSpace(runnerKind))
        {
            return false;
        }

        return (kinds ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(runnerKind, StringComparer.OrdinalIgnoreCase);
    }
}

/// <summary>
/// 고치기·요청·취소의 결과. <b>「안 됐다」를 이유와 함께 돌려주려고 있다.</b>
/// </summary>
/// <remarks>
/// <c>null</c> 하나로 돌려주면 「없는 번호」와 「지금은 안 되는 상태」가
/// 구분되지 않는다. 앞엣것은 404 고 뒤엣것은 사람에게 할 말이 있는 409 다.
/// </remarks>
public sealed class AiTaskEditResult
{
    public bool Found { get; private init; } = true;

    /// <summary>상태 때문에 못 한 경우의 이유. 없으면 성공이다.</summary>
    public string? ConflictMessage { get; private init; }

    public AiTask? Item { get; private init; }

    public bool IsOk => Found && ConflictMessage is null;

    public static AiTaskEditResult Ok(AiTask? item) => new() { Item = item };

    public static AiTaskEditResult NotFound() => new() { Found = false };

    public static AiTaskEditResult Conflict(string message) => new() { ConflictMessage = message };
}
