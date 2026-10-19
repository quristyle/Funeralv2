using System.Data;
using System.Text.Json;

using Dapper;
using JSini.Shared.Infrastructure.Time;
using Npgsql;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>AI 작업(projmng.ai_task). 1단계는 글 저장과 상태 전이까지이고 실행기는 2단계다.</summary>
/// 설계는 docs/ai-task-runner.md 를 본다.
public sealed class AiTaskService(
    IConfiguration configuration, AiTargetService targets, AiTaskQueue queue,
    AiRequestAlerter alerts, AiTaskFileService files, AiTurnTitler turnTitler)
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
        -- 처리차수. 실행 이력의 최대 seq 또는 진행/대기 중인 차수.
        COALESCE(( SELECT MAX(r.seq)::int FROM projmng.ai_task_run r
                    WHERE r.task_key = a.task_key ), 0)
        + CASE WHEN a.task_status IN ('queued', 'preparing')
                AND EXISTS (
                    SELECT 1 FROM projmng.ai_task_run r2
                     WHERE r2.task_key = a.task_key AND r2.finished_at IS NOT NULL
                )
                AND NOT EXISTS (
                    SELECT 1 FROM projmng.ai_task_run r3
                     WHERE r3.task_key = a.task_key AND r3.finished_at IS NULL
                )
               THEN 1
               WHEN NOT EXISTS (
                    SELECT 1 FROM projmng.ai_task_run r4
                     WHERE r4.task_key = a.task_key
               ) THEN 1
               ELSE 0 END AS TurnCount,
        -- 붙은 첨부 개수. **목록이 배지 하나를 세우는 값**이라 개수만 센다 —
        -- 이름까지 끌고 오면 카드 열 장을 그리는 조회가 그만큼 무거워지고,
        -- 정작 이름은 눌러서 여는 자리에서만 쓴다.
        ( SELECT COUNT(*)::int FROM projmng.ai_task_file f
           WHERE f.task_key = a.task_key ) AS FileCount,
        -- 오간 남길말 수와 **올린 사람이 아직 안 읽은 수**. 목록이 배지
        -- 둘을 세운다 — 「AI 작업 요청」 화면에서 「관리자가 답했나」는
        -- 상태 배지가 답하지 못하는 물음이다(상태는 그대로 「접수 대기」다).
        --
        -- 여기서도 개수만 센다. 첫 줄까지 끌고 오면 목록 한 번에 본문
        -- 스물이 더 실리는데, 정작 그 글은 눌러서 여는 자리에서만 읽는다.
        ( SELECT COUNT(*)::int FROM projmng.ai_task_note n
           WHERE n.task_key = a.task_key AND n.is_deleted = false ) AS NoteCount,
        ( SELECT COUNT(*)::int FROM projmng.ai_task_note n
           WHERE n.task_key = a.task_key AND n.is_deleted = false
             AND n.read_dt IS NULL ) AS UnreadNoteCount,
        -- 차수별 제목과 상태 목록. 차수가 2회 이상일 때 모바일 카드에 차수별 제목을 표시한다.
        ( SELECT json_agg(json_build_object(
                   'no', r.seq,
                   'title', r.turn_title,
                   'status', r.run_status
                 ) ORDER BY r.seq)::text
            FROM projmng.ai_task_run r
           WHERE r.task_key = a.task_key ) AS TurnTitlesJson,
        a.target_key      AS TargetKey,
        b.target_nm       AS TargetNm,
        b.target_kind     AS TargetKind,
        b.repo_url        AS TargetRepoUrl,
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
        a.title_auto      AS TitleAuto,
        a.title_run_key   AS TitleRunKey,
        -- 아직 실행 줄이 없는 차수의 제목. **화면이 그 차수의 머리줄에 건다** —
        -- 보냈는데 아무도 안 집어 간 동안에도 「무엇을 시킨 차수인가」가 보여야
        -- 한다(`AiTurnTitler`).
        a.next_turn_title              AS NextTurnTitle,
        a.pending_continue_addition    AS PendingContinueAddition,
        a.pending_continue_runner_kind AS PendingContinueRunnerKind,
        a.pending_continue_user_id     AS PendingContinueUserId,
        a.pending_continue_at          AS PendingContinueAt,
        a.is_user_request              AS IsUserRequest,
        a.cre_id                       AS CreId,
        a.cre_dt                       AS CreDt,
        a.mod_id                       AS ModId,
        a.mod_dt                       AS ModDt
        """;

    /// <summary>목록. 준 조건만 걸리고 본문까지 함께 읽는다.</summary>
    /// excludeDone 은 완료·취소만 뺀다. 실패·시간초과·중단은 남긴다.
    public async Task<List<AiTask>> ListAsync(
        string? taskStatus = null, string? requestFlag = null, long? targetKey = null,
        string? keyword = null, long? taskKey = null, bool? userConfirmed = null,
        string? creId = null, bool? userRequest = null, bool excludeDone = false)
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
               -- **올린 사람으로 좁힌다.** 「내가 올린 요청」 화면이 이 조건
               -- 하나로 남의 건을 못 보게 한다 — 화면에서 거르면 자료는 이미
               -- 브라우저까지 다 간 뒤다.
               AND (@creId = '' OR a.cre_id = @creId)
               AND (@userRequest::boolean IS NULL OR a.is_user_request = @userRequest)
               AND (@keyword = '' OR a.title ILIKE '%' || @keyword || '%'
                                  OR a.contents ILIKE '%' || @keyword || '%')
               -- 끝난 건 빼기. **실패한 건은 남는다** — 까닭은 파라미터 설명에.
               AND (@excludeDone = false OR a.task_status NOT IN ('succeeded', 'canceled'))
             ORDER BY a.task_key DESC
            """, new
        {
            taskKey,
            taskStatus = taskStatus ?? string.Empty,
            requestFlag = requestFlag ?? string.Empty,
            targetKey,
            keyword = keyword ?? string.Empty,
            userConfirmed,
            creId = creId ?? string.Empty,
            userRequest,
            excludeDone,
        });

        var list = rows.AsList();
        foreach (var item in list)
        {
            if (!string.IsNullOrWhiteSpace(item.TurnTitlesJson))
            {
                try
                {
                    item.TurnTitles = JsonSerializer.Deserialize<List<AiTaskTurnTitleDto>>(
                        item.TurnTitlesJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
                }
                catch
                {
                    item.TurnTitles = [];
                }
            }

            // 1차 제목이 비어있으면 건의 대표 제목으로 보완
            if (item.TurnTitles.Count > 0 && item.TurnTitles[0].No == 1 && string.IsNullOrWhiteSpace(item.TurnTitles[0].Title))
            {
                item.TurnTitles[0].Title = item.Title;
            }

            // 아직 실행 줄이 없는 차수(대기 중인 차수)가 있을 때:
            var hasUnstartedTurn = !string.IsNullOrWhiteSpace(item.NextTurnTitle)
                || (item.IsBusy && (item.TurnTitles.Count == 0 || item.FinishedAt is not null));

            if (hasUnstartedTurn)
            {
                var nextNo = item.TurnTitles.Count == 0 ? 1 : item.TurnTitles.Max(t => t.No) + 1;
                var nextTitle = !string.IsNullOrWhiteSpace(item.NextTurnTitle)
                    ? item.NextTurnTitle
                    : (nextNo == 1 ? item.Title : null);

                item.TurnTitles.Add(new AiTaskTurnTitleDto
                {
                    No = nextNo,
                    Title = nextTitle,
                    Status = item.TaskStatus,
                });
            }

            foreach (var t in item.TurnTitles)
            {
                if (!string.IsNullOrWhiteSpace(t.Title))
                {
                    t.Title = t.Title.Trim();
                }
            }
        }

        return list;
    }

    public async Task<AiTask?> GetAsync(long taskKey)
        => (await ListAsync(taskKey: taskKey)).FirstOrDefault();

    public async Task<AiTask?> CreateAsync(AiTask item, string? userId)
    {
        var titleAuto = string.IsNullOrWhiteSpace(item.Title);
        item.TitleAuto = titleAuto;

        await NormalizeAsync(item);

        using var db = Open();

        var key = await db.ExecuteScalarAsync<long>("""
            INSERT INTO projmng.ai_task
                 ( title, title_auto, title_run_key, contents, content_format, target_key, target_ref,
                   runner_kind, request_flag, task_status, priority,
                   timeout_minutes, attempt_max, auto_push,
                   notify_email, notify_pwa, notify_to, notify_when,
                   is_user_request, row_version, cre_id, cre_dt )
            VALUES ( @Title, @TitleAuto, NULL, @Contents, @ContentFormat, @TargetKey, @TargetRef,
                     @RunnerKind, 'none', 'idle', @Priority,
                     @TimeoutMinutes, @AttemptMax, @AutoPush,
                     @NotifyEmail, @NotifyPwa, @NotifyTo, @NotifyWhen,
                     @IsUserRequest, 1, @userId, now() )
            RETURNING task_key
            """, new
        {
            item.Title, item.TitleAuto, item.Contents, item.ContentFormat, item.TargetKey, item.TargetRef,
            item.RunnerKind, item.Priority, item.TimeoutMinutes, item.AttemptMax,
            item.AutoPush, item.NotifyEmail, item.NotifyPwa, item.NotifyTo, item.NotifyWhen,
            item.IsUserRequest, userId,
        });

        // 미리 올린 첨부를 이 작업에 묶는다. 번호가 생긴 뒤라야 하므로 여기서 묶는다.
        // 못 묶어도 되돌리지 않는다 — 사진 한 장 때문에 지시가 안 나가는 것이 더 나쁘다.
        if (item.FileKeys is { Length: > 0 } fileKeys)
        {
            await files.BindAsync(key, fileKeys, userId);
        }

        return await GetAsync(key);
    }

    // 일반 사용자 요청 구간. 관리자 입구와 갈라 둔 까닭은 이 화면 사용자가 관리자가 아니기 때문이다.
    // 셋 다 cre_id 를 WHERE 에 넣는다 — 읽고 판단한 뒤 고치면 사이가 비고 남의 건을 건드릴 수 있다.

    /// <summary>일반 사용자가 지시를 적는다. 저장만 되고 아무 데서도 돌지 않는다.</summary>
    /// 대상·push·알림 등 돌게 하는 값은 화면이 무엇을 보내든 서버가 지운다.
    public async Task<AiTask?> CreateUserRequestAsync(AiTask item, string? userId, string? userName = null)
    {
        // **대상을 비운다.** 대상이 있으면 관리자가 요청만 누르면 되는데,
        // 일반 사용자가 고른 자리에서 그대로 돌아가는 것이 이 화면의 전제와
        // 어긋난다(어디서 돌릴지는 관리자가 정한다).
        item.TargetKey = null;
        item.TargetRef = null;
        item.AutoPush = false;

        // **AI 도 고르지 않는다.** 화면에는 그 칸이 없지만 이 경로는 로그인한
        // 누구에게나 열려 있어 본문에 실어 보낼 수 있다. 남겨 두면 관리자가
        // 고른 대상이 그 AI 를 허용하지 않을 때 **요청 단계에서야** 막힌다.
        // 기본값은 `NormalizeAsync` 가 채운다.
        item.RunnerKind = null;

        // 알림도 끈다. 이 건이 끝나는 시점은 관리자가 실제로 시킨 뒤이고,
        // 그때의 알림 설정은 관리자가 정한다.
        item.NotifyEmail = false;
        item.NotifyPwa = false;
        item.NotifyTo = null;

        item.IsUserRequest = true;

        var created = await CreateAsync(item, userId);

        // **관리자에게 알린다.** 이 건은 저장만 되고 아무 데서도 안 돌므로,
        // 알려 주지 않으면 관리자가 화면을 스스로 열어 볼 때까지 그대로 쌓인다
        // (`AiRequestAlerter` 머리말). 기다리지 않고, 못 보내도 요청은 살아 있다.
        alerts.Fire(created, userName);

        return created;
    }

    /// <summary>자기가 올린 요청을 고친다. 아직 아무도 안 건드린 것만.</summary>
    /// row_version 을 조건에 넣는다 — 관리자가 손본 사이에 저장하면 그 손질이 소리 없이 덮인다.
    public async Task<AiTaskEditResult> UpdateUserRequestAsync(
        long taskKey, AiTask item, string? userId)
    {
        var current = await FindOwnAsync(taskKey, userId);

        if (current is null)
        {
            return AiTaskEditResult.NotFound();
        }

        if (!IsStillEditable(current))
        {
            return AiTaskEditResult.Conflict(
                "관리자가 이미 처리를 시작한 요청입니다. 더 고칠 수 없습니다.");
        }

        item.TitleAuto = string.IsNullOrWhiteSpace(item.Title);
        item.Title = string.IsNullOrWhiteSpace(item.Title)
            ? MakeTitle(item.Contents)
            : item.Title.Trim();

        if (item.Title.Length > 200)
        {
            item.Title = item.Title[..200];
        }

        using var db = Open();

        var affected = await db.ExecuteAsync("""
            UPDATE projmng.ai_task
               SET title      = @Title,
                   title_auto = @TitleAuto,
                   contents   = @Contents,
                   row_version = row_version + 1,
                   mod_id     = @userId,
                   mod_dt     = now()
             WHERE task_key     = @taskKey
               AND is_deleted   = false
               AND cre_id       = @userId
               AND request_flag = 'none'
               AND task_status  = 'idle'
               AND row_version  = @RowVersion
            """, new { taskKey, item.Title, item.TitleAuto, item.Contents, item.RowVersion, userId });

        return affected == 0
            ? AiTaskEditResult.Conflict("다른 사람이 먼저 고쳤습니다. 다시 읽은 뒤 저장하십시오.")
            : AiTaskEditResult.Ok(await GetAsync(taskKey));
    }

    /// <summary>
    /// <b>자기가 올린 요청을 거둬들인다.</b> 아직 아무도 안 건드린 것만.
    /// </summary>
    public async Task<AiTaskEditResult> DeleteUserRequestAsync(long taskKey, string? userId)
    {
        var current = await FindOwnAsync(taskKey, userId);

        if (current is null)
        {
            return AiTaskEditResult.NotFound();
        }

        if (!IsStillEditable(current))
        {
            return AiTaskEditResult.Conflict(
                "관리자가 이미 처리를 시작한 요청입니다. 지울 수 없습니다.");
        }

        using var db = Open();

        var affected = await db.ExecuteAsync("""
            UPDATE projmng.ai_task
               SET is_deleted = true, mod_id = @userId, mod_dt = now()
             WHERE task_key     = @taskKey
               AND is_deleted   = false
               AND cre_id       = @userId
               AND request_flag = 'none'
               AND task_status  = 'idle'
            """, new { taskKey, userId });

        return affected == 0
            ? AiTaskEditResult.Conflict("그 사이에 상태가 바뀌었습니다. 다시 읽으십시오.")
            : AiTaskEditResult.Ok(null);
    }

    /// <summary>내가 올린 건인가. 남의 건은 「없다」로 답해 그 번호에 무엇이 있는지 알리지 않는다.</summary>
    private async Task<AiTask?> FindOwnAsync(long taskKey, string? userId)
    {
        var current = await GetAsync(taskKey);

        return current is not null
               && !string.IsNullOrWhiteSpace(userId)
               && string.Equals(current.CreId, userId, StringComparison.Ordinal)
            ? current
            : null;
    }

    /// <summary>올린 사람이 아직 고칠 수 있나. 관리자가 손대기 전까지다.</summary>
    /// 대상이 채워져 있으면 관리자가 읽고 판단한 글과 실제 도는 글이 달라지므로 막는다.
    private static bool IsStillEditable(AiTask t) =>
        t.RequestFlag == AiTaskFlag.None
        && t.TaskStatus == AiTaskStatus.Idle
        && t.TargetKey is null
        && t.LastRunKey is null;

    /// <summary>고친다. 도는 중에는 본문을 고칠 수 없다.</summary>
    /// row_version 을 조건에 넣어 동시 편집을 막는다. 검사 후 갱신으로 나누면 그 사이 끼어드는 것을 못 막는다.
    public async Task<AiTaskEditResult> UpdateAsync(long taskKey, AiTask item, string? userId)
    {
        var titleAuto = string.IsNullOrWhiteSpace(item.Title);
        item.TitleAuto = titleAuto;

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
                   title_auto      = @TitleAuto,
                   title_run_key   = CASE WHEN @TitleAuto THEN NULL ELSE title_run_key END,
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
            taskKey, item.Title, item.TitleAuto, item.Contents, item.ContentFormat, item.TargetKey,
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

    /// <summary>작업을 요청한다. requested·queued 로 바꾸고 preparing 은 실행기가 집을 때 붙인다.</summary>
    /// 조건을 WHERE 에 넣어 한 문장으로 바꾼다 — 세 걸음으로 나누면 두 번 누른 사이 상태가 바뀔 수 있다.
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
               SET request_flag   = 'requested',
                   task_status    = 'queued',
                   user_confirmed = false,
                   requested_at   = now(),
                   started_at     = NULL,
                   finished_at    = NULL,
                   duration_ms    = NULL,
                   last_error     = NULL,
                   notify_error   = NULL,
                   attempt_count  = 0,
                   row_version    = row_version + 1,
                   mod_id         = @userId,
                   mod_dt         = now()
             WHERE task_key   = @taskKey
               AND is_deleted = false
               AND task_status NOT IN ('queued', 'preparing', 'running')
            """, new { taskKey, userId });

        if (affected == 0)
        {
            return AiTaskEditResult.Conflict("그 사이에 상태가 바뀌었습니다. 다시 읽으십시오.");
        }

        // 종은 맡기고 간다. 여기서 기다리면 AMQP 연결 한 번이 단추에 붙는다 — 실행기 안전망 조회가 집는다.
        queue.Ring(taskKey);

        // 차수 제목도 맡긴다. 종을 울린 뒤에 해야 집어가기와 경합해도 제목이 실행 줄을 찾아간다.
        _ = turnTitler.PrepareAsync(taskKey);

        return AiTaskEditResult.Ok(await GetAsync(taskKey));
    }

    /// <summary>취소를 요청한다. queued 는 그 자리에서 취소하고, 도는 중이면 cancel_requested 로 남긴다.</summary>
    /// 도는 CLI 를 화면에서 먼저 지우지 않으려고 상태는 실행기가 멈춘 뒤 바꾼다.
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

    /// <summary>끝난 작업에 이어서 지시한다. 새 작업이 아니라 같은 작업에 실행이 하나 더 붙는다.</summary>
    /// 요약은 AI 의 주장이라 믿지 않고 기계가 잰 값(종료 코드·바뀐 파일·브랜치)을 나란히 싣는다. runnerKind 로 다른 AI 에게 맡길 수 있다.
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
               SET contents                     = @body,
                   runner_kind                  = @kind,
                   task_status                  = 'idle',
                   request_flag                 = 'none',
                   user_confirmed               = false,
                   pending_continue_addition    = NULL,
                   pending_continue_runner_kind = NULL,
                   pending_continue_user_id     = NULL,
                   pending_continue_at          = NULL,
                   last_error                   = NULL,
                   notify_error                 = NULL,
                   row_version                  = row_version + 1,
                   mod_id                       = @userId,
                   mod_dt                       = now()
             WHERE task_key = @taskKey AND is_deleted = false
            """, new { taskKey, body, kind, userId });

        return affected == 0
            ? AiTaskEditResult.Conflict("그 사이에 상태가 바뀌었습니다.")
            : AiTaskEditResult.Ok(await GetAsync(taskKey));
    }

    /// <summary>완료 후 이어서 지시할 내용을 예약·저장하거나 취소한다.</summary>
    public async Task<AiTaskEditResult> SetPendingContinueAsync(
        long taskKey, string? addition, string? runnerKind, string? userId)
    {
        var current = await GetAsync(taskKey);
        if (current is null)
        {
            return AiTaskEditResult.NotFound();
        }

        using var db = Open();

        if (string.IsNullOrWhiteSpace(addition))
        {
            // 예약 취소
            var affected = await db.ExecuteAsync("""
                UPDATE projmng.ai_task
                   SET pending_continue_addition    = NULL,
                       pending_continue_runner_kind = NULL,
                       pending_continue_user_id     = NULL,
                       pending_continue_at          = NULL,
                       row_version                  = row_version + 1,
                       mod_id                       = @userId,
                       mod_dt                       = now()
                 WHERE task_key = @taskKey AND is_deleted = false
                """, new { taskKey, userId });

            return affected == 0
                ? AiTaskEditResult.Conflict("그 사이에 상태가 바뀌었습니다.")
                : AiTaskEditResult.Ok(await GetAsync(taskKey));
        }

        var kind = string.IsNullOrWhiteSpace(runnerKind)
            ? current.RunnerKind
            : runnerKind.Trim();

        if (string.IsNullOrWhiteSpace(kind))
        {
            kind = "claude";
        }

        if (!string.Equals(kind, current.RunnerKind, StringComparison.OrdinalIgnoreCase)
            && !RunnerAllowed(current.TargetRunnerKinds, kind))
        {
            return AiTaskEditResult.Conflict(
                $"고른 대상은 '{kind}' 실행기를 허용하지 않습니다.");
        }

        var updated = await db.ExecuteAsync("""
            UPDATE projmng.ai_task
               SET pending_continue_addition    = @addition,
                   pending_continue_runner_kind = @kind,
                   pending_continue_user_id     = @userId,
                   pending_continue_at          = now(),
                   row_version                  = row_version + 1,
                   mod_id                       = @userId,
                   mod_dt                       = now()
             WHERE task_key = @taskKey AND is_deleted = false
            """, new { taskKey, addition = addition.Trim(), kind, userId });

        return updated == 0
            ? AiTaskEditResult.Conflict("그 사이에 상태가 바뀌었습니다.")
            : AiTaskEditResult.Ok(await GetAsync(taskKey));
    }

    /// <summary>작업을 사용자 확인 완료 처리한다.</summary>
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

    /// <summary>실패한 작업을 수동으로 다시 요청한다. 추가 지시는 본문 뒤에 덧붙이고 attempt_count 를 0 으로 되돌린다.</summary>
    public async Task<AiTaskEditResult> RetryAsync(long taskKey, string? addition, string? runnerKind, string? userId)
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

        var kind = string.IsNullOrWhiteSpace(runnerKind)
            ? current.RunnerKind
            : runnerKind.Trim();

        if (string.IsNullOrWhiteSpace(kind))
        {
            kind = "claude";
        }

        if (!string.Equals(kind, current.RunnerKind, StringComparison.OrdinalIgnoreCase)
            && !RunnerAllowed(current.TargetRunnerKinds, kind))
        {
            return AiTaskEditResult.Conflict(
                $"고른 대상은 '{kind}' 실행기를 허용하지 않습니다.");
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
               SET contents       = @newContents,
                   runner_kind    = @kind,
                   request_flag   = 'requested',
                   task_status    = 'queued',
                   user_confirmed = false,
                   requested_at   = now(),
                   started_at     = NULL,
                   finished_at    = NULL,
                   duration_ms    = NULL,
                   last_error     = NULL,
                   notify_error   = NULL,
                   attempt_count  = 0,
                   row_version    = row_version + 1,
                   mod_id         = @userId,
                   mod_dt         = now()
             WHERE task_key   = @taskKey
               AND is_deleted = false
               AND task_status NOT IN ('queued', 'preparing', 'running')
            """, new { taskKey, newContents, kind, userId });

        if (affected == 0)
        {
            return AiTaskEditResult.Conflict("그 사이에 상태가 바뀌었습니다. 다시 읽으십시오.");
        }

        queue.Ring(taskKey);

        return AiTaskEditResult.Ok(await GetAsync(taskKey));
    }

    /// <summary>이어가기 지시문을 조립한다.</summary>
    /// 최근 셋은 전문, 그 이전은 한 줄로 접고 접었다는 사실을 적는다 — 조용히 자르면 그것이 전부인 줄 안다.
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

    /// <summary>지운다. 행은 남긴다 — 실행 이력이 그 번호를 가리킨다.</summary>
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

    /// <summary>빈 값을 채우고 말이 안 되는 조합을 바로잡는다.</summary>
    /// 제목은 서버가 만든다. 본문 첫 제목 줄 → 첫 줄 → 날짜 순으로 떨어진다.
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

        // 0 을 그냥 Clamp 하면 **1분**이 된다 — 값을 안 실어 보낸 쪽이
        // 가장 짧은 제한을 받는 꼴이라, 아래 AttemptMax 와 같이 기본으로 되돌린다.
        item.TimeoutMinutes = item.TimeoutMinutes <= 0
            ? 120
            : Math.Clamp(item.TimeoutMinutes, 1, 24 * 60);
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

        // 사람이 목록에서 읽는 글자라 **한국 시각**으로 적는다. 시스템의
        // 시각은 UTC 지만(docs/utc-time.md) 이것은 저장값이 아니라 이름이다.
        return $"제목 없는 작업 {AppTime.ToKorea(AppTime.UtcNow):yyyy-MM-dd HH:mm}";
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

/// <summary>고치기·요청·취소의 결과. 안 됐을 때 이유를 함께 돌려준다.</summary>
/// null 하나로 돌려주면 없는 번호(404)와 지금은 안 되는 상태(409)가 구분되지 않는다.
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
