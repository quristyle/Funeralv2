using System.Data;
using Dapper;
using Npgsql;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>
/// 프로젝트 DB 접속 — <c>projmng.devdbinfo</c>. <b>프로시저를 쓰지 않는다.</b>
/// </summary>
/// <remarks>
/// <para>
/// 옛 길은 셋이었다 — <c>sp_projdblist</c>(목록) · <c>sp_projdbsave</c>
/// (등록·수정) · <c>sp_projdbdel</c>(삭제).
/// </para>
///
/// <para>
/// [옮기면서 확인한 것 넷]
/// </para>
///
/// <list type="number">
///   <item>
///     <b>비밀번호가 목록에 실려 나갔다.</b> <c>select a.*</c> 라
///     <c>db_pwd</c> 가 화면까지 갔다. 목록에서 그 값을 쓰는 자리는 없다.
///     이제 <b>보내지 않는다</b> — 저장할 때 비워 두면 기존 값을 지킨다.
///   </item>
///   <item>
///     <b>설명(<c>db_comm</c>)을 저장이 다루지 않았다.</b> 목록에는 나가는데
///     넣거나 고칠 길이 없었다. 이제 저장에 넣었다.
///   </item>
///   <item>
///     <b>수정할 때는 커서를 열지 않았다.</b> 저장 프로시저가 insert 갈래에서만
///     <c>open p_cur</c> 를 한다. 부르는 쪽은 늘 커서를 기대하므로, 수정은
///     결과 없이 끝나거나 드라이버에 따라 오류가 났다. REST 로 오면서
///     그 갈래 자체가 없어졌다.
///   </item>
///   <item>
///     <b>번호를 <c>max(db_rid)+1</c> 로 만들었다.</b> 다른 표와 같다 —
///     한 문장으로 좁혔고 진짜 해법은 시퀀스다.
///   </item>
///   <item>
///     <b>사람이 정하는 차례가 없었다</b>(2026-09-13). 목록은
///     <c>db_rid</c>(만든 순서), 고르개 둘(<c>projdb</c> · <c>projdb2</c>)은
///     <c>db_nick</c>(가나다)로 <b>서로 다르게</b> 늘어놓았고, 자주 쓰는
///     접속을 위로 올릴 길이 없었다. <c>db_srt</c> 를 더하고 셋 다 그것을
///     먼저 본다(<c>deploy/sql/projmng-devdbinfo-sort-2026-09-13.sql</c>).
///   </item>
/// </list>
/// </remarks>
public sealed class ProjectDbService(IConfiguration configuration)
{
    private readonly string _connectionString =
        configuration.GetConnectionString("jsini")
        ?? throw new InvalidOperationException("연결 문자열 'jsini' 가 없습니다.");

    private IDbConnection Open() => new NpgsqlConnection(_connectionString);

    /// <summary>
    /// 목록에 싣는 칸. <b><c>db_pwd</c> 가 없다</b> — 일부러다(머리말).
    /// </summary>
    private const string Columns = """
        a.db_rid      AS DbRid,
        a.prj_rid     AS PrjRid,
        b.prj_name    AS PrjName,
        b.prj_nick    AS PrjNick,
        a.db_nick     AS DbNick,
        a.db_type     AS DbType,
        a.db_ip       AS DbIp,
        a.db_port     AS DbPort,
        a.db_database AS DbDatabase,
        a.db_schema   AS DbSchema,
        a.db_id       AS DbId,
        a.db_cert     AS DbCert,
        a.db_comm     AS DbComm,
        a.db_srt      AS DbSrt
        """;

    public async Task<List<ProjectDb>> ListAsync(int? prjRid = null, int? dbRid = null)
    {
        using var db = Open();

        var rows = await db.QueryAsync<ProjectDb>($"""
            SELECT {Columns}
              FROM projmng.devdbinfo a
              LEFT JOIN projmng.dev_proj b ON b.prj_rid = a.prj_rid
             WHERE (@prjRid IS NULL OR a.prj_rid = @prjRid)
               AND (@dbRid IS NULL OR a.db_rid = @dbRid)
             -- 사람이 정한 차례가 먼저다(머리말 5). 빈 줄은 맨 뒤로.
             ORDER BY a.db_srt NULLS LAST, a.db_nick, a.db_rid
            """, new { prjRid, dbRid });

        return [.. rows];
    }

    public async Task<ProjectDb?> CreateAsync(ProjectDb item)
    {
        using var db = Open();

        var id = await db.ExecuteScalarAsync<int>("""
            INSERT INTO projmng.devdbinfo
                 ( db_rid, db_ip, db_nick, db_cert, db_type, db_id, db_pwd,
                   db_database, db_port, db_schema, db_comm, prj_rid, db_srt )
            SELECT COALESCE(MAX(db_rid), 0) + 1,
                   @DbIp, @DbNick, @DbCert, @DbType, @DbId, @DbPwd,
                   @DbDatabase, @DbPort, @DbSchema, @DbComm, @PrjRid,

                   -- **맨 뒤에 붙인다.** 화면이 준 값이 있으면 그것을 쓰고,
                   -- 없으면 지금 제일 큰 차례 다음이다. 새 줄을 0 이나 빈
                   -- 값으로 두면 목록 맨 위(또는 맨 아래)에 불쑥 끼어든다.
                   COALESCE(@DbSrt, MAX(db_srt) + 1, 1)
              FROM projmng.devdbinfo
            RETURNING db_rid
            """, item);

        return (await ListAsync(dbRid: id)).FirstOrDefault();
    }

