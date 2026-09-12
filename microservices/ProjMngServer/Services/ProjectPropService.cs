using Dapper;
using Npgsql;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>
/// 프로젝트 속성. 옛 <c>sp_dev_proj_prop_exec</c> 를 대신한다.
///
/// <para>
/// 유스케이스 화면이 그린 도형을 여기 담는다 — 프로젝트 하나에 이름별로
/// 한 줄이다.
/// </para>
///
/// <para>[옛 프로시저에서 고친 것]</para>
/// <list type="number">
///   <item>
///     <b>지우는 길이 없었다.</b> <c>save</c> 와 조회 둘뿐이라, 한 번 만든
///     속성은 DB 를 직접 만지지 않으면 없앨 수 없었다.
///   </item>
///   <item>
///     <b>저장이 갈래(<c>prop_type</c>)를 못 고쳤다.</b> 열쇠의 일부라
///     그것이 맞다 — 대신 <b>이름을 바꾸는 것도 못 한다</b>는 뜻이므로,
///     화면에서 이름을 고치면 새 줄이 생기고 옛 줄이 남는다. 그 성질을
///     화면 주석에 적어 두었다.
///   </item>
/// </list>
/// </summary>
public sealed class ProjectPropService(IConfiguration configuration) {

  private readonly string _connectionString =
      configuration.GetConnectionString("jsini")
      ?? throw new InvalidOperationException("ConnectionStrings:jsini 가 없습니다.");

  private const string Columns = """
      prj_rid     AS PrjRid,
      prop_cd     AS PropCd,
      prop_val    AS PropVal,
      prop_comm   AS PropComm,
      prop_use_yn AS PropUseYn,
      prop_type   AS PropType
      """;

  /// <summary>목록.</summary>
  /// <param name="prjRid">프로젝트. 비우면 전부.</param>
  /// <param name="propCd">속성 이름. 비우면 전부.</param>
  /// <param name="propType">속성 갈래. 비우면 전부.</param>
  /// <param name="ct">취소 토큰</param>
  public async Task<IReadOnlyList<ProjectProp>> ListAsync(
      string? prjRid = null, string? propCd = null, string? propType = null,
      CancellationToken ct = default) {

    await using var db = new NpgsqlConnection(_connectionString);

    var rows = await db.QueryAsync<ProjectProp>(new CommandDefinition($"""
        SELECT {Columns}
          FROM projmng.dev_proj_prop a
         WHERE (@prjRid = '' OR a.prj_rid = @prjRid)
           AND (@propCd = '' OR a.prop_cd = @propCd)
           AND (@propType = '' OR a.prop_type = @propType)
         ORDER BY a.prop_cd
        """,
        new {
          prjRid = prjRid ?? string.Empty,
          propCd = propCd ?? string.Empty,
          propType = propType ?? string.Empty,
        },
        cancellationToken: ct));

    return [.. rows];
  }

  /// <summary>넣거나 고친다. 열쇠 셋이 같으면 고친다 — 옛 프로시저와 같다.</summary>
  public async Task<ProjectProp?> SaveAsync(ProjectProp item, CancellationToken ct = default) {
    await using var db = new NpgsqlConnection(_connectionString);

    var updated = await db.ExecuteAsync(new CommandDefinition("""
        UPDATE projmng.dev_proj_prop
           SET prop_val    = @PropVal,
               prop_comm   = @PropComm,
               prop_use_yn = @PropUseYn
         WHERE prj_rid   = @PrjRid
           AND prop_cd   = @PropCd
           AND prop_type = @PropType
        """, item, cancellationToken: ct));

    if (updated == 0) {
      await db.ExecuteAsync(new CommandDefinition("""
          INSERT INTO projmng.dev_proj_prop
               ( prj_rid, prop_cd, prop_val, prop_comm, prop_use_yn, prop_type )
          VALUES ( @PrjRid, @PropCd, @PropVal, @PropComm, @PropUseYn, @PropType )
          """, item, cancellationToken: ct));
    }

    return (await ListAsync(item.PrjRid, item.PropCd, item.PropType, ct)).FirstOrDefault();
  }

  /// <summary>지운다. <b>옛 프로시저에는 없던 길이다.</b></summary>
  public async Task<bool> DeleteAsync(
      string prjRid, string propCd, string propType, CancellationToken ct = default) {

    await using var db = new NpgsqlConnection(_connectionString);

    var rows = await db.ExecuteAsync(new CommandDefinition("""
        DELETE FROM projmng.dev_proj_prop
         WHERE prj_rid = @prjRid AND prop_cd = @propCd AND prop_type = @propType
        """, new { prjRid, propCd, propType }, cancellationToken: ct));

    return rows > 0;
  }
}
