using JSini.Shared.Domain;

namespace SiteServer.Entities;

/// <summary>
/// 소개 사이트에서 남긴 문의. 로그인하지 않은 사람이 쓰는 유일한 표다.
/// </summary>
/// <remarks>
/// 익명 쓰기라 두 가지를 함께 둔다.
///
/// 1. <b>스팸 방어</b>는 세 겹이다 — 허니팟 필드(사람은 못 보는 칸이 채워져 있으면 봇),
///    게이트웨이의 IP 레이트리밋(<c>public-write</c>), 그리고 <see cref="ClientIp"/> 기록.
///    캡차는 외부 스크립트를 불러야 해서 지금은 두지 않는다 (결정 D-S4).
///
/// 2. <b>개인정보</b>다. 이름 · 연락처 · 이메일이 들어온다.
///    <see cref="ConsentedAt"/> 이 비어 있는 행은 만들지 않는다 — 동의 없이 받은 것이 되기 때문이다.
///    보관 기간과 동의 문구는 회사가 확정해야 한다 (결정 D-S7).
///    <b>그 문구가 나오기 전까지 화면에 폼을 열지 않는다.</b>
/// </remarks>
public class SiteInquiry : BaseEntity<Guid>
{
    /// <summary>보낸 사람 이름</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>회사명. 안 적어도 된다</summary>
    public string? Company { get; set; }

    /// <summary>이메일. 답을 보낼 곳이라 필수로 받는다</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>전화번호. 안 적어도 된다</summary>
    public string? Phone { get; set; }

    /// <summary>문의 분류 (예: <c>영업</c> · <c>기술</c> · <c>채용</c>)</summary>
    public string? Category { get; set; }

    /// <summary>제목</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>본문</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 개인정보 수집·이용에 동의한 시각. **비어 있을 수 없다.**
    /// 서비스가 접수할 때 채우며, 동의 표시가 없으면 접수 자체를 거절한다.
    /// </summary>
    public DateTime ConsentedAt { get; set; }

    /// <summary>접수 당시의 클라이언트 아이피. 스팸 추적용이다</summary>
    public string? ClientIp { get; set; }

    /// <summary>접수 당시의 User-Agent</summary>
    public string? UserAgent { get; set; }

    /// <summary>언어. 어느 쪽 사이트에서 왔는지 (<c>ko</c> · <c>en</c>)</summary>
    public string Locale { get; set; } = "ko";

    /// <summary>처리 상태. <c>new</c> · <c>reading</c> · <c>answered</c> · <c>spam</c></summary>
    public string Status { get; set; } = "new";

    /// <summary>담당자가 남기는 내부 메모. 방문자에게 보이지 않는다</summary>
    public string? InternalNote { get; set; }
}

/// <summary>
/// 화면 조회 집계. 한 행이 (날짜 · 경로 · 언어) 하나다.
/// </summary>
/// <remarks>
/// 방문자 한 명 한 명을 남기지 않는다. 개인을 특정할 값을 쌓지 않으려는 것이고,
/// 소개 사이트에서 알고 싶은 것은 "어느 페이지가 읽히는가" 뿐이다.
/// 그래서 아이피도 세션도 두지 않고 날짜별 횟수만 올린다.
/// </remarks>
public class SiteVisit : BaseEntity<Guid>
{
    /// <summary>집계 날짜 (UTC 기준 날짜만)</summary>
    public DateTime VisitDate { get; set; }

    /// <summary>경로 (예: <c>/ko/about</c>)</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>언어</summary>
    public string Locale { get; set; } = "ko";

    /// <summary>그 날 그 경로의 조회 수</summary>
    public int ViewCount { get; set; }
}
