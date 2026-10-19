using System.Data;
using Dapper;
using Npgsql;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>
/// 소스 정보와 그 상세. <b>프로시저를 쓰지 않는다.</b>
/// </summary>
/// 목록은 프로젝트 이름과 URL 패턴 하나(상세의 최소 값)를 조인해 붙이고, 상세는 src_dtl_rid 순이다.
/// 수정은 src_pattern_nullvalue 를 함께 고치며, 상세의 src_rid 는 옮기지 않는다 — 상세는 늘 자기 뿌리 아래에서 만들어진다.
public sealed class SourceInfoService(IConfiguration configuration)
{
    private readonly string _connectionString =
        configuration.GetConnectionString("jsini")
        ?? throw new InvalidOperationException("연결 문자열 'jsini' 가 없습니다.");

    private IDbConnection Open() => new NpgsqlConnection(_connectionString);

    private const string RootColumns = """
        a.src_rid       AS SrcRid,
        a.prj_rid       AS PrjRid,
        b.prj_name      AS PrjName,
        b.prj_nick      AS PrjNick,
        a.src_os        AS SrcOs,
        a.src_path      AS SrcPath,
        a.src_nick      AS SrcNick,
        a.src_type      AS SrcType,
        a.src_lang      AS SrcLang,
        a.src_comm      AS SrcComm,
        a.src_ui_root   AS SrcUiRoot,
        a.prj_namespace AS PrjNamespace,
        ( SELECT min(url_pattern)
            FROM projmng.dev_srcinfo_dtl
           WHERE src_pattern_grp = 'url' AND src_rid = a.src_rid ) AS UrlPattern
        """;

    private const string DetailColumns = """
        src_dtl_rid           AS SrcDtlRid,
        src_extend            AS SrcExtend,
        src_pattern_grp       AS SrcPatternGrp,
        url_pattern           AS UrlPattern,
        src_pattern_comment   AS SrcPatternComment,
        src_pattern_nullvalue AS SrcPatternNullvalue,
        src_rid               AS SrcRid
        """;

    // ── 뿌리 ──────────────────────────────────────────────

    public async Task<List<SourceInfo>> ListAsync(int? prjRid = null, int? srcRid = null)
    {
        using var db = Open();

        var rows = await db.QueryAsync<SourceInfo>($"""
            SELECT {RootColumns}
              FROM projmng.dev_srcinfo a
              LEFT JOIN projmng.dev_proj b ON b.prj_rid = a.prj_rid
             WHERE (@srcRid IS NULL OR a.src_rid = @srcRid)
               AND (@prjRid IS NULL OR a.prj_rid = @prjRid)
             ORDER BY a.src_rid
            """, new { srcRid, prjRid });

        return [.. rows];
    }

    public async Task<SourceInfo?> CreateAsync(SourceInfo item)
    {
        using var db = Open();

        var id = await db.ExecuteScalarAsync<int>("""
            INSERT INTO projmng.dev_srcinfo
                 ( src_rid, src_os, src_path, src_nick, src_type,
                   src_lang, src_comm, prj_rid, src_ui_root, prj_namespace )
            SELECT COALESCE(MAX(src_rid), 0) + 1,
                   @SrcOs, @SrcPath, @SrcNick, @SrcType,
                   @SrcLang, @SrcComm, @PrjRid, @SrcUiRoot, @PrjNamespace
              FROM projmng.dev_srcinfo
            RETURNING src_rid
            """, item);

        // 프로젝트 이름과 URL 패턴은 조인·하위질의로 붙는 값이라 다시 읽는다.
        return (await ListAsync(srcRid: id)).FirstOrDefault();
    }

    public async Task<SourceInfo?> UpdateAsync(int srcRid, SourceInfo item)
    {
        using var db = Open();

        var affected = await db.ExecuteAsync("""
            UPDATE projmng.dev_srcinfo
               SET src_os        = @SrcOs,
                   src_path      = @SrcPath,
                   src_nick      = @SrcNick,
                   src_type      = @SrcType,
                   src_lang      = @SrcLang,
                   src_comm      = @SrcComm,
                   prj_rid       = @PrjRid,
                   src_ui_root   = @SrcUiRoot,
                   prj_namespace = @PrjNamespace
             WHERE src_rid = @srcRid
            """, new
        {
            srcRid,
            item.SrcOs, item.SrcPath, item.SrcNick, item.SrcType,
            item.SrcLang, item.SrcComm, item.PrjRid, item.SrcUiRoot, item.PrjNamespace,
        });

        return affected == 0 ? null : (await ListAsync(srcRid: srcRid)).FirstOrDefault();
    }

    // ── 상세 ──────────────────────────────────────────────

    public async Task<List<SourceInfoDetail>> DetailsAsync(int? srcRid = null, int? srcDtlRid = null)
    {
        using var db = Open();

        var rows = await db.QueryAsync<SourceInfoDetail>($"""
            SELECT {DetailColumns}
              FROM projmng.dev_srcinfo_dtl
             WHERE (@srcDtlRid IS NULL OR src_dtl_rid = @srcDtlRid)
               AND (@srcRid IS NULL OR src_rid = @srcRid)
             ORDER BY src_dtl_rid
            """, new { srcDtlRid, srcRid });

        return [.. rows];
    }

    public async Task<SourceInfoDetail> CreateDetailAsync(SourceInfoDetail item)
    {
        using var db = Open();

        return await db.QuerySingleAsync<SourceInfoDetail>($"""
            INSERT INTO projmng.dev_srcinfo_dtl
                 ( src_dtl_rid, src_extend, src_pattern_grp, url_pattern,
                   src_pattern_comment, src_pattern_nullvalue, src_rid )
            SELECT COALESCE(MAX(src_dtl_rid), 0) + 1,
                   @SrcExtend, @SrcPatternGrp, @UrlPattern,
                   @SrcPatternComment, @SrcPatternNullvalue, @SrcRid
              FROM projmng.dev_srcinfo_dtl
            RETURNING {DetailColumns}
            """, item);
    }

    /// <summary>
    /// 상세를 고친다. <b><c>src_pattern_nullvalue</c> 도 함께 고친다</b> —
    /// 수정 질의에 빠지면 한 번 넣은 값을 고칠 수 없는 칸이다.
    /// </summary>
    public async Task<SourceInfoDetail?> UpdateDetailAsync(int srcDtlRid, SourceInfoDetail item)
    {
        using var db = Open();

        return await db.QuerySingleOrDefaultAsync<SourceInfoDetail>($"""
            UPDATE projmng.dev_srcinfo_dtl
               SET src_extend            = @SrcExtend,
                   src_pattern_grp       = @SrcPatternGrp,
                   url_pattern           = @UrlPattern,
                   src_pattern_comment   = @SrcPatternComment,
                   src_pattern_nullvalue = @SrcPatternNullvalue
             WHERE src_dtl_rid = @srcDtlRid
            RETURNING {DetailColumns}
            """, new
        {
            srcDtlRid,
            item.SrcExtend, item.SrcPatternGrp, item.UrlPattern,
            item.SrcPatternComment, item.SrcPatternNullvalue,
        });
    }

    public async Task<bool> DeleteDetailAsync(int srcDtlRid)
    {
        using var db = Open();

        var affected = await db.ExecuteAsync(
            "DELETE FROM projmng.dev_srcinfo_dtl WHERE src_dtl_rid = @srcDtlRid", new { srcDtlRid });

        return affected > 0;
    }
}
