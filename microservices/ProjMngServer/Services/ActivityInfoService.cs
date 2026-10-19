using Dapper;
using Npgsql;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>Glue 서비스 정의(md_glue_service). 서버가 *-service.xml · *.glue_sql 을 훑어 쌓는다.</summary>
/// 재수집은 소스의 옛 줄을 한 거래 안에서 먼저 비운다 — 덮어쓰기만 하면 파일에서 사라진 서비스가 DB 에 남는다.
public sealed class ActivityInfoService(IConfiguration configuration) {

  private readonly string _connectionString =
      configuration.GetConnectionString("jsini")
      ?? throw new InvalidOperationException("ConnectionStrings:jsini 가 없습니다.");

  private const string Columns = """
      servicename     AS ServiceName,
      transitionname  AS TransitionName,
      transitionvalue AS TransitionValue,
      dao             AS Dao,
      procedurename   AS ProcedureName,
      resultkey       AS ResultKey,
      activity        AS Activity,
      activity_type   AS ActivityType,
      active_context  AS ActiveContext,
      src_rid         AS SrcRid
      """;

  /// <summary>목록. 소스로만 거른다 — 이 표에는 prj_rid 칸이 없다.</summary>
  public async Task<IReadOnlyList<ActivityInfoRow>> ListAsync(
      string? srcRid = null, CancellationToken ct = default) {

    await using var db = new NpgsqlConnection(_connectionString);

    var rows = await db.QueryAsync<ActivityInfoRow>(new CommandDefinition($"""
        SELECT {Columns}
          FROM projmng.dev_activityinfo a
         WHERE (@srcRid = '' OR a.src_rid = @srcRid)
         ORDER BY a.servicename, a.transitionname
        """, new { srcRid = srcRid ?? string.Empty }, cancellationToken: ct));

    return [.. rows];
  }

  /// <summary>한 줄을 넣거나 고친다. 열쇠는 servicename · transitionname · src_rid 셋이다.</summary>
  public async Task<int> SaveAsync(ActivityInfoRow item, CancellationToken ct = default) {
    await using var db = new NpgsqlConnection(_connectionString);

    return await SaveAsync(db, item, ct);
  }

  private static async Task<int> SaveAsync(
      NpgsqlConnection db, ActivityInfoRow item, CancellationToken ct) {

    var updated = await db.ExecuteAsync(new CommandDefinition("""
        UPDATE projmng.dev_activityinfo
           SET transitionvalue = @TransitionValue,
               dao             = @Dao,
               procedurename   = @ProcedureName,
               resultkey       = @ResultKey,
               activity        = @Activity,
               activity_type   = @ActivityType,
               active_context  = @ActiveContext
         WHERE servicename    = @ServiceName
           AND transitionname = @TransitionName
           AND src_rid        = @SrcRid
        """, item, cancellationToken: ct));

    if (updated > 0) { return updated; }

    return await db.ExecuteAsync(new CommandDefinition("""
        INSERT INTO projmng.dev_activityinfo
             ( servicename, transitionname, transitionvalue, dao, procedurename,
               resultkey, activity, activity_type, active_context, src_rid )
        VALUES ( @ServiceName, @TransitionName, @TransitionValue, @Dao, @ProcedureName,
                 @ResultKey, @Activity, @ActivityType, @ActiveContext, @SrcRid )
        """, item, cancellationToken: ct));
  }

  /// <summary>한 소스의 자료를 통째로 갈아 끼운다. 재수집이 부른다.</summary>
  /// 한 거래 안에서 지우고 다시 넣으므로 중간에 끊겨도 반만 남지 않는다.
  public async Task<int> ReplaceAsync(
      string srcRid, IReadOnlyList<ActivityInfoRow> items, CancellationToken ct = default) {

    await using var db = new NpgsqlConnection(_connectionString);
    await db.OpenAsync(ct);
    await using var tx = await db.BeginTransactionAsync(ct);

    await db.ExecuteAsync(new CommandDefinition(
        "DELETE FROM projmng.dev_activityinfo WHERE src_rid = @srcRid",
        new { srcRid }, tx, cancellationToken: ct));

    var saved = 0;

    foreach (var item in items) {
      item.SrcRid = srcRid;

      saved += await db.ExecuteAsync(new CommandDefinition("""
          INSERT INTO projmng.dev_activityinfo
               ( servicename, transitionname, transitionvalue, dao, procedurename,
                 resultkey, activity, activity_type, active_context, src_rid )
          VALUES ( @ServiceName, @TransitionName, @TransitionValue, @Dao, @ProcedureName,
                   @ResultKey, @Activity, @ActivityType, @ActiveContext, @SrcRid )
          """, item, tx, cancellationToken: ct));
    }

    await tx.CommitAsync(ct);

    return saved;
  }

  /// <summary>한 줄을 지운다.</summary>
  public async Task<bool> DeleteAsync(
      string srcRid, string serviceName, string transitionName, CancellationToken ct = default) {

    await using var db = new NpgsqlConnection(_connectionString);

    var rows = await db.ExecuteAsync(new CommandDefinition("""
        DELETE FROM projmng.dev_activityinfo
         WHERE src_rid = @srcRid
           AND servicename = @serviceName
           AND transitionname = @transitionName
        """, new { srcRid, serviceName, transitionName }, cancellationToken: ct));

    return rows > 0;
  }
}
