using System.Data;
using Dapper;
using Npgsql;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>
/// 프로젝트 목록 — <b>프로시저를 쓰지 않는다.</b>
/// </summary>
/// <remarks>
/// <para>
/// 옛 길은 <c>projmng.sp_dev_proj_exec</c> 였다. 그 안에서 하던 일은 셋뿐이고
/// (조회 · 등록 · 수정) 전부 여기로 옮겨 왔다. 옮기면서 확인한 것과 고친 것을
/// 아래에 적어 둔다 — <b>프로시저를 지우고 나면 그 안의 규칙을 다시 읽을 수
/// 없기 때문</b>이다.
/// </para>
///
/// <para>
/// [그대로 옮긴 것]
/// </para>
///
/// <list type="bullet">
///   <item><c>prj_rid</c> 를 주면 그 한 건, 안 주면 전부. 정렬은 <c>prj_rid</c>.</item>
///   <item>등록이면 <c>prj_rid</c> 를 서버가 만들고, 수정이면 그 값으로 찾는다.</item>
/// </list>
///
/// <para>
/// [고친 것 셋 — 프로시저의 결함이다]
/// </para>
///
/// <list type="number">
///   <item>
///     <b>수정할 때 등록일(<c>cre_dt</c>)을 덮어썼다.</b>
///     프로시저의 update 문에 <c>cre_dt = now()</c> 가 들어 있어, 한 번이라도
///     고친 프로젝트는 등록일을 잃었다. 여기서는 <b>등록할 때만</b> 채운다.
///   </item>
///   <item>
///     <b>번호를 <c>max(prj_rid)+1</c> 로 만들었다.</b> 둘이 동시에 등록하면
///     같은 번호가 나온다(그 칸에 유일 제약도 없다). 여기서는 한 문장 안에서
///     뽑아 넣어(<c>insert … select</c>) 그 틈을 좁혔다.
///     <b>진짜 해법은 시퀀스</b>이고, 표를 고칠 수 있을 때 그렇게 바꾼다.
///   </item>
///   <item>
///     <b>금액 형변환이 등록과 수정에서 달랐다</b>(<c>::bigint</c> ↔ <c>::int</c>).
///     칸이 <c>integer</c> 라 등록에서 큰 값을 넣으면 그 자리에서 터졌다.
///     이제 <see cref="int"/> 하나다.
///   </item>
/// </list>
///
/// <para>
/// [정렬 순서(<c>prj_srt</c>)를 등록에서도 받는다]
/// </para>
///
/// <para>
/// 프로시저는 <b>수정할 때만</b> 이 값을 다뤘다. 등록에서는 빠져 있어 DB
/// 기본값(99999)이 들어갔고, 새 프로젝트가 늘 목록 끝으로 갔다. 값을 주면
/// 그대로 쓰고, 안 주면 기본값이 그대로 살아 있게 <c>null</c> 을 넣는다.
/// </para>
/// </remarks>
public sealed class ProjectService(IConfiguration configuration)
{
    private readonly string _connectionString =
        configuration.GetConnectionString("jsini")
        ?? throw new InvalidOperationException("연결 문자열 'jsini' 가 없습니다.");

    private IDbConnection Open() => new NpgsqlConnection(_connectionString);

    /// <summary>칸 이름을 한 곳에만 적는다 — 조회·등록·수정이 같은 목록을 본다.</summary>
    private const string Columns = """
        prj_rid      AS PrjRid,
        prj_name     AS PrjName,
        prj_desc     AS PrjDesc,
        prj_sdt      AS PrjSdt,
        prj_edt      AS PrjEdt,
        prj_nick     AS PrjNick,
        prj_type     AS PrjType,
        proj_pay     AS ProjPay,
        prj_use_pay  AS PrjUsePay,
        prj_srt      AS PrjSrt,
        mod_dt       AS ModDt,
        cre_dt       AS CreDt
        """;