    /// <summary>
    /// 고친다. <b>비밀번호를 비워 보내면 기존 값을 지킨다</b>(머리말).
    /// </summary>
    public async Task<ProjectDb?> UpdateAsync(int dbRid, ProjectDb item)
    {
        using var db = Open();

        var affected = await db.ExecuteAsync("""
            UPDATE projmng.devdbinfo
               SET prj_rid     = @PrjRid,
                   db_nick     = @DbNick,
                   db_type     = @DbType,
                   db_ip       = @DbIp,
                   db_port     = @DbPort,
                   db_database = @DbDatabase,
                   db_schema   = @DbSchema,
                   db_id       = @DbId,
                   db_cert     = @DbCert,
                   db_comm     = @DbComm,

                   -- 비워 보내면 그대로 둔다. 편집 창에 차례 칸이 없던 시절의
                   -- 줄이나 다른 경로로 온 저장이 **차례를 지워 버리지 않게** 한다.
                   db_srt      = COALESCE(@DbSrt, db_srt),

                   -- 빈 값이면 그대로 둔다. 목록이 비밀번호를 안 주므로
                   -- 화면은 대개 빈 채로 보내고, 그때 지워지면 접속이 끊긴다.
                   db_pwd      = COALESCE(NULLIF(@DbPwd, ''), db_pwd)
             WHERE db_rid = @dbRid
            """, new
        {
            dbRid,
            item.PrjRid, item.DbNick, item.DbType, item.DbIp, item.DbPort,
            item.DbDatabase, item.DbSchema, item.DbId, item.DbCert, item.DbComm,
            item.DbSrt,
            DbPwd = item.DbPwd ?? string.Empty,
        });

        return affected == 0 ? null : (await ListAsync(dbRid: dbRid)).FirstOrDefault();
    }

    /// <summary>
    /// 끌어 옮긴 차례를 저장한다. <paramref name="dbRids"/> 는
    /// <b>화면에 보이는 줄 전부를, 보이는 차례대로</b> 담는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// [1..N 으로 다시 번호를 매기지 않는다]
    /// </para>
    ///
    /// <para>
    /// 화면은 대개 프로젝트로 좁혀 본다. 거기서 1..N 을 새로 매기면
    /// <b>다른 프로젝트의 줄과 번호가 겹치고</b>, 그 순간 이 프로젝트를
    /// 만진 적 없는 사람의 고르개 차례가 흔들린다.
    /// </para>
    ///
    /// <para>
    /// 그래서 <b>그 줄들이 지금 차지한 자리만 서로 바꾼다</b> — 값들을 모아
    /// 오름차순으로 세우고 새 차례대로 다시 나눠 준다. 목록에 없는 줄은
    /// 값도 상대 위치도 그대로다.
    /// </para>
    ///
    /// <para>
    /// [한 번에 커밋한다]
    /// </para>
    ///
    /// <para>
    /// 줄마다 따로 저장하면 중간에 끊겼을 때 <b>두 줄이 같은 자리를 갖거나
    /// 어느 자리도 갖지 않는</b> 상태로 남는다. 되돌릴 방법이 화면에 없다.
    /// </para>
    /// </remarks>
    /// <param name="dbRids">보이는 차례대로 담은 접속 번호들.</param>
    /// <returns>실제로 자리가 바뀐 줄 수.</returns>
    public async Task<int> ReorderAsync(IReadOnlyList<int> dbRids)
    {
        // 같은 번호가 두 번 오면 자리 수와 줄 수가 어긋난다.
        var ids = dbRids.Distinct().ToArray();

        if (ids.Length < 2)
        {
            // 한 줄짜리는 옮길 자리가 없다.
            return 0;
        }

        using var db = Open();
        db.Open();
        using var tx = db.BeginTransaction();

        var current = (await db.QueryAsync<(int DbRid, int? DbSrt)>(
            "SELECT db_rid AS DbRid, db_srt AS DbSrt FROM projmng.devdbinfo WHERE db_rid = ANY(@ids)",
            new { ids }, tx)).ToDictionary(r => r.DbRid, r => r.DbSrt);

        // 화면이 보낸 번호 중 **실제로 있는 줄**만 옮긴다. 그 사이에 누가
        // 지운 줄이 섞여 오면 자리 수가 모자라 나눠 주다 어긋난다.
        var moving = ids.Where(current.ContainsKey).ToArray();

        if (moving.Length < 2)
        {
            return 0;
        }

        // 비어 있는 줄은 맨 뒤로 — 목록의 차례와 같은 규칙이다.
        var slots = moving
            .Select(id => current[id] ?? int.MaxValue)
            .Order()
            .ToArray();

        // 전부 비어 있으면 나눠 줄 자리가 없다. 그때만 1..N 을 새로 매긴다 —
        // 겹칠 다른 값이 애초에 없으므로 위의 걱정이 여기서는 성립하지 않는다.
        if (slots[0] == int.MaxValue)
        {
            slots = [.. Enumerable.Range(1, moving.Length)];
        }

        var changed = 0;

        for (var i = 0; i < moving.Length; i++)
        {
            if (current[moving[i]] == slots[i])
            {
                continue;
            }

            changed += await db.ExecuteAsync(
                "UPDATE projmng.devdbinfo SET db_srt = @srt WHERE db_rid = @dbRid",
                new { srt = slots[i], dbRid = moving[i] }, tx);
        }

        tx.Commit();
        return changed;
    }

