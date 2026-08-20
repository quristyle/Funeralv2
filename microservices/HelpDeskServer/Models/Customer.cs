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

    /// <summary>소속 회사 ID</summary>
    public int CompanyId { get; set; }

    /// <summary>
    /// 소속 회사 (Navigation property)
    /// </summary>
    public CustomerCompany? Company { get; set; }

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