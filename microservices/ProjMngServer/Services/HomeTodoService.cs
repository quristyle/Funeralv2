using System.Data;
using Dapper;
using Npgsql;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>할 일(<c>projmng.home_todo</c>). <b>프로시저를 쓰지 않는다.</b></summary>
public sealed class HomeTodoService(IConfiguration configuration)
{
    private readonly string _connectionString =
        configuration.GetConnectionString("jsini")
        ?? throw new InvalidOperationException("연결 문자열 'jsini' 가 없습니다.");

    private IDbConnection Open() => new NpgsqlConnection(_connectionString);

    private const string Columns = """
        a.todo_key    AS TodoKey,
        a.target_day  AS TargetDay,
        a.title       AS Title,
        a.is_complete AS IsComplete,
        a.cre_dt      AS CreDt,
        a.comp_dt     AS CompDt,
        a.cre_id      AS CreId,
        a.comp_id     AS CompId,
        a.mod_dt      AS ModDt,
        a.mod_id      AS ModId,
        a.comments    AS Comments,
        a.target_user AS TargetUser,
        COALESCE(a.fix_point, 0) AS FixPoint,
        a.todo_state  AS TodoState,
        b.cm_nm       AS TodoStateName
        """;

    /// <summary>할 일 목록. 준 조건만 걸린다.</summary>
    public async Task<List<HomeTodo>> ListAsync(
        string? targetUser = null, string? todoState = null,
        bool? isComplete = null, DateOnly? targetDay = null, long? todoKey = null)
    {
        using var db = Open();

        var rows = await db.QueryAsync<HomeTodo>($"""
            SELECT {Columns}
              FROM projmng.home_todo a
              LEFT JOIN ( SELECT cm_cd, cm_nm FROM projmng.devcomm
                           WHERE cm_pcd = 'TODO_STATE' ) b
                     ON b.cm_cd = a.todo_state
             -- 널일 수 있는 파라미터에는 **형을 붙인다.** 안 붙이면
             -- PostgreSQL 이 `$5` 의 형을 못 정해 42P08 로 끊는다 —
             -- 값이 있을 때는 멀쩡하고 **비었을 때만** 난다.
             WHERE (@todoKey::bigint IS NULL OR a.todo_key = @todoKey)
               AND (@targetUser = '' OR a.target_user = @targetUser)
               AND (@todoState = '' OR a.todo_state = @todoState)
               AND (@isComplete::boolean IS NULL OR a.is_complete = @isComplete)
               AND (@targetDay::date IS NULL OR a.target_day = @targetDay)
             ORDER BY a.target_day, COALESCE(a.fix_point, 0) DESC, a.title
            """, new
        {
            todoKey,
            targetUser = targetUser ?? string.Empty,
            todoState = todoState ?? string.Empty,
            isComplete,
            targetDay,
        });

        return [.. rows];
    }

    public async Task<HomeTodo?> CreateAsync(HomeTodo item, string? userId)
    {
        using var db = Open();

        var key = await db.ExecuteScalarAsync<long>("""
            INSERT INTO projmng.home_todo
                 ( todo_key, title, is_complete, cre_dt, cre_id, comp_id,
                   comments, target_day, fix_point, target_user, todo_state )
            SELECT COALESCE(MAX(todo_key), 0) + 1,
                   @Title, @IsComplete, now(), @userId, @CompId,
                   @Comments, @TargetDay, @FixPoint, @TargetUser, @TodoState
              FROM projmng.home_todo
            RETURNING todo_key
            """, new
        {
            item.Title, item.IsComplete, userId, item.CompId,
            item.Comments, item.TargetDay, item.FixPoint, item.TargetUser, item.TodoState,
        });

        return (await ListAsync(todoKey: key)).FirstOrDefault();
    }