    public async Task<bool> DeleteAsync(int dbRid)
    {
        using var db = Open();

        var affected = await db.ExecuteAsync(
            "DELETE FROM projmng.devdbinfo WHERE db_rid = @dbRid", new { dbRid });

        return affected > 0;
    }

    // ── 접속에 딸린 속성 (`dev_db_prop`) ──────────────────
    //
    // 옛 길은 `sp_dev_db_prop_exec` 하나였다. 옮기면서 확인한 것 셋 —
    //
    //  1. **설명·구분(`db_pcomment` · `db_ptype`)을 저장이 다루지 않았다.**
    //     목록에는 나가는데 insert 에도 update 에도 없어서 넣을 길이 없었다.
    //     이제 둘 다 저장한다.
    //  2. **수정이 열쇠 셋을 모두 맞춰야 들었다**(`db_prid` · `db_rid` ·
    //     `db_pkey`). 그래서 키 이름을 바꾸면 수정이 **조용히 아무 줄도 안
    //     고쳤다** — 오류도 안 났다. 이제 번호 하나로 찾는다.
    //  3. **번호를 `max(db_prid)+1` 로 만들었다.** 다른 표와 같다.
    //
    // 값에서 빈 줄을 걷어 내는 것은 그대로 옮겼다(모델 머리말).

    private const string PropColumns = """
        db_prid    AS DbPrid,
        db_rid     AS DbRid,
        db_pkey    AS DbPkey,
        REGEXP_REPLACE(db_pvalue, E'\r\n\r\n', '', 'g') AS DbPvalue,
        db_pcomment AS DbPcomment,
        db_ptype   AS DbPtype,
        mod_dt     AS ModDt,
        cre_dt     AS CreDt
        """;

    public async Task<List<ProjectDbProp>> PropsAsync(int dbRid, string? key = null)
    {
        using var db = Open();

        var rows = await db.QueryAsync<ProjectDbProp>($"""
            SELECT {PropColumns}
              FROM projmng.dev_db_prop
             WHERE db_rid = @dbRid
               AND (@key = '' OR db_pkey = @key)
             ORDER BY db_prid
            """, new { dbRid, key = key ?? string.Empty });

        return [.. rows];
    }

    public async Task<ProjectDbProp> CreatePropAsync(ProjectDbProp item)
    {
        using var db = Open();

        return await db.QuerySingleAsync<ProjectDbProp>($"""
            INSERT INTO projmng.dev_db_prop
                 ( db_rid, db_prid, db_pkey, db_pvalue, db_pcomment, db_ptype, mod_dt, cre_dt )
            SELECT @DbRid, COALESCE(MAX(db_prid), 0) + 1,
                   @DbPkey, @DbPvalue, @DbPcomment, @DbPtype, now(), now()
              FROM projmng.dev_db_prop
            RETURNING {PropColumns}
            """, item);
    }

    /// <summary>
    /// 속성을 고친다. <b>번호 하나로 찾는다</b> — 옛 것은 열쇠 셋을 모두
    /// 맞춰야 들어서, 키 이름을 바꾸면 조용히 아무 줄도 안 고쳤다.
    /// </summary>
    public async Task<ProjectDbProp?> UpdatePropAsync(int dbPrid, ProjectDbProp item)
    {
        using var db = Open();

        return await db.QuerySingleOrDefaultAsync<ProjectDbProp>($"""
            UPDATE projmng.dev_db_prop
               SET db_pkey     = @DbPkey,
                   db_pvalue   = @DbPvalue,
                   db_pcomment = @DbPcomment,
                   db_ptype    = @DbPtype,
                   mod_dt      = now()
             WHERE db_prid = @dbPrid
            RETURNING {PropColumns}
            """, new { dbPrid, item.DbPkey, item.DbPvalue, item.DbPcomment, item.DbPtype });
    }

    public async Task<bool> DeletePropAsync(int dbPrid)
    {
        using var db = Open();

        var affected = await db.ExecuteAsync(
            "DELETE FROM projmng.dev_db_prop WHERE db_prid = @dbPrid", new { dbPrid });

        return affected > 0;
    }
}
