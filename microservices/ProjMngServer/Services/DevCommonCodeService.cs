using System.Data;
using Dapper;
using Npgsql;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>
/// 프로젝트관리 공통코드 — <c>projmng.devcomm</c>. <b>프로시저를 쓰지 않는다.</b>
/// </summary>
/// <remarks>
/// <para>
/// 옛 길은 <c>sp_devcomm_exec</c> 하나가 조회·등록·수정을 다 했다.
/// 조건 셋을 이어 붙이던 것을 그대로 옮겼다.
/// </para>
///
/// <list type="bullet">
///   <item><c>cm_rid</c> 를 주면 그 한 건.</item>
///   <item>
///     <c>srch_type</c> 이 <b>비어 있지 않으면</b> 묶음만(상위 코드가 빈 줄).
///     값이 무엇이든 상관없었다 — <c>!= ''</c> 만 봤다. 그래서 여기서는
///     <see cref="bool"/> 하나로 받는다(<c>groupsOnly</c>).
///   </item>
///   <item><c>cm_pcd</c> 를 주면 그 묶음에 속한 코드만.</item>
/// </list>
///
/// <para>
/// [고친 것 둘 — 프로젝트 표와 같은 결함이다]
/// </para>
///
/// <list type="number">
///   <item>
///     <b>번호를 <c>max(cm_rid)+1</c> 로 만들었다.</b> 동시에 둘이 등록하면
///     같은 번호가 나온다. <c>insert … select</c> 한 문장으로 좁혔다 —
///     진짜 해법은 시퀀스다.
///   </item>
///   <item>
///     <b>정렬 순서(<c>cm_srt</c>)를 등록에서 안 받았다.</b> 수정에서만
///     다뤄서 새 코드는 정렬값이 비어 있었고(표에 기본값도 없다), 그러면
///     <c>ORDER BY cm_srt</c> 에서 <b>맨 앞으로 온다</b>(PostgreSQL 은
///     <c>NULL</c> 을 오름차순 끝에 두지만 정렬 키가 하나뿐이라 자리가
///     들쭉날쭉해진다). 이제 등록에서도 받고, 안 주면 999 다 — 옛 수정이
///     쓰던 기본값과 같은 값이다.
///   </item>
/// </list>
/// </remarks>
public sealed class DevCommonCodeService(IConfiguration configuration)
{
    private readonly string _connectionString =
        configuration.GetConnectionString("jsini")
        ?? throw new InvalidOperationException("연결 문자열 'jsini' 가 없습니다.");

    private IDbConnection Open() => new NpgsqlConnection(_connectionString);

    private const string Columns = """
        cm_rid  AS CmRid,
        cm_cd   AS CmCd,
        cm_nm   AS CmNm,
        cm_prop AS CmProp,
        cm_pcd  AS CmPcd,
        cm_val  AS CmVal,
        cm_type AS CmType,
        cm_val2 AS CmVal2,
        cm_val3 AS CmVal3,
        cm_srt  AS CmSrt,
        cm_rmk  AS CmRmk
        """;

    /// <summary>
    /// 공통코드 목록.
    /// </summary>
    /// <param name="groupsOnly">참이면 묶음만(상위 코드가 빈 줄).</param>
    /// <param name="parentCode">그 묶음에 속한 코드만.</param>
    /// <param name="cmRid">한 건만.</param>
    public async Task<List<DevCommonCode>> ListAsync(
        bool groupsOnly = false, string? parentCode = null, int? cmRid = null)
    {
        using var db = Open();

        var rows = await db.QueryAsync<DevCommonCode>($"""
            SELECT {Columns}
              FROM projmng.devcomm
             WHERE (@cmRid IS NULL OR cm_rid = @cmRid)
               AND (@groupsOnly = false OR COALESCE(cm_pcd, '') = '')
               AND (@parentCode = '' OR cm_pcd = @parentCode)
             ORDER BY cm_srt, cm_rid
            """, new { cmRid, groupsOnly, parentCode = parentCode ?? string.Empty });

        return [.. rows];
    }

    /// <summary>새 코드. 번호는 서버가 정한다.</summary>
    public async Task<DevCommonCode> CreateAsync(DevCommonCode item)
    {
        using var db = Open();

        return await db.QuerySingleAsync<DevCommonCode>($"""
            INSERT INTO projmng.devcomm
                 ( cm_rid, cm_cd, cm_nm, cm_prop, cm_pcd,
                   cm_val, cm_type, cm_val2, cm_val3, cm_srt, cm_rmk )
            SELECT COALESCE(MAX(cm_rid), 0) + 1,
                   @CmCd, @CmNm, @CmProp, @CmPcd,
                   @CmVal, @CmType, @CmVal2, @CmVal3,
                   COALESCE(@CmSrt, 999), @CmRmk
              FROM projmng.devcomm
            RETURNING {Columns}
            """, item);
    }

    /// <summary>코드를 고친다.</summary>
    /// <returns>고친 것. 그 번호가 없으면 <c>null</c>.</returns>
    public async Task<DevCommonCode?> UpdateAsync(int cmRid, DevCommonCode item)
    {
        using var db = Open();

        return await db.QuerySingleOrDefaultAsync<DevCommonCode>($"""
            UPDATE projmng.devcomm
               SET cm_cd   = @CmCd,
                   cm_nm   = @CmNm,
                   cm_prop = @CmProp,
                   cm_pcd  = @CmPcd,
                   cm_val  = @CmVal,
                   cm_type = @CmType,
                   cm_val2 = @CmVal2,
                   cm_val3 = @CmVal3,
                   cm_srt  = COALESCE(@CmSrt, 999),
                   cm_rmk  = @CmRmk
             WHERE cm_rid = @cmRid
            RETURNING {Columns}
            """, new
        {
            cmRid,
            item.CmCd, item.CmNm, item.CmProp, item.CmPcd,
            item.CmVal, item.CmType, item.CmVal2, item.CmVal3,
            item.CmSrt, item.CmRmk,
        });
    }

    /// <summary>
    /// 코드를 지운다.
    ///
    /// <para>
    /// <b>묶음을 지우면 그 아래 코드가 남는다.</b> 표에 외래키가 없어 DB 가
    /// 막아 주지 않으므로, 지우기 전에 딸린 것이 있는지 부르는 쪽이 확인한다
    /// (<see cref="CountChildrenAsync"/>).
    /// </para>
    /// </summary>
    public async Task<bool> DeleteAsync(int cmRid)
    {
        using var db = Open();

        var affected = await db.ExecuteAsync(
            "DELETE FROM projmng.devcomm WHERE cm_rid = @cmRid", new { cmRid });

        return affected > 0;
    }

    /// <summary>이 코드값을 상위로 두는 줄이 몇이나 되나.</summary>
    public async Task<int> CountChildrenAsync(string code)
    {
        using var db = Open();

        return await db.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM projmng.devcomm WHERE cm_pcd = @code", new { code });
    }
}
