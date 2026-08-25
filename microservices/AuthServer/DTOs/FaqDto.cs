namespace AuthServer.DTOs;

/// <summary>
/// F.A.Q 조회 결과
/// </summary>
public class FaqDto
{
    public string Id { get; set; } = string.Empty;

    /// <summary>분류. 비우면 화면이 '기타' 로 묶는다.</summary>
    public string? Category { get; set; }

    public string Question { get; set; } = string.Empty;

    /// <summary>답변 (HTML)</summary>
    public string? Answer { get; set; }

    public int OrderNo { get; set; }

    /// <summary>0: 비활성, 1: 활성</summary>
    public int Status { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// F.A.Q 등록·수정 요청
/// </summary>
public class SaveFaqDto
{
    public string? Category { get; set; }
    public string Question { get; set; } = string.Empty;
    public string? Answer { get; set; }
    public int OrderNo { get; set; }
    public int Status { get; set; } = 1;
}

/// <summary>
/// F.A.Q 목록 응답
/// </summary>
/// <remarks>
/// 목록과 함께 "이 사용자가 관리자인지"를 내려준다.
/// 화면이 권한 스토어만 보고 판단하면, 권한 정보가 늦게 도착했을 때
/// 서버 판정과 어긋난 버튼이 보인다. 판정은 서버 한 곳에서 한다.
/// </remarks>
public class FaqListDto
{
    public List<FaqDto> Items { get; set; } = new();

    /// <summary>등록·수정·삭제할 수 있는 사용자인지</summary>
    public bool CanManage { get; set; }

    /// <summary>지금 등록된 분류 목록. 등록 창의 분류 추천에 쓴다.</summary>
    public List<string> Categories { get; set; } = new();
}
