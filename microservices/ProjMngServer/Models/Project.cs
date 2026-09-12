namespace ProjMngServer.Models;

/// <summary>
/// 관리 대상 프로젝트 한 건 — <c>projmng.dev_proj</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>기존 <c>ProjModel.ProjectInfo</c> 를 쓰지 않는다.</b> 그것은 칸이 일곱
/// 개뿐이고(금액·정렬·일시가 없다) 값이 전부 <c>string?</c> 이다 — 프로시저에
/// 파라미터를 문자열로 실어 보내던 시절의 그릇이라, 날짜와 금액이 오갈 때마다
/// 화면과 서버가 각자 파싱했다.
/// </para>
///
/// <para>
/// 여기서는 <b>DB 의 타입을 그대로</b> 받는다. 날짜는 <see cref="DateOnly"/>,
/// 금액은 <see cref="int"/> 다. 형식을 맞추는 일은 화면 한 곳에서 한다.
/// </para>
/// </remarks>
public sealed class Project
{
    /// <summary>프로젝트 번호. <b>등록할 때는 서버가 정한다</b>(요청 값은 무시).</summary>
    public int PrjRid { get; set; }

    public string? PrjName { get; set; }
    public string? PrjDesc { get; set; }

    /// <summary>시작일. DB 가 <c>date</c> 라 시각이 없다.</summary>
    public DateOnly? PrjSdt { get; set; }

    public DateOnly? PrjEdt { get; set; }

    /// <summary>별칭. 화면 드롭다운에 이 이름이 나온다.</summary>
    public string? PrjNick { get; set; }

    /// <summary>구분(<c>blazor</c> · <c>vue</c> …).</summary>
    public string? PrjType { get; set; }

    /// <summary>총 수주비용.</summary>
    public int? ProjPay { get; set; }

    /// <summary>총 투입비용.</summary>
    public int? PrjUsePay { get; set; }

    /// <summary>정렬 순서. 기본값은 DB 가 정한다(99999).</summary>
    public int? PrjSrt { get; set; }

    /// <summary>
    /// 수정 일시. <b>서버가 채운다</b> — 요청에 실려 와도 쓰지 않는다.
    /// </summary>
    public DateTime? ModDt { get; set; }

    /// <summary>
    /// 등록 일시. <b>등록할 때만 채운다.</b>
    ///
    /// <para>
    /// 옛 프로시저는 <b>수정할 때도 이 값을 <c>now()</c> 로 덮었다</b> —
    /// 그래서 한 번이라도 고친 프로젝트는 등록일을 잃었다. 옮기면서 고친다.
    /// </para>
    /// </summary>
    public DateTime? CreDt { get; set; }
}
