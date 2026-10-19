using System.Data;
using Dapper;
using Npgsql;

namespace ProjMngServer.Services;

/// <summary>
/// 드롭다운 목록을 한 통로로 준다. 고르개는 이 통로 하나를 codeId 만 바꿔 부른다.
/// 한 줄은 사전이다 — code·name·desc 에 그 표의 칸이 전부 실린다(db_nick · db_type · db_schema 를 쓰기 때문이다). DB 목록은 db_pwd 를 내보내지 않고, projlist 만 사람이 참여한 프로젝트로 좁힌다.
/// </summary>
public sealed class ProjCodeService(IConfiguration configuration) {

  private readonly string _connectionString =
      configuration.GetConnectionString("jsini")
      ?? throw new InvalidOperationException("ConnectionStrings:jsini 가 없습니다.");

  /// <summary>DB 접속 목록에서 <b>내보내지 않는</b> 칸.</summary>
  private const string DbColumns =
      "a.db_rid, a.db_ip, a.db_port, a.db_database, a.db_id, a.db_cert, " +
      "a.db_comm, a.db_nick, a.db_type, a.db_schema, a.prj_rid";

  /// <summary>
  /// 목록을 읽는다.
  /// </summary>
  /// <param name="codeId">
  /// 코드 묶음 이름(<c>TODO_STATE</c> · <c>HOMEWORK</c> …) 또는 이름 있는
  /// 갈래(<c>projlist</c> · <c>sourcelist</c> · <c>projdb</c> · <c>projdb2</c> ·
  /// <c>wbsflowlist</c>).
  /// </param>
  /// <param name="etc0">갈래 안에서 다시 좁히는 값. 대개 프로젝트 번호다.</param>
  /// <param name="userId">부르는 사람. <c>projlist</c> 에서만 쓴다.</param>
  /// <param name="ct">취소 토큰</param>
  public async Task<IReadOnlyList<Dictionary<string, object?>>> ListAsync(
      string codeId, string? etc0, string? userId, CancellationToken ct = default) {

    await using var db = new NpgsqlConnection(_connectionString);

    // 공통코드 묶음이 있으면 그것이 이긴다.
    var isGroup = await db.ExecuteScalarAsync<bool>(new CommandDefinition(
        "SELECT EXISTS (SELECT 1 FROM projmng.devcomm WHERE cm_pcd = upper(@codeId))",
        new { codeId }, cancellationToken: ct));

    if (isGroup) {
      return await QueryAsync(db, """
          SELECT a.cm_cd AS code, a.cm_nm AS name, a.cm_val AS "desc", a.*
            FROM projmng.devcomm a
           WHERE a.cm_pcd = upper(@codeId)
           ORDER BY a.cm_srt, a.cm_nm
          """, new { codeId }, ct);
    }

    var project = string.IsNullOrWhiteSpace(etc0) ? (int?)null
                : int.TryParse(etc0, out var rid) ? rid : null;

    return codeId.ToLowerInvariant() switch {

      // 참여한 프로젝트만. 신원이 없으면(게이트웨이를 안 지난 직접 호출) 전부.
      "projlist" => await QueryAsync(db, """
          SELECT a.prj_rid AS code, a.prj_name AS name, a.prj_desc AS "desc", a.*
            FROM projmng.dev_proj a
           WHERE (@userId = ''
                  OR a.prj_rid IN (SELECT prj_rid FROM projmng.dev_proj_user_map
                                    WHERE user_id = @userId))
           ORDER BY a.prj_srt
          """, new { userId = userId ?? string.Empty }, ct),

      "sourcelist" => await QueryAsync(db, """
          SELECT a.src_rid AS code, a.src_nick AS name, a.src_comm AS "desc", a.*
            FROM projmng.dev_srcinfo a
           WHERE (@project::int IS NULL OR a.prj_rid = @project)
           ORDER BY a.src_nick
          """, new { project }, ct),

      // 고르는 값이 번호다. 화면이 그 번호로 접속을 집는다.
      "projdb" => await QueryAsync(db, $"""
          SELECT a.db_rid AS code, a.db_nick AS name, a.db_comm AS "desc", {DbColumns}
            FROM projmng.devdbinfo a
           WHERE (@project::int IS NULL OR a.prj_rid = @project)
           -- 사람이 정한 차례가 먼저다. 빈 줄은 맨 뒤로 보내고 이름으로
           -- 줄 세운다 — 옛 차례(가나다)가 그때 그대로 남는다.
           ORDER BY a.db_srt NULLS LAST, a.db_nick
          """, new { project }, ct),

      // 고르는 값이 별칭이다. 프로시저를 부르던 화면들이 별칭을 넘겼다.
      "projdb2" => await QueryAsync(db, $"""
          SELECT a.db_nick AS code, a.db_nick AS name, a.db_comm AS "desc", {DbColumns}
            FROM projmng.devdbinfo a
           WHERE (@project::int IS NULL OR a.prj_rid = @project)
           -- 사람이 정한 차례가 먼저다. 빈 줄은 맨 뒤로 보내고 이름으로
           -- 줄 세운다 — 옛 차례(가나다)가 그때 그대로 남는다.
           ORDER BY a.db_srt NULLS LAST, a.db_nick
          """, new { project }, ct),

      "wbsflowlist" => await QueryAsync(db, """
          SELECT a.proc_tp AS code, a.proc_tp AS name, '' AS "desc"
            FROM projmng.dev_wbs a
           WHERE COALESCE(a.proc_tp, '') <> ''
             AND (@project::int IS NULL OR a.prj_rid = @project)
           GROUP BY a.proc_tp
           ORDER BY a.proc_tp
          """, new { project }, ct),

      // 없는 이름이면 빈 목록이다 — 고르개 하나만 비고 화면은 뜬다.
      _ => [],
    };
  }

  /// <summary>한 줄을 사전으로 읽는다. 같은 이름의 칸은 앞엣것이 남는다.</summary>
  private static async Task<IReadOnlyList<Dictionary<string, object?>>> QueryAsync(
      IDbConnection db, string sql, object parameters, CancellationToken ct) {

    var rows = await db.QueryAsync(new CommandDefinition(sql, parameters, cancellationToken: ct));

    var list = new List<Dictionary<string, object?>>();

    foreach (IDictionary<string, object?> row in rows) {
      var item = new Dictionary<string, object?>(StringComparer.Ordinal);

      foreach (var (key, value) in row) {
        // `a.*` 가 code·name·desc 와 겹치는 칸을 다시 낸다. 먼저 들어온
        // 별칭이 이긴다 — 화면이 보는 이름이 그쪽이다.
        item.TryAdd(key, value);
      }

      list.Add(item);
    }

    return list;
  }
}
