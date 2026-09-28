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

    /// <summary>
    /// <b>접수일시</b> — 담당자가 이 글을 맡은 때.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 전에는 이 값이 없어서 <c>createdat</c>(요청이 들어온 때)을 화면에
    /// 「접수」라고 적어 놓고 있었다. 둘은 다른 시각이다 — 글이 들어온 때와
    /// 누군가 그것을 맡은 때 사이가 이 조직이 재는 <b>응답 시간</b>이고,
    /// 칸이 없으면 그것을 영영 못 센다.
    /// </para>
    /// <para>
    /// <see cref="AdminId"/>(접수자)와 <b>짝</b>이다. 한쪽만 채워지면
    /// 「누가 맡았는지는 아는데 언제 맡았는지는 모르는」 줄이 남으므로
    /// 접수 처리는 둘을 한 번에 넣는다.
    /// </para>
    /// <para>
    /// <b>한 번 박히면 다시 움직이지 않는다.</b> 완료로 넘어갈 때 다시
    /// 찍으면 응답 시간이 0 이 되어 버린다.
    /// </para>
    /// </remarks>
    public DateTime? AcceptedAt { get; set; }

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