    /// <summary>
    /// 프로젝트 목록. <paramref name="prjRid"/> 를 주면 그 한 건만.
    /// </summary>
    public async Task<List<Project>> ListAsync(int? prjRid = null)
    {
        using var db = Open();

        // 조건을 문자열로 잇지 않는다. `@prjRid` 가 null 이면 전부다 —
        // 프로시저가 `nvl(p_prj_rid,'') = ''` 로 하던 것과 같은 뜻이고,
        // 값이 질의문에 섞이지 않는다는 것만 다르다.
        var rows = await db.QueryAsync<Project>($"""
            SELECT {Columns}
              FROM projmng.dev_proj
             WHERE (@prjRid IS NULL OR prj_rid = @prjRid)
             ORDER BY prj_rid
            """, new { prjRid });

        return [.. rows];
    }

    /// <summary>새 프로젝트. 번호와 등록·수정 일시는 서버가 정한다.</summary>
    public async Task<Project> CreateAsync(Project item)
    {
        using var db = Open();

        // 번호를 **같은 문장 안에서** 뽑는다. 따로 읽어 두었다가 넣으면 그
        // 사이에 남이 먼저 넣는다. 표가 비어 있을 때를 위해 `coalesce`.
        var created = await db.QuerySingleAsync<Project>($"""
            INSERT INTO projmng.dev_proj
                 ( prj_rid, prj_name, prj_desc, prj_sdt, prj_edt, prj_nick,
                   prj_type, proj_pay, prj_use_pay, prj_srt, mod_dt, cre_dt )
            SELECT COALESCE(MAX(prj_rid), 0) + 1,
                   @PrjName, @PrjDesc, @PrjSdt, @PrjEdt, @PrjNick,
                   @PrjType, @ProjPay, @PrjUsePay,
                   COALESCE(@PrjSrt, 99999), now(), now()
              FROM projmng.dev_proj
            RETURNING {Columns}
            """, item);

        return created;
    }

    /// <summary>
    /// 프로젝트를 고친다. <b>등록일은 건드리지 않는다</b>(머리말 참고).
    /// </summary>
    /// <returns>고친 것. 그 번호가 없으면 <c>null</c>.</returns>
    public async Task<Project?> UpdateAsync(int prjRid, Project item)
    {
        using var db = Open();

        return await db.QuerySingleOrDefaultAsync<Project>($"""
            UPDATE projmng.dev_proj
               SET prj_name    = @PrjName,
                   prj_desc    = @PrjDesc,
                   prj_sdt     = @PrjSdt,
                   prj_edt     = @PrjEdt,
                   prj_nick    = @PrjNick,
                   prj_type    = @PrjType,
                   proj_pay    = @ProjPay,
                   prj_use_pay = @PrjUsePay,
                   prj_srt     = COALESCE(@PrjSrt, prj_srt),
                   mod_dt      = now()
             WHERE prj_rid = @prjRid
            RETURNING {Columns}
            """, new
        {
            prjRid,
            item.PrjName,
            item.PrjDesc,
            item.PrjSdt,
            item.PrjEdt,
            item.PrjNick,
            item.PrjType,
            item.ProjPay,
            item.PrjUsePay,
            item.PrjSrt,
        });
    }

    /// <summary>
    /// 프로젝트를 지운다.
    ///
    /// <para>
    /// <b>옛 프로시저에는 삭제가 없었다.</b> 화면에도 삭제 단추가 없었으므로
    /// 기능을 늘리는 셈인데, 그래서 <b>부르는 곳을 두지 않았다</b> — 경로만
    /// 열어 두면 실수로 지울 길이 생긴다. 필요해질 때 화면에서 연다.
    /// </para>
    /// </summary>
    public async Task<bool> DeleteAsync(int prjRid)
    {
        using var db = Open();

        var affected = await db.ExecuteAsync(
            "DELETE FROM projmng.dev_proj WHERE prj_rid = @prjRid", new { prjRid });

        return affected > 0;
    }
}
