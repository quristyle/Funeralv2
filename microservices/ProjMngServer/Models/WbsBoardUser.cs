namespace ProjMngServer.Models;

/// <summary>
/// 개발자 명부 한 줄 — <c>projmng.wbs_user</c>.
/// </summary>
/// <remarks>
/// <para>
/// 원장의 담당자 칸(<c>user_bp_id</c>)이 <see cref="BpId"/> 를 가리킨다.
/// 그래서 이 표가 비면 대시보드의 사람 이름이 전부 사번으로 보인다.
/// </para>
///
/// <para>
/// [칸이 서른 넷인 까닭]
/// </para>
///
/// <para>
/// 명부이면서 <b>장비 대장</b>이다 — 노트북·모니터 세 대의 장비번호와 확인번호,
/// MAC, 하계·동계 옷 치수까지 같은 줄에 있다. 포털 계정에는 그런 칸이 없어서
/// 표를 통째로 계정 쪽에 넘기지 못했고, 대신 <see cref="LoginId"/> 하나로 잇는다.
/// </para>
/// </remarks>
public sealed class WbsBoardUser
{
    /// <summary>사번. 프로젝트 안에서 열쇠다.</summary>
    public string? BpId { get; set; }

    /// <summary>
    /// 포털 계정(로그인 아이디). 채우면 화면이 포털에서 이름과 얼굴을 가져온다.
    /// <b>비어 있어도 된다</b> — 아직 계정을 안 이은 사람이다.
    /// </summary>
    public string? LoginId { get; set; }

    public string? Name { get; set; }
    public string? PositionNm { get; set; }
    public string? Email { get; set; }
    public string? TelNo { get; set; }
    public string? EmergTelNo { get; set; }
    public string? BirthDt { get; set; }

    // 계정 발급 현황 — 「이 사람이 무엇을 받았나」를 적어 두는 칸들이다.
    public string? Git { get; set; }
    public string? Startkit { get; set; }
    public string? Dxb { get; set; }
    public string? VmConn { get; set; }
    public string? Aipro { get; set; }
    public string? Claudecode { get; set; }
    public string? DevDb { get; set; }
    public string? Wiki { get; set; }
    public string? Projectview { get; set; }
    public string? Svn { get; set; }

    /// <summary>ProjectView 사용자 id(<c>USR-…</c>). 워크플로 담당자를 이름 대신 이 값으로 찾는다.</summary>
    public string? PvUserId { get; set; }

    // 장비
    public string? Notebook { get; set; }
    public string? HubHdmi { get; set; }
    public string? SummerSize { get; set; }
    public string? WinterSize { get; set; }
    public string? MacAddr { get; set; }
    public string? NotebookNo { get; set; }
    public string? NotebookChkNo { get; set; }
    public string? Monitor1No { get; set; }
    public string? Monitor1ChkNo { get; set; }
    public string? Monitor2No { get; set; }
    public string? Monitor2ChkNo { get; set; }
    public string? Monitor3No { get; set; }
    public string? Monitor3ChkNo { get; set; }

    /// <summary>
    /// 사용 IP. 사내에서는 <b>이것이 인증 전부</b>였다(접속 IP 를 이 값과 대조).
    /// 포털 안에서는 장비 대장으로만 쓴다 — 권한 판정에 쓰지 않는다.
    /// </summary>
    public string? UseIp { get; set; }

    /// <inheritdoc cref="UseIp"/>
    public string? SuperYn { get; set; }

    /// <inheritdoc cref="UseIp"/>
    public string? BlockYn { get; set; }
}

/// <summary>팀 공유 문서 한 쪽 — <c>projmng.wbs_docs</c>.</summary>
public sealed class WbsBoardDoc
{
    public int Id { get; set; }

    public string? Title { get; set; }

    /// <summary>본문 HTML. 화면의 서식 편집기가 만든 것이다.</summary>
    public string? Content { get; set; }

    public int SortOrder { get; set; }
    public string? UpdatedBy { get; set; }
    public string? UpdatedAt { get; set; }
}

/// <summary>사용자별 화면 설정 한 건 — <c>projmng.wbs_user_pref</c>.</summary>
public sealed class WbsBoardPref
{
    public string? Key { get; set; }

    /// <summary>담아 둔 값. JSON 글자 그대로다.</summary>
    public string? Value { get; set; }

    /// <summary>누구 몫으로 담겼나. 화면이 「누구의 설정인지」를 보여 준다.</summary>
    public string? Who { get; set; }
}
