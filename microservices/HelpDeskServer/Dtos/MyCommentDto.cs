namespace HelpDeskServer.Dtos;

/// <summary>
/// 내 댓글 DTO
/// </summary>
public class MyCommentDto {


  /// <summary>
  /// 댓글 아이디
  /// </summary>
  public int CommentId { get; set; }
  /// <summary>
  /// 댓글 내용
  /// </summary>
  public string? CommentText { get; set; }
  /// <summary>
  /// 작성일시
  /// </summary>
  public DateTime CreatedAt { get; set; }
  /// <summary>
  /// 요청 아이디
  /// </summary>
  public int RequestId { get; set; }
  /// <summary>
  /// 요청 제목
  /// </summary>
  public string? RequestTitle { get; set; }
  /// <summary>
  /// 요청 상태
  /// </summary>
  public string? RequestStatus { get; set; }


  public string AuthorName { get; set; }

  public string? AuthorPhoto { get; set; }

  /// <summary>
  /// 부모 댓글 아이디
  /// </summary>
  public int? ParentCommentId { get; set; }

  /// <summary>
  /// 대댓글 수
  /// </summary>
  public int ReplyCount { get; set; }

}
