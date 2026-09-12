using Dapper;
using Npgsql;
using ProjMngServer.Models;

namespace ProjMngServer.Services;

/// <summary>
/// Glue 서비스 정의. 옛 <c>sp_dev_activityinfo_exec</c> 를 대신한다.
///
/// <para>
/// 서버가 <c>*-service.xml</c> · <c>*.glue_sql</c> 을 훑어 여기 쌓고
/// (<c>md_glue_service</c>), 「Glue 서비스 추적」 화면이 그것을 읽는다.
/// 운영 자료가 3,947줄이다.
/// </para>
///
/// <para>[옛 프로시저에서 고친 것]</para>
/// <list type="number">
///   <item>
///     <b>지우는 길이 아예 없었다.</b> 프로시저가 <c>save</c> 와 조회 둘뿐이라,
///     파일에서 사라진 서비스가 DB 에는 영영 남았다. 재수집해도 지워지지
///     않는다 — 덮어쓰기만 하기 때문이다. 이제 지울 수 있고, <b>재수집은
///     그 소스의 옛 줄을 먼저 비운다</b>(아래 <see cref="ReplaceAsync"/>).
///   </item>
///   <item>
///     <b>열쇠가 셋인데 색인이 없다.</b> 3,947줄을 세 칸으로 훑어 맞히는
///     갱신이 수집 때마다 줄 수만큼 돈다. 옮기면서 바꾸지는 않았다 —
///     색인은 DB 쪽 일이고, 여기서 걸면 배포와 엇갈린다.
///   </item>
/// </list>
/// </summary>
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

  /// <summary>
  /// 목록. <b>소스로만 거른다</b> — 옛 프로시저가 <c>prj_rid</c> 를 보지 않았고
  /// 표에도 그 칸이 없다.
  /// </summary>
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

  /// <summary>
  /// 한 줄을 넣거나 고친다. 열쇠는 <c>servicename</c> · <c>transitionname</c> ·
  /// <c>src_rid</c> 셋이다 — 옛 프로시저와 같다.
  /// </summary>
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

  /// <summary>
  /// 한 소스의 자료를 <b>통째로 갈아 끼운다.</b> 재수집이 부른다.
  ///
  /// <para>
  /// 옛 길은 덮어쓰기만 해서 <b>파일에서 없어진 서비스가 DB 에 남았다.</b>
  /// 추적 화면에는 있는데 소스에는 없는 줄이고, 그것을 사람이 알 방법이 없었다.
  /// 한 거래 안에서 지우고 다시 넣으므로 중간에 끊겨도 반만 남지 않는다.
  /// </para>
  /// </summary>
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
