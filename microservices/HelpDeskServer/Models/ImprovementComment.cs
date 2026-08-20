

using System;
using HelpDeskServer.Services;
using System.Collections.Generic;

namespace HelpDeskServer.Models {

  /// <summary>개선 요청 덧글</summary>
  public class ImprovementComment : BaseEntity {
    /// <summary>개선 요청 ID</summary>
    public int RequestId { get; set; }

    /// <summary>
    /// 이 덧글이 속한 개선 요청 (Navigation property)
    /// </summary>
    public ImprovementRequest? Request { get; set; }

    /// <summary>덧글 내용</summary>
    public string CommentText { get; set; } = string.Empty;

    /// <summary>작성자 구분</summary>
    public string AuthorType { get; set; } = "Customer"; // "Customer" or "Admin"

    /// <summary>작성자 ID</summary>
    public int AuthorId { get; set; }

    /// <summary>부모 덧글 ID</summary>
    public int? ParentCommentId { get; set; }

    /// <summary>삭제 여부</summary>
    public bool IsDel { get; set; }

    public DateTime? DeletedAt { get; set; }

    /// <summary>부모 덧글</summary>
    public ImprovementComment? ParentComment { get; set; }

    /// <summary>자식 덧글들</summary>
    public ICollection<ImprovementComment> Children { get; set; } = new List<ImprovementComment>();

    //public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
  }



}