namespace ProjMngServer.Models;

/// <summary>프로젝트가 바라보는 DB 접속 한 건 — <c>projmng.devdbinfo</c>.</summary>
/// <summary>프로젝트가 바라보는 DB 접속 한 건(projmng.devdbinfo). 개발 도구 화면들이 이 표로 대상 DB 에 붙는다.</summary>
/// 비밀번호는 목록에 실어 보내지 않는다 — 목록에서는 비어 있고, 저장할 때 비우면 기존 값을 그대로 둔다.
public sealed class ProjectDb
{
    /// <summary>번호. <b>등록할 때는 서버가 정한다.</b></summary>
    public int DbRid { get; set; }

    public int? PrjRid { get; set; }

    /// <summary>조인해서 오는 값. <b>고칠 수 없다.</b></summary>
    public string? PrjName { get; set; }

    /// <inheritdoc cref="PrjName"/>
    public string? PrjNick { get; set; }

    /// <summary>보여 줄 차례. 작을수록 먼저이며 비면 맨 뒤로 간다.</summary>
    /// 셋 다 이 값을 먼저 보며, 값은 서로 달라야 끌어 옮기기가 다른 프로젝트의 차례를 흔들지 않는다.
    public int? DbSrt { get; set; }

    public string? DbNick { get; set; }
    public string? DbType { get; set; }
    public string? DbIp { get; set; }
    public string? DbPort { get; set; }
    public string? DbDatabase { get; set; }
    public string? DbSchema { get; set; }
    public string? DbId { get; set; }

    /// <summary>비밀번호. 목록에서는 늘 비어 있고, 저장할 때 비워 두면 기존 값을 그대로 둔다.</summary>
    public string? DbPwd { get; set; }

    public string? DbCert { get; set; }

    /// <summary>설명.</summary>
    public string? DbComm { get; set; }
}

/// <summary>DB 접속에 딸린 속성 한 줄 — <c>projmng.dev_db_prop</c>.</summary>
/// 값은 여러 줄 질의라 편집기를 오갈 때 빈 줄이 쌓이므로, 저장할 때 빈 줄을 걷어 낸다.
public sealed class ProjectDbProp
{
    /// <summary>속성 번호. <b>등록할 때는 서버가 정한다.</b></summary>
    public int DbPrid { get; set; }

    /// <summary>어느 접속의 속성인가.</summary>
    public int DbRid { get; set; }

    public string? DbPkey { get; set; }
    public string? DbPvalue { get; set; }

    /// <summary>설명·구분.</summary>
    public string? DbPcomment { get; set; }

    /// <inheritdoc cref="DbPcomment"/>
    public string? DbPtype { get; set; }

    public DateTime? ModDt { get; set; }
    public DateTime? CreDt { get; set; }
}
