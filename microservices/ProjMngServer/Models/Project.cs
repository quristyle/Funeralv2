namespace ProjMngServer.Models;

/// <summary>
/// 관리 대상 프로젝트 한 건 — <c>projmng.dev_proj</c>.
/// </summary>
/// 기존 ProjModel.ProjectInfo 를 쓰지 않는다 — 칸이 일곱뿐이고 값이 전부 string? 라 날짜·금액이 오갈 때마다 파싱해야 했다.
/// 여기서는 DB 타입을 그대로 받는다(날짜 DateOnly · 금액 int). 형식을 맞추는 일은 화면 한 곳에서 한다.
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
    /// 등록할 때만 채운다 — 수정 때 덮어쓰면 등록일이 사라진다.
    /// </summary>
    public DateTime? CreDt { get; set; }
}
