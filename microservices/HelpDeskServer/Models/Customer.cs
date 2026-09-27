using HelpDeskServer.Services;

namespace HelpDeskServer.Models;

/// <summary>고객</summary>
public class Customer : BaseEntity, IPasswordEnabled
{
    /// <summary>로그인 ID</summary>
    public string LoginId { get; set; } = string.Empty;

    /// <summary>사용자 이름</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>이메일</summary>
    public string Email { get; set; } = string.Empty;
    /// <summary>성별(M/F)</summary>
    public string Sex { get; set; } = "M";

    /// <summary>사진 URL</summary>
    public string Photo { get; set; } = string.Empty;

    /// <summary>비밀번호 해시</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>비고</summary>
    public string? Remake { get; set; } = string.Empty;

    /// <summary>계정 상태</summary>
    public CustomerStatus Status { get; set; } = CustomerStatus.Pending;

    /// <summary>
    /// 로그인 실패 횟수
    /// </summary>
    public int? FailedLoginAttempts { get; set; }

    /// <summary>
    /// 계정 잠금 종료 시간 (UTC)
    /// </summary>
    public DateTime? LockoutEnd { get; set; }

    /// <summary>
    /// 소속 회사 식별자 — <b>포털(<c>scom.companies.id</c>)의 값</b>이다.
    /// </summary>
    /// <remarks>
    /// 헬프데스크는 회사를 스스로 관리하지 않는다. 옛 단독 시스템 시절의
    /// <c>customercompany</c> 표를 걷어내고, 포털이 정본으로 들고 있는 회사를
    /// 그대로 가리킨다. 그래서 형이 <c>int</c> 가 아니라 <c>string</c> 이다
    /// (포털 회사 아이디는 <c>jsini</c> · GUID 같은 글자다).
    ///
    /// 회사를 알 수 없는 줄이 있을 수 있어 <c>null</c> 을 허용한다 — 전에는
    /// NOT NULL 외래키라 회사가 없으면 고객 줄조차 만들 수 없었다.
    /// 이름은 <see cref="Services.IPortalCompanyDirectory"/> 로 푼다.
    /// </remarks>
    public string? CompanyId { get; set; }

    /// <summary>
    /// 이 고객이 생성한 개선 요청 목록
    /// </summary>
    public ICollection<ImprovementRequest> ImprovementRequests { get; set; } = new List<ImprovementRequest>();

    //public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();

    /// <summary>삭제 여부 (Soft Delete)</summary>
    public bool IsDeleted { get; set; } = false;

}


/// <summary>
/// 고객 계정 상태
/// </summary>
public enum CustomerStatus
{
    /// <summary>
    /// 승인 대기
    /// </summary>
    [System.ComponentModel.DataAnnotations.Display(Name = "승인대기")]
    Pending,

    /// <summary>
    /// 승인됨
    /// </summary>
    [System.ComponentModel.DataAnnotations.Display(Name = "승인")]
    Approved,

    /// <summary>
    /// 거부됨
    /// </summary>
    [System.ComponentModel.DataAnnotations.Display(Name = "거부")]
    Rejected
}