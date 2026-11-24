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
    /// <b>고객사</b> — 글을 쓴 사람이 <b>쓸 때</b> 속해 있던 포털 회사 아이디(scom.companies.id). 회사 걸름은 이 칸을 본다.
    /// 고객 줄의 회사는 조직도를 따라 바뀌므로 그것을 빌려 쓰면 옮겨 간 사람의 옛 글이 새 회사로 함께 넘어간다.
    /// </summary>
    public string? CompanyId { get; set; }

    /// <summary>
    /// <b>고객사 이름</b> — <see cref="CompanyId"/> 를 포털에서 푼 것. <b>DB 에는 없다.</b>
    /// </summary>
    [NotMapped]
    public string? CompanyName { get; set; }

    /// <summary>이 요청에 붙은 첨부. <b>DB 칸이 아니다</b> — 첨부는 공통 표(attachments)에 EntityType 「ImprovementRequest」로 걸려 있어 상세 조회가 따로 채운다.</summary>
    [NotMapped]
    public List<Attachment>? Attachments { get; set; }

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