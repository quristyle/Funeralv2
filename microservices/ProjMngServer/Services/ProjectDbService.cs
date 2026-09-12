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
        a.db_comm     AS DbComm
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
             ORDER BY a.db_rid
            """, new { prjRid, dbRid });

        return [.. rows];
    }

    public async Task<ProjectDb?> CreateAsync(ProjectDb item)
    {
        using var db = Open();

        var id = await db.ExecuteScalarAsync<int>("""
            INSERT INTO projmng.devdbinfo
                 ( db_rid, db_ip, db_nick, db_cert, db_type, db_id, db_pwd,
                   db_database, db_port, db_schema, db_comm, prj_rid )
            SELECT COALESCE(MAX(db_rid), 0) + 1,
                   @DbIp, @DbNick, @DbCert, @DbType, @DbId, @DbPwd,
                   @DbDatabase, @DbPort, @DbSchema, @DbComm, @PrjRid
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

                   -- 빈 값이면 그대로 둔다. 목록이 비밀번호를 안 주므로
                   -- 화면은 대개 빈 채로 보내고, 그때 지워지면 접속이 끊긴다.
                   db_pwd      = COALESCE(NULLIF(@DbPwd, ''), db_pwd)
             WHERE db_rid = @dbRid
            """, new
        {
            dbRid,
            item.PrjRid, item.DbNick, item.DbType, item.DbIp, item.DbPort,
            item.DbDatabase, item.DbSchema, item.DbId, item.DbCert, item.DbComm,
            DbPwd = item.DbPwd ?? string.Empty,
        });

        return affected == 0 ? null : (await ListAsync(dbRid: dbRid)).FirstOrDefault();
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
