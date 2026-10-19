using System.Data;
using Dapper;
using Npgsql;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>프로젝트관리 공통코드(projmng.devcomm). 조회는 cm_rid · cm_pcd · srch_type 중 하나로 갈린다.</summary>
/// 번호는 max+1 이라 동시 등록이 겹칠 수 있다 — 진짜 해법은 시퀀스다. 정렬 순서는 등록에서도 받고 안 주면 999 다.
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

    /// <summary>공통코드 목록.</summary>
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

    /// <summary>코드를 지운다. 묶음을 지우면 그 아래 코드가 남으므로 지우기 전에 딸린 것이 있는지 본다.</summary>
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
