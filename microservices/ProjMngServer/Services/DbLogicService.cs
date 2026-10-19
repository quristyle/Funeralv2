using Dapper;
using Npgsql;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>시스템 질의 관리(이름표와 DB 종류별 질의).</summary>
/// 이름표를 지울 때 딸린 질의가 있으면 막는다 — 표에 외래키가 없어 DB 가 막아 주지 않는다.
public sealed class DbLogicService(IConfiguration configuration) {

  private readonly string _connectionString =
      configuration.GetConnectionString("jsini")
      ?? throw new InvalidOperationException("ConnectionStrings:jsini 가 없습니다.");

  /// <summary>이름표 목록. 딸린 질의 수를 함께 센다.</summary>
  public async Task<IReadOnlyList<DbLogicBase>> ListAsync(
      string? dslCd = null, CancellationToken ct = default) {

    await using var db = new NpgsqlConnection(_connectionString);

    var rows = await db.QueryAsync<DbLogicBase>(new CommandDefinition("""
        SELECT a.dsl_cd AS DslCd,
               a.comm   AS Comm,
               a.sort   AS Sort,
               (SELECT count(*) FROM projmng.devsqlresp q WHERE q.dsl_cd = a.dsl_cd) AS QueryCount
          FROM projmng.devsqlresp_base a
         WHERE (@dslCd = '' OR a.dsl_cd = @dslCd)
         ORDER BY a.sort, a.dsl_cd
        """, new { dslCd = dslCd ?? string.Empty }, cancellationToken: ct));

    return [.. rows];
  }

  /// <summary>이름표를 넣거나 고친다. 이름이 곧 열쇠라 같은 이름이 있으면 갈린다.</summary>
  /// 이 표에는 제약이 없어 ON CONFLICT 를 쓸 수 없으므로 세어 보고 가른다.
  public async Task<DbLogicBase?> SaveAsync(DbLogicBase item, CancellationToken ct = default) {
    await using var db = new NpgsqlConnection(_connectionString);

    var updated = await db.ExecuteAsync(new CommandDefinition("""
        UPDATE projmng.devsqlresp_base
           SET comm = @Comm,
               sort = COALESCE(@Sort, 999)
         WHERE dsl_cd = @DslCd
        """, item, cancellationToken: ct));

    if (updated == 0) {
      await db.ExecuteAsync(new CommandDefinition("""
          INSERT INTO projmng.devsqlresp_base ( dsl_cd, comm, sort )
          VALUES ( @DslCd, @Comm, COALESCE(@Sort, 999) )
          """, item, cancellationToken: ct));
    }

    return (await ListAsync(item.DslCd, ct)).FirstOrDefault();
  }

  /// <summary>이름표를 지운다. <b>딸린 질의가 있으면 막는다</b>(머리말 3).</summary>
  /// <returns>지웠으면 <c>null</c>, 못 지웠으면 그 이유.</returns>
  public async Task<string?> DeleteAsync(string dslCd, CancellationToken ct = default) {
    await using var db = new NpgsqlConnection(_connectionString);

    var children = await db.ExecuteScalarAsync<int>(new CommandDefinition(
        "SELECT count(*) FROM projmng.devsqlresp WHERE dsl_cd = @dslCd",
        new { dslCd }, cancellationToken: ct));

    if (children > 0) {
      return $"이 이름표에 등록된 질의가 {children}건 있습니다. 먼저 지우십시오.";
    }

    var rows = await db.ExecuteAsync(new CommandDefinition(
        "DELETE FROM projmng.devsqlresp_base WHERE dsl_cd = @dslCd",
        new { dslCd }, cancellationToken: ct));

    return rows > 0 ? null : "없는 이름표입니다.";
  }

  /// <summary>이름표 하나에 딸린 DB 종류별 질의.</summary>
  public async Task<IReadOnlyList<DbLogicQuery>> QueriesAsync(
      string dslCd, CancellationToken ct = default) {

    await using var db = new NpgsqlConnection(_connectionString);

    var rows = await db.QueryAsync<DbLogicQuery>(new CommandDefinition("""
        SELECT dsl_id    AS DslId,
               dsl_type  AS DslType,
               dsl_cd    AS DslCd,
               dsl_query AS DslQuery,
               comm      AS Comm
          FROM projmng.devsqlresp
         WHERE dsl_cd = @dslCd
         ORDER BY dsl_type
        """, new { dslCd }, cancellationToken: ct));

    return [.. rows];
  }

  /// <summary>질의를 새로 넣는다. 번호는 <c>max+1</c> — 다른 표와 같다.</summary>
  public async Task<DbLogicQuery?> CreateQueryAsync(
      DbLogicQuery item, CancellationToken ct = default) {

    await using var db = new NpgsqlConnection(_connectionString);

    return await db.QuerySingleOrDefaultAsync<DbLogicQuery>(new CommandDefinition("""
        INSERT INTO projmng.devsqlresp ( dsl_id, dsl_type, dsl_cd, dsl_query, comm )
        SELECT COALESCE(MAX(dsl_id), 0) + 1, @DslType, @DslCd, @DslQuery, @Comm
          FROM projmng.devsqlresp
        RETURNING dsl_id AS DslId, dsl_type AS DslType, dsl_cd AS DslCd,
                  dsl_query AS DslQuery, comm AS Comm
        """, item, cancellationToken: ct));
  }

  /// <summary>질의를 고친다. <b>번호 하나로 찾는다.</b></summary>
  public async Task<DbLogicQuery?> UpdateQueryAsync(
      DbLogicQuery item, CancellationToken ct = default) {

    await using var db = new NpgsqlConnection(_connectionString);

    return await db.QuerySingleOrDefaultAsync<DbLogicQuery>(new CommandDefinition("""
        UPDATE projmng.devsqlresp
           SET dsl_type  = @DslType,
               dsl_cd    = @DslCd,
               dsl_query = @DslQuery,
               comm      = @Comm
         WHERE dsl_id = @DslId
        RETURNING dsl_id AS DslId, dsl_type AS DslType, dsl_cd AS DslCd,
                  dsl_query AS DslQuery, comm AS Comm
        """, item, cancellationToken: ct));
  }

  /// <summary>질의를 지운다.</summary>
  public async Task<bool> DeleteQueryAsync(long dslId, CancellationToken ct = default) {
    await using var db = new NpgsqlConnection(_connectionString);

    var rows = await db.ExecuteAsync(new CommandDefinition(
        "DELETE FROM projmng.devsqlresp WHERE dsl_id = @dslId",
        new { dslId }, cancellationToken: ct));

    return rows > 0;
  }
}
