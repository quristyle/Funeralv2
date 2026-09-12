namespace ProjMngServer.Models;

/// <summary>
/// 프로젝트관리 공통코드 한 건 — <c>projmng.devcomm</c>.
/// </summary>
/// <remarks>
/// <para>
/// 묶음과 코드가 <b>같은 표에</b> 있다. 상위 코드(<see cref="CmPcd"/>)가 비어
/// 있으면 묶음이고, 채워져 있으면 그 묶음에 속한 코드다.
/// </para>
///
/// <para>
/// <b>표에 <c>cm_rmk</c> 칸이 하나 더 있는데 옛 프로시저가 읽지도 쓰지도
/// 않았다.</b> 화면에 나온 적이 없으므로 값이 들어 있을 리도 없지만, 있는
/// 칸을 없는 것처럼 두면 나중에 「왜 비어 있지」가 된다. 그래서 여기서는
/// 담아 온다(<see cref="CmRmk"/>).
/// </para>
/// </remarks>
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

    /// <summary>비고. <b>옛 프로시저가 다루지 않던 칸</b>이다(머리말).</summary>
    public string? CmRmk { get; set; }
}
