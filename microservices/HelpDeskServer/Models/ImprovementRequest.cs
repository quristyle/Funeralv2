using System;
using HelpDeskServer.Services;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

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

    /// <summary>요청을 생성한 고객 (Navigation property)</summary>
    public Customer? Customer { get; set; }

    /// <summary>
    /// <b>고객사 이름</b> — 글을 쓴 사람이 그때 속해 있던 회사. <b>DB 에는 없다.</b>
    /// 회사의 정본은 포털(scom.companies)이고, 헬프데스크가 들고 있는 것은 고객 줄에 박힌 포털 회사 아이디뿐이다( — 글을 쓸 때 포털 토큰의 회사가 그대로 들어간다).
    /// </summary>
    [NotMapped]
    public string? CompanyName { get; set; }

    /// <summary>담당 관리자 ID</summary>
    public int? AdminId { get; set; }

    /// <summary>요청에 배정된 관리자 (Navigation property)</summary>
    public Admin? Admin { get; set; }

    /// <summary>
    /// <b>접수일시</b> — 담당자가 이 글을 맡은 때.
    /// createdat(요청이 들어온 때)을 「접수」로 쓰면 안 된다 — 이 칸이 따로 있다.
    /// </summary>
    public DateTime? AcceptedAt { get; set; }

    /// <summary>처리완료일시</summary>
    public DateTime? CompletededAt { get; set; }


    /// <summary>사용자완료(종료)일시</summary>
    public DateTime? UserCompletededAt { get; set; }





    /// <summary>상태</summary>
    public ImprovementStatus Status { get; set; } = ImprovementStatus.Pending;

    /// <summary>개선 유형</summary>
    public ImprovementType IpType { get; set; } = ImprovementType.Improvement;

    /// <summary>이 요청에 달린 덧글 목록</summary>
    public ICollection<ImprovementComment> Comments { get; set; } = new List<ImprovementComment>();
  }
}