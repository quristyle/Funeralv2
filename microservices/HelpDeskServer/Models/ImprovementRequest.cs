using System;
using HelpDeskServer.Services;
using System.Collections.Generic;

namespace HelpDeskServer.Models {

  /// <summary>개선 요청</summary>
  public class ImprovementRequest : BaseEntity {
    /// <summary>제목</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>내용</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>본문의 대표 사진</summary>
    public string? MainPhoto { get; set; }

    /// <summary>요청일시</summary>
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    /// <summary>고객 ID</summary>
    public int CustomerId { get; set; }

    /// <summary>
    /// 요청을 생성한 고객 (Navigation property)
    /// </summary>
    public Customer? Customer { get; set; }

    /// <summary>담당 관리자 ID</summary>
    public int? AdminId { get; set; }

    /// <summary>
    /// 요청에 배정된 관리자 (Navigation property)
    /// </summary>
    public Admin? Admin { get; set; }

    /// <summary>처리완료일시</summary>
    public DateTime? CompletededAt { get; set; }


    /// <summary>사용자완료(종료)일시</summary>
    public DateTime? UserCompletededAt { get; set; }





    /// <summary>상태</summary>
    public ImprovementStatus Status { get; set; } = ImprovementStatus.Pending;

    /// <summary>개선 유형</summary>
    public ImprovementType IpType { get; set; } = ImprovementType.Improvement;

    /// <summary>
    /// 이 요청에 달린 덧글 목록
    /// </summary>
    public ICollection<ImprovementComment> Comments { get; set; } = new List<ImprovementComment>();
    //public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
  }
}