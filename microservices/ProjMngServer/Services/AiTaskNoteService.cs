using System.Data;

using Dapper;
using Npgsql;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>
/// AI 작업 요청에 오가는 <b>남길말</b> — <c>projmng.ai_task_note</c>.
/// </summary>
/// 일반 사용자의 요청은 관리자가 말을 붙여야 움직이므로, 말을 표에 남기고 상대에게 앱푸시를 보낸다. 보내는 일은 AiRequestAlerter.FireNote 가 하고 기다리지 않는다.
/// 말은 지시문(ai_task.contents)에 적지 않는다 — 실행기가 그 글을 그대로 지시로 읽기 때문이다. 관리자 경로는 번호만 받고, 일반 사용자 경로는 그 요청을 올린 사람인지 먼저 본다.
public sealed class AiTaskNoteService(
    IConfiguration configuration, AiTaskService tasks, AiRequestAlerter alerts)
{
    private readonly string _connectionString =
        configuration.GetConnectionString("jsini")
        ?? throw new InvalidOperationException("연결 문자열 'jsini' 가 없습니다.");

    private IDbConnection Open() => new NpgsqlConnection(_connectionString);

    /// <summary>한 줄이 담을 수 있는 글자 수. 화면도 같은 수로 막는다.</summary>
    public const int MaxLength = 2000;

    private const string Columns = """
        n.note_key AS NoteKey,
        n.task_key AS TaskKey,
        n.contents AS Contents,
        n.read_dt  AS ReadDt,
        n.cre_id   AS CreId,
        n.cre_dt   AS CreDt,
        -- 「올린 사람이 적은 줄인가」. **표에 칸으로 두지 않는다** —
        -- 굳혀 두면 나중에 역할이 바뀔 때 지난 줄이 거짓말을 한다.
        ( a.cre_id IS NOT NULL AND n.cre_id = a.cre_id ) AS IsOwner
        """;

    /// <summary>
    /// 한 건에 붙은 말 전부. <b>적은 순서대로</b> — 대화라서 순서가 뜻이다.
    /// </summary>
    public async Task<List<AiTaskNote>> ListAsync(long taskKey)
    {
        using var db = Open();

        var rows = await db.QueryAsync<AiTaskNote>($"""
            SELECT {Columns}
              FROM projmng.ai_task_note n
              JOIN projmng.ai_task a ON a.task_key = n.task_key
             WHERE n.task_key = @taskKey
               AND n.is_deleted = false
             ORDER BY n.note_key
            """, new { taskKey });

        return [.. rows];
    }

    /// <summary>
    /// <b>말을 하나 남긴다.</b> 남기면 상대에게 앱푸시가 간다.
    /// </summary>
    /// <param name="taskKey">어느 요청에.</param>
    /// <param name="contents">남길 말.</param>
    /// <param name="userId">적는 사람.</param>
    /// <param name="userName">적는 사람의 이름. 알림 본문에 쓴다.</param>
    /// 올린 사람이 스스로 적은 줄은 적는 순간 읽은 것으로 찍는다 — 자기 글 때문에 자기 화면에 새 말 배지가 서면 안 된다.
    public async Task<AiTaskNoteResult> AddAsync(
        long taskKey, string? contents, string? userId, string? userName = null)
    {
        var text = (contents ?? string.Empty).Trim();

        if (text.Length == 0)
        {
            return AiTaskNoteResult.Conflict("남길 말을 적으십시오.");
        }

        if (text.Length > MaxLength)
        {
            text = text[..MaxLength];
        }

        var task = await tasks.GetAsync(taskKey);

        if (task is null)
        {
            return AiTaskNoteResult.NotFound();
        }

        var byOwner = !string.IsNullOrWhiteSpace(userId)
                      && string.Equals(task.CreId, userId, StringComparison.Ordinal);

        using var db = Open();

        var noteKey = await db.ExecuteScalarAsync<long>("""
            INSERT INTO projmng.ai_task_note
                 ( task_key, contents, read_dt, cre_id, cre_dt )
            VALUES ( @taskKey, @text, CASE WHEN @byOwner THEN now() ELSE NULL END,
                     @userId, now() )
            RETURNING note_key
            """, new { taskKey, text, byOwner, userId });

        var saved = new AiTaskNote
        {
            NoteKey = noteKey,
            TaskKey = taskKey,
            Contents = text,

            // 올린 사람이 적은 줄은 넣을 때 이미 찍었다. **여기도 같이 채운다** —
            // 되읽지 않고 이 값을 그대로 돌려주므로, 비워 두면 화면이 방금
            // 적은 제 글을 「안 읽음」으로 그린다.
            ReadDt = byOwner ? DateTime.UtcNow : null,

            CreId = userId,
            CreDt = DateTime.UtcNow,
            IsOwner = byOwner,
        };

        // **알림은 기다리지 않는다.** 말은 이미 표에 남았고 화면도 그것을
        // 다시 읽는다 — 알림이 늦거나 못 가도 잃는 것이 없다
        // (`AiRequestAlerter` 머리말과 같은 판단이다).
        alerts.FireNote(task, saved, userName);

        return AiTaskNoteResult.Ok(saved);
    }

    // ── 일반 사용자의 입구 ──────────────────────────────────
    //
    // 셋 다 **그 요청을 올린 사람인지** 먼저 본다. 남의 건은 「없다」로
    // 답한다 — 「당신 것이 아닙니다」는 그 번호에 무엇이 있다는 사실을
    // 알려 준다(`AiTaskService.FindOwnAsync` 와 같은 규칙이다).

    /// <summary><b>내가 올린 요청에 붙은 말.</b> 남의 건은 한 줄도 오지 않는다.</summary>
    public async Task<List<AiTaskNote>?> MineListAsync(long taskKey, string? userId)
        => await IsOwnerAsync(taskKey, userId) ? await ListAsync(taskKey) : null;

    /// <summary><b>내가 올린 요청에 말을 남긴다.</b></summary>
    public async Task<AiTaskNoteResult> MineAddAsync(
        long taskKey, string? contents, string? userId, string? userName = null)
        => await IsOwnerAsync(taskKey, userId)
            ? await AddAsync(taskKey, contents, userId, userName)
            : AiTaskNoteResult.NotFound();

    /// <summary>
    /// <b>내가 올린 요청에 붙은 남의 말을 전부 읽은 것으로 찍는다.</b>
    /// </summary>
    /// 줄마다 찍지 않는다 — 화면에서 말을 펴면 한 건이 통째로 열려 붙은 줄이 다 보이므로 한 줄만 읽었다고 할 수 없다. 내가 적은 줄은 넣을 때 이미 찍혔으니 건드리지 않는다.
    /// <returns>이번에 읽은 것으로 바뀐 줄 수. 주인이 아니면 <c>null</c>.</returns>
    public async Task<int?> MineMarkReadAsync(long taskKey, string? userId)
    {
        if (!await IsOwnerAsync(taskKey, userId))
        {
            return null;
        }

        using var db = Open();

        return await db.ExecuteAsync("""
            UPDATE projmng.ai_task_note
               SET read_dt = now(), mod_id = @userId, mod_dt = now()
             WHERE task_key   = @taskKey
               AND is_deleted = false
               AND read_dt IS NULL
            """, new { taskKey, userId });
    }

    /// <summary>그 요청을 올린 사람인가.</summary>
    private async Task<bool> IsOwnerAsync(long taskKey, string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        var task = await tasks.GetAsync(taskKey);

        return task is not null && string.Equals(task.CreId, userId, StringComparison.Ordinal);
    }
}

/// <summary>
/// 남길말 저장의 결과. <b>「없는 번호」와 「지금은 안 되는 것」을 가른다</b> —
/// <see cref="AiTaskEditResult"/> 와 같은 까닭이다.
/// </summary>
public sealed class AiTaskNoteResult
{
    public bool Found { get; private init; } = true;

    public string? ConflictMessage { get; private init; }

    public AiTaskNote? Item { get; private init; }

    public bool IsOk => Found && ConflictMessage is null;

    public static AiTaskNoteResult Ok(AiTaskNote? item) => new() { Item = item };

    public static AiTaskNoteResult NotFound() => new() { Found = false };

    public static AiTaskNoteResult Conflict(string message) => new() { ConflictMessage = message };
}
