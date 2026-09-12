namespace ProjMngServer.Models;

/// <summary>
/// 프로젝트가 바라보는 DB 접속 한 건 — <c>projmng.devdbinfo</c>.
/// </summary>
/// <remarks>
/// <para>
/// 개발 도구 화면들(테이블 관리 · 쿼리 테스터 · ERD)이 이 표를 보고 대상 DB
/// 에 붙는다. <b>이 표가 망가지면 그 화면 전부가 접속을 못 찾는다.</b>
/// </para>
///
/// <para>
/// [비밀번호는 목록에 실어 보내지 않는다]
/// </para>
///
/// <para>
/// 옛 프로시저는 <c>select a.*</c> 라 <c>db_pwd</c> 가 그대로 화면까지 갔다.
/// 목록에서 그 값을 쓰는 자리는 없는데 **사람이 볼 수 있는 곳까지 나가 있었다**
/// — 개발 도구라 해도 남의 DB 비밀번호다.
/// </para>
///
/// <para>
/// 그래서 <see cref="DbPwd"/> 는 <b>목록에서 늘 비어 있다.</b> 저장할 때
/// 비워 두면 <b>기존 값을 그대로 둔다</b> — 그래야 비밀번호를 몰라도 다른 칸을
/// 고칠 수 있다. 바꾸려면 새 값을 적는다.
/// </para>
/// </remarks>
public sealed class ProjectDb
{
    /// <summary>번호. <b>등록할 때는 서버가 정한다.</b></summary>
    public int DbRid { get; set; }

    public int? PrjRid { get; set; }

    /// <summary>조인해서 오는 값. <b>고칠 수 없다.</b></summary>
    public string? PrjName { get; set; }

    /// <inheritdoc cref="PrjName"/>
    public string? PrjNick { get; set; }

    public string? DbNick { get; set; }
    public string? DbType { get; set; }
    public string? DbIp { get; set; }
    public string? DbPort { get; set; }
    public string? DbDatabase { get; set; }
    public string? DbSchema { get; set; }
    public string? DbId { get; set; }

    /// <summary>
    /// 비밀번호. <b>목록에서는 늘 비어 있고</b>, 저장할 때 비워 두면 기존 값을
    /// 그대로 둔다(머리말).
    /// </summary>
    public string? DbPwd { get; set; }

    public string? DbCert { get; set; }

    /// <summary>
    /// 설명. <b>옛 프로시저의 저장에 없던 칸</b>이다 — 목록에는
    /// <c>a.*</c> 로 실려 나갔지만 넣거나 고칠 길이 없었다.
    /// </summary>
    public string? DbComm { get; set; }
}

/// <summary>DB 접속에 딸린 속성 한 줄 — <c>projmng.dev_db_prop</c>.</summary>
/// <remarks>
/// <para>
/// 개발 도구가 대상 DB 를 다룰 때 쓰는 값들이다(질의 조각·설정 …).
/// </para>
///
/// <para>
/// <b>값에서 빈 줄을 걷어 내 온다.</b> 옛 프로시저가
/// <c>REGEXP_REPLACE(db_pvalue, '\r\n\r\n', '')</c> 로 그렇게 했고 그대로
/// 옮겼다 — 값이 여러 줄짜리 질의라 편집기를 오갈 때 빈 줄이 쌓인다.
/// </para>
/// </remarks>
public sealed class ProjectDbProp
{
    /// <summary>속성 번호. <b>등록할 때는 서버가 정한다.</b></summary>
    public int DbPrid { get; set; }

    /// <summary>어느 접속의 속성인가.</summary>
    public int DbRid { get; set; }

    public string? DbPkey { get; set; }
    public string? DbPvalue { get; set; }

    /// <summary>
    /// 설명·구분. <b>옛 저장이 다루지 않던 칸 둘</b>이다 — 목록에는 나가는데
    /// insert 에도 update 에도 없어서 넣을 길이 없었다.
    /// </summary>
    public string? DbPcomment { get; set; }

    /// <inheritdoc cref="DbPcomment"/>
    public string? DbPtype { get; set; }

    public DateTime? ModDt { get; set; }
    public DateTime? CreDt { get; set; }
}
