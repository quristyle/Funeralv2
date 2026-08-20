using System;
using HelpDeskServer.Services;
using System.Collections.Generic;

namespace HelpDeskServer.Models {

  /// <summary>관리자</summary>
  public class Admin : BaseEntity, IPasswordEnabled {
    /// <summary>로그인 ID</summary>
    public string LoginId { get; set; } = string.Empty;

    /// <summary>관리자 이름</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>이메일</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>비밀번호 해시</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>최초 로그인시 암호 변경 필요 여부</summary>
    public bool MustChangePassword { get; set; } = true;

    /// <summary>
    /// 로그인 실패 횟수
    /// </summary>
    public int? FailedLoginAttempts { get; set; }

    /// <summary>
    /// 계정 잠금 종료 시간 (UTC)
    /// </summary>
    public DateTime? LockoutEnd { get; set; }

    /// <summary>사진 URL</summary>
    public string? Photo { get; set; }



    /// <summary>
    /// 소속 팀 목록 (N:N 관계)
    /// </summary>
    public ICollection<AdminTeam> AdminTeams { get; set; } = new List<AdminTeam>();

    /// <summary>
    /// 이 관리자에게 배정된 개선 요청 목록
    /// </summary>
    public ICollection<ImprovementRequest> AssignedRequests { get; set; } = new List<ImprovementRequest>();
    //public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();

    /// <summary>삭제 여부 (Soft Delete)</summary>
    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// 사용자 속성들
    /// </summary>
    //public ICollection<UserProperty> UserProperties { get; set; } = new List<UserProperty>();

  }

}