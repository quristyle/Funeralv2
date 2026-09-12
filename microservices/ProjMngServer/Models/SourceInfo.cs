namespace ProjMngServer.Models;

/// <summary>
/// 소스 정보 한 건 — <c>projmng.dev_srcinfo</c>.
/// </summary>
/// <remarks>
/// 프로젝트 하나에 소스 뿌리가 여럿 붙는다(서버·화면·공용 …). 그 뿌리마다
/// 경로·언어·네임스페이스를 적어 두고, 소스 추적 화면들이 그것을 읽는다.
/// </remarks>
public sealed class SourceInfo
{
    /// <summary>번호. <b>등록할 때는 서버가 정한다.</b></summary>
    public int SrcRid { get; set; }

    public int? PrjRid { get; set; }

    /// <summary>프로젝트 이름·별칭. <b>조인해서 함께 온다</b>(읽기 전용).</summary>
    public string? PrjName { get; set; }

    /// <inheritdoc cref="PrjName"/>
    public string? PrjNick { get; set; }

    public string? SrcOs { get; set; }
    public string? SrcPath { get; set; }
    public string? SrcNick { get; set; }
    public string? SrcType { get; set; }
    public string? SrcLang { get; set; }
    public string? SrcComm { get; set; }

    /// <summary>화면 소스의 뿌리 폴더.</summary>
    public string? SrcUiRoot { get; set; }

    public string? PrjNamespace { get; set; }

    /// <summary>
    /// 이 소스에 걸린 URL 패턴 중 하나. <b>상세에서 끌어온 값</b>이라 읽기 전용이다.
    /// </summary>
    public string? UrlPattern { get; set; }
}

/// <summary>소스 정보의 상세 한 줄 — <c>projmng.dev_srcinfo_dtl</c>.</summary>
public sealed class SourceInfoDetail
{
    /// <summary>번호. <b>등록할 때는 서버가 정한다.</b></summary>
    public int SrcDtlRid { get; set; }

    public int? SrcRid { get; set; }

    public string? SrcExtend { get; set; }

    /// <summary>패턴 묶음. <c>url</c> 이면 위쪽 목록의 URL 패턴으로 올라간다.</summary>
    public string? SrcPatternGrp { get; set; }

    public string? UrlPattern { get; set; }
    public string? SrcPatternComment { get; set; }

    /// <summary>
    /// <b>등록할 때만 쓰이던 칸</b>이다 — 옛 프로시저의 update 문에 이 칸이
    /// 없어서, 한 번 넣은 값을 화면에서 고칠 방법이 없었다. 아래
    /// <c>SourceInfoService</c> 머리말 참고.
    /// </summary>
    public string? SrcPatternNullvalue { get; set; }
}
