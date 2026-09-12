using System.Data;
using Dapper;
using Npgsql;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>
/// 소스 정보와 그 상세. <b>프로시저를 쓰지 않는다.</b>
/// </summary>
/// <remarks>
/// <para>
/// 옛 길은 <c>sp_dev_srcinfo_exec</c>(뿌리)와 <c>sp_dev_srcinfo_dtl_exec</c>
/// (상세) 둘이었다.
/// </para>
///
/// <para>
/// [그대로 옮긴 것]
/// </para>
///
/// <list type="bullet">
///   <item>
///     뿌리 목록에 프로젝트 이름·별칭을 조인해 붙이고, <b>URL 패턴 하나</b>를
///     상세에서 끌어온다(<c>src_pattern_grp = 'url'</c> 중 가장 작은 값).
///     여럿이면 그중 하나만 보이는 것이 원래 동작이다 — 목록에서 「무엇을
///     다루는 소스인가」를 가늠하는 용도라 그것으로 충분했다.
///   </item>
///   <item>상세는 <c>src_dtl_rid</c> 순으로.</item>
/// </list>
///
/// <para>
/// [고친 것 셋]
/// </para>
///
/// <list type="number">
///   <item>
///     <b>상세를 고칠 때 <c>src_pattern_nullvalue</c> 가 빠져 있었다.</b>
///     insert 에는 있고 update 에는 없다. 한 번 넣은 값을 화면에서 바꿀
///     방법이 없었고, 바꾸려면 DB 를 직접 만져야 했다. 이제 함께 고친다.
///   </item>
///   <item>
///     <b>번호를 <c>max(...)+1</c> 로 만들었다.</b> 둘 다 그렇다.
///     <c>insert … select</c> 한 문장으로 좁혔다 — 진짜 해법은 시퀀스다.
///   </item>
///   <item>
///     <b>상세의 <c>src_rid</c> 를 수정에서 다루지 않았다.</b> 그래서 상세를
///     다른 소스로 옮길 수 없었는데, 그것은 <b>그대로 둔다</b> — 상세는 늘
///     자기 뿌리 아래에서 만들어지고, 옮기는 일은 업무에 없다.
///     (여기서는 수정 질의에 넣지 않는 것으로 그 뜻을 지킨다.)
///   </item>
/// </list>
/// </remarks>
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
    /// 옛 update 에 빠져 있던 칸이다(머리말).
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