    public async Task<HomeTodo?> UpdateAsync(long todoKey, HomeTodo item, string? userId)
    {
        using var db = Open();

        var affected = await db.ExecuteAsync("""
            UPDATE projmng.home_todo
               SET title       = @Title,
                   is_complete = @IsComplete,
                   comp_id     = @CompId,
                   mod_dt      = now(),
                   mod_id      = @userId,
                   comments    = @Comments,
                   target_day  = @TargetDay,
                   fix_point   = @FixPoint,
                   todo_state  = @TodoState,
                   target_user = @TargetUser,

                   -- 완료 일시는 **완료로 바뀌는 순간에만** 찍는다.
                   -- 옛 프로시저는 이 칸을 아예 안 건드려서 늘 비어 있었다.
                   comp_dt     = CASE WHEN @IsComplete AND comp_dt IS NULL THEN now()
                                      WHEN NOT @IsComplete THEN NULL
                                      ELSE comp_dt END
             WHERE todo_key = @todoKey
            """, new
        {
            todoKey, item.Title, item.IsComplete, item.CompId, userId,
            item.Comments, item.TargetDay, item.FixPoint, item.TodoState, item.TargetUser,
        });

        return affected == 0 ? null : (await ListAsync(todoKey: todoKey)).FirstOrDefault();
    }

    public async Task<bool> DeleteAsync(long todoKey)
    {
        using var db = Open();

        var affected = await db.ExecuteAsync(
            "DELETE FROM projmng.home_todo WHERE todo_key = @todoKey", new { todoKey });

        return affected > 0;
    }

    /// <summary>그 날짜의 되풀이 할 일을 만든다. 공통코드 HOMEWORK 중 cm_val2 가 R·M 인 것을 사람마다 한 벌씩 넣는다 — 사람을 받아서 만든다.</summary>
    public async Task<int> MakeAsync(DateOnly targetDay, IReadOnlyList<string> users)
    {
        using var db = Open();

        var made = 0;

        foreach (var user in users.Where(u => !string.IsNullOrWhiteSpace(u)))
        {
            made += await db.ExecuteAsync("""
                INSERT INTO projmng.home_todo
                     ( todo_key, title, is_complete, cre_dt, cre_id, comp_id,
                       comments, target_day, fix_point, target_user, todo_state )
                SELECT (SELECT COALESCE(MAX(todo_key), 0) FROM projmng.home_todo)
                       + row_number() OVER (ORDER BY c.cm_cd),
                       c.cm_nm, false, now(), 'system', '',
                       '', @targetDay, COALESCE(c.cm_val, '0')::bigint, @user, c.cm_val2
                  FROM projmng.devcomm c
                 WHERE c.cm_pcd = 'HOMEWORK'
                   AND c.cm_val2 IN ('R', 'M')
                   AND NOT EXISTS (
                       SELECT 1 FROM projmng.home_todo t
                        WHERE t.target_user = @user
                          AND t.target_day = @targetDay
                          AND t.title = c.cm_nm)
                """, new { targetDay, user });
        }

        return made;
    }

    /// <summary>사람별 적립 금액. 완료한 것만 센다.</summary>
    public async Task<List<HomeTodoPay>> PayAsync(string? targetUser = null)
    {
        using var db = Open();

        var rows = await db.QueryAsync<HomeTodoPay>("""
            SELECT a.target_user AS TargetUser,
                   a.total_pay   AS TotalPay,
                   COALESCE(b.today_pay, 0) AS TodayPay
              FROM ( SELECT target_user, SUM(COALESCE(fix_point, 0)) AS total_pay
                       FROM projmng.home_todo
                      WHERE is_complete = true
                      GROUP BY target_user ) a
              LEFT JOIN
                   ( SELECT target_user, SUM(COALESCE(fix_point, 0)) AS today_pay
                       FROM projmng.home_todo
                      WHERE is_complete = true
                        AND target_day >= projmng.today_kst()
                      GROUP BY target_user ) b
                ON b.target_user = a.target_user
             WHERE (@targetUser = '' OR a.target_user = @targetUser)
             ORDER BY a.target_user
            """, new { targetUser = targetUser ?? string.Empty });

        return [.. rows];
    }
}
