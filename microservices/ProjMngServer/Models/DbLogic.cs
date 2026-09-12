namespace ProjMngServer.Models;

/// <summary>
/// 시스템 질의 하나의 <b>이름표</b>. 무엇을 하는 질의인지만 담는다
/// (<c>tablelist</c> · <c>proclist</c> · <c>columnsOftable</c> …).
/// </summary>
/// <remarks>
/// 실제 SQL 은 <see cref="DbLogicQuery"/> 에 DB 종류별로 따로 있다 —
/// 같은 「테이블 목록」이라도 PostgreSQL 과 MSSQL 의 질의가 다르기 때문이다.
/// </remarks>
public sealed class DbLogicBase {

  /// <summary>질의 이름. <b>열쇠다</b> — 화면과 서버가 이 글자로 질의를 찾는다.</summary>
  public string DslCd { get; set; } = string.Empty;

  public string? Comm { get; set; }

  /// <summary>목록 순서. 비우면 999.</summary>
  public int? Sort { get; set; }

  /// <summary>등록된 DB 종류 수. <b>읽기 전용</b> — 서버가 센다.</summary>
  public int QueryCount { get; set; }
}

/// <summary>DB 종류 하나에 대한 실제 SQL.</summary>
public sealed class DbLogicQuery {

  public long DslId { get; set; }

  /// <summary>DB 종류(<c>POSTGRESQL</c> · <c>MSSQL</c> · <c>MYSQL</c> …).</summary>
  public string? DslType { get; set; }

  /// <summary>어느 이름표에 딸린 질의인가.</summary>
  public string? DslCd { get; set; }

  public string? DslQuery { get; set; }

  public string? Comm { get; set; }
}
