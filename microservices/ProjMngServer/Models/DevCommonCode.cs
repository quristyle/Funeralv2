namespace ProjMngServer.Models;

/// <summary>
/// 프로젝트관리 공통코드 한 건 — <c>projmng.devcomm</c>.
/// </summary>
/// 묶음과 코드가 같은 표에 있다 — 상위 코드(CmPcd)가 비어 있으면 묶음이다. cm_rmk 칸은 화면이 다루지 않지만 있는 칸을 없는 것처럼 두지 않으려고 담아 온다.
public sealed class DevCommonCode
{
    /// <summary>번호. <b>등록할 때는 서버가 정한다.</b></summary>
    public int CmRid { get; set; }

    /// <summary>코드값.</summary>
    public string? CmCd { get; set; }

    /// <summary>이름.</summary>
    public string? CmNm { get; set; }

    /// <summary>
    /// 상위 코드. <b>비어 있으면 묶음</b>이고 채워져 있으면 그 묶음의 코드다.
    /// </summary>
    public string? CmPcd { get; set; }

    public string? CmProp { get; set; }
    public string? CmVal { get; set; }
    public string? CmType { get; set; }
    public string? CmVal2 { get; set; }
    public string? CmVal3 { get; set; }

    /// <summary>정렬 순서. 비우면 999.</summary>
    public int? CmSrt { get; set; }

    /// <summary>비고.</summary>
    public string? CmRmk { get; set; }
}
