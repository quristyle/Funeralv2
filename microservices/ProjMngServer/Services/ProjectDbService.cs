using System.Data;
using Dapper;
using Npgsql;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>프로젝트 DB 접속(<c>projmng.devdbinfo</c>). <b>프로시저를 쓰지 않는다.</b></summary>
public sealed class ProjectDbService(IConfiguration configuration)
{
    private readonly string _connectionString =
        configuration.GetConnectionString("jsini")
        ?? throw new InvalidOperationException("연결 문자열 'jsini' 가 없습니다.");

    private IDbConnection Open() => new NpgsqlConnection(_connectionString);

    /// <summary>목록에 싣는 칸. <c>db_pwd</c> 는 일부러 싣지 않는다.</summary>
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

    /// <summary>고친다. 비밀번호를 비워 보내면 기존 값을 지킨다.</summary>
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

    /// <summary>끌어 옮긴 차례를 저장한다. <paramref name="dbRids"/> 는 화면에 보이는 줄 전부를 보이는 차례대로 담는다 — 같은 번호가 두 번 오면 자리 수와 줄 수가 어긋난다.</summary>
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

        // 화면이 보낸 번호 중 실제로 있는 줄만 옮긴다 — 지워진 줄이 섞이면 자리 수가 모자라 어긋난다.
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

        // 전부 비어 있을 때만 1..N 을 새로 매긴다.
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

    // 설명·구분(db_pcomment · db_ptype)은 insert 와 update 양쪽에서 다뤄야 한다 — 목록에는 나가는데 저장 경로가 없으면 넣을 길이 없다.

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

    /// <summary>속성을 고친다. 번호 하나로 찾는다 — 열쇠 셋을 맞추면 키 이름이 바뀔 때 조용히 아무 줄도 안 고쳐진다.</summary>
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
