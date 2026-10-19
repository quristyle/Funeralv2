using System.Data;
using Dapper;
using Npgsql;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>
/// 프로젝트 목록 — <b>프로시저를 쓰지 않는다.</b>
/// </summary>
/// 프로시저 대신 이쪽에서 다룬다. 옮기면서 고친 것: 등록일(cre_dt)은 등록할 때만 채우고(수정이 덮어쓰던 결함), 번호는 한 문장 안에서 뽑아 넣고(동시 등록 틈), 금액은 int 하나로 맞췄다.
/// 정렬 순서(prj_srt)는 등록에서도 받는다 — 빠지면 DB 기본값(99999)이 들어가 새 프로젝트가 늘 목록 끝으로 간다.
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
    /// <b>등록일은 건드리지 않는다</b>.
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
    /// 삭제 경로는 두지만 부르는 곳이 없다 — 화면에 삭제 단추가 없어, 필요해질 때 화면에서 연다.
    /// </summary>
    public async Task<bool> DeleteAsync(int prjRid)
    {
        using var db = Open();

        var affected = await db.ExecuteAsync(
            "DELETE FROM projmng.dev_proj WHERE prj_rid = @prjRid", new { prjRid });

        return affected > 0;
    }
}
