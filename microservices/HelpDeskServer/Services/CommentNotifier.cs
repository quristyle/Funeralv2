using System.Net;

using HelpDeskServer.Data;
using HelpDeskServer.Models;
using HtmlAgilityPack;
using JSini.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace HelpDeskServer.Services;

/// <summary>내 요청글의 댓글·내 댓글의 답글을 당사자에게 앱 푸시와 이메일로 알린다. 푸시는 알림 서비스로 보낸다(헬프데스크 자체 구독표는 쓰지 않는다).
/// 자기 글에 자기가 단 답글도 보낸다 — 한 사람이 글쓴이와 관리자를 겸하는 회사라서. 끄는 자리는 알림관리 정책과 본인 설정이다. 알림이 실패해도 댓글 저장은 성공으로 둔다.</summary>
public interface ICommentNotifier {
  /// <summary>댓글 하나를 알린다. 당사자를 포털 계정으로 풀지 못하면 아무것도 하지 않는다. actorLoginId 는 받는 사람이 아니라 알림 아이콘용이다.</summary>
  Task NotifyAsync(
      ImprovementComment comment, ImprovementRequest request,
      string authorName, string? actorLoginId, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class CommentNotifier : ICommentNotifier {
  /// <summary>알림 서비스에 적어 보내는 부른 이 이름(<c>X-User-Id</c>).</summary>
  private const string CallerId = "HELPDESK_COMMENT";

  /// <summary>알림·메일 본문에 싣는 글자 수 상한. 알림창은 어차피 더 못 보여 준다.</summary>
  private const int PreviewLength = 120;

  private readonly AppDbContext _db;
  private readonly IRequestOwnerResolver _owners;
  private readonly INotifyGateway _gateway;
  private readonly ILogger<CommentNotifier> _logger;

  /// <summary>서비스를 생성한다.</summary>
  public CommentNotifier(
      AppDbContext db,
      IRequestOwnerResolver owners,
      INotifyGateway gateway,
      ILogger<CommentNotifier> logger) {
    _db = db;
    _owners = owners;
    _gateway = gateway;
    _logger = logger;
  }

  /// <inheritdoc />
  public async Task NotifyAsync(
      ImprovementComment comment, ImprovementRequest request,
      string authorName, string? actorLoginId, CancellationToken ct = default) {

    var preview = Preview(comment.CommentText);

    // 앱 알림과 메일은 같은 링크를 쓴다 — 두 벌이면 한쪽만 고쳐져 엉뚱한 화면으로 간다. #comment-… 조각은 상세가 댓글마다 달아 둔 자리 이름이다.
    var url = $"/helpdesk/request/detail/{request.Id}#comment-{comment.Id}";

    // 1. 부모 댓글이 있는 경우(답글) 부모 댓글 작성자를 확인한다.
    ImprovementComment? parentComment = null;
    string? parentAuthorLoginId = null;

    if (comment.ParentCommentId is { } parentId && parentId > 0) {
      parentComment = await _db.Comments
          .AsNoTracking()
          .FirstOrDefaultAsync(c => c.Id == parentId, ct);

      if (parentComment != null) {
        parentAuthorLoginId = await _owners.CommentAuthorLoginIdAsync(parentComment, ct);
        if (string.IsNullOrWhiteSpace(parentAuthorLoginId)) {
          _logger.LogInformation(
              "부모 댓글 {ParentCommentId} 의 작성자를 포털 계정으로 풀지 못했습니다 "
              + "(authorId={AuthorId}, authorType={AuthorType}, createdBy={CreatedBy}).",
              parentComment.Id, parentComment.AuthorId, parentComment.AuthorType, parentComment.CreatedBy);
        }
      }
    }

    // 2. 요청글 작성자를 확인한다.
    var ownerLoginId = await _owners.OwnerLoginIdAsync(request, ct);
    if (string.IsNullOrWhiteSpace(ownerLoginId)) {
      _logger.LogInformation(
          "요청 {RequestId} 의 작성자를 포털 계정으로 풀지 못했습니다 "
          + "(customerId={CustomerId}, createdBy={CreatedBy}).",
          request.Id, request.CustomerId, request.CreatedBy);
    }

    if (string.IsNullOrWhiteSpace(parentAuthorLoginId) && string.IsNullOrWhiteSpace(ownerLoginId)) {
      return;
    }

    // 3. 부모 댓글 작성자에게 답글 알림 발송
    if (!string.IsNullOrWhiteSpace(parentAuthorLoginId)) {
      await SendPushAsync(request, parentAuthorLoginId, authorName, preview, url, actorLoginId, isReply: true, ct);
      await SendEmailAsync(request, parentAuthorLoginId, authorName, comment, url, isReply: true, ct);
    }

    // 4. 요청글 작성자에게 댓글 알림 발송 (부모 댓글 작성자와 동일한 사람이면 중복 발송 방지)
    if (!string.IsNullOrWhiteSpace(ownerLoginId)
        && !string.Equals(ownerLoginId, parentAuthorLoginId, StringComparison.OrdinalIgnoreCase)) {
      await SendPushAsync(request, ownerLoginId, authorName, preview, url, actorLoginId, isReply: false, ct);
      await SendEmailAsync(request, ownerLoginId, authorName, comment, url, isReply: false, ct);
    }
  }

  /// <summary>앱 푸시 한 통. 알림 서비스가 받는 사람의 기기를 찾아 보낸다.</summary>
  private async Task SendPushAsync(
      ImprovementRequest request, string targetLoginId, string authorName,
      string preview, string url, string? actorLoginId, bool isReply, CancellationToken ct) {

    var label = isReply ? "답글" : "댓글";

    var payload = new {
      owners = new[] { new { ownerType = "jsini", ownerKey = targetLoginId } },

      message = new {
        title = $"{label} - {authorName}",
        body = preview,
        url,

        // 얼굴은 댓글을 쓴 사람 것이다 — 알림 아이콘이 답해야 하는 것은 누가 말을 걸었나 하나다.
        iconOwnerKey = actorLoginId,

        // 같은 요청글의 댓글은 알림창에서 겹쳐 그린다(답글이 연달아 쌓여 화면을 덮지 않게).
        tag = $"helpdesk-comment-{request.Id}",

        category = PushCategories.HelpDeskComment,

        // 알림관리가 역할·길을 매다는 열쇠. 지금은 알림구분과 같은 글자라 같은 줄을 찾는다.
        eventCode = NotificationEvents.HelpDeskComment
      }
    };

    await _gateway.PostAsync(
        "/notifications/push", payload, CallerId, $"{label} 앱푸시", $"요청 {request.Id} -> {targetLoginId}", ct);
  }

  /// <summary>이메일 한 통. 본문에 댓글 원문과 그 글로 가는 고리를 싣는다. 큐가 아니라 직발송을 쓴다 — 여기서 아는 것은 포털 로그인 아이디뿐이라 toUser 로 저쪽에서 주소를 풀게 한다.</summary>
  private async Task SendEmailAsync(
      ImprovementRequest request, string targetLoginId, string authorName,
      ImprovementComment comment, string url, bool isReply, CancellationToken ct) {

    var label = isReply ? "답글" : "댓글";

    var payload = new {
      toUser = targetLoginId,
      subject = $"[헬프데스크] {request.Title} — {authorName} 님의 {label}",
      body = EmailBody(request, authorName, comment, url, isReply),

      // 저쪽 DTO 속성은 Html 이다. isHtml 로 적으면 조용히 기본값이 쓰여 태그가 그대로 보인다.
      html = true,

      // 이 갈래를 끈 사람은 저쪽이 뺀다.
      category = PushCategories.HelpDeskComment,
      eventCode = NotificationEvents.HelpDeskComment
    };

    await _gateway.PostAsync(
        "/emails/send", payload, CallerId, $"{label} 이메일", $"요청 {request.Id} -> {targetLoginId}", ct);
  }

  /// <summary>메일 본문에 댓글 원문을 그대로 싣는다(미리보기만 보내면 받는 사람이 포털을 열어야 한다). 댓글은 HTML 이라 html=true 로 보낸다.</summary>
  private static string EmailBody(
      ImprovementRequest request, string authorName, ImprovementComment comment, string url, bool isReply) {

    var portalUrl = $"https://portal.jsini.co.kr{url}";
    var title = WebUtility.HtmlEncode(request.Title ?? string.Empty);
    var who = WebUtility.HtmlEncode(authorName);

    var messageText = isReply
        ? $"<strong>{who}</strong> 님이 요청글 「{title}」 에 남기신 댓글에 답글을 남겼습니다."
        : $"<strong>{who}</strong> 님이 요청글 「{title}」 에 댓글을 남겼습니다.";

    return $"""
      <div style="font-family:'Apple SD Gothic Neo','Malgun Gothic',sans-serif;font-size:14px;color:#222;line-height:1.6;">
        <p style="margin:0 0 12px 0;">{messageText}</p>
        <blockquote style="margin:16px 0;padding:12px 16px;border-left:3px solid #007bff;background:#f8f9fa;">
          {comment.CommentText}
        </blockquote>
        <table role="presentation" cellpadding="0" cellspacing="0" border="0" style="margin-top:20px;">
          <tr>
            <td align="center" style="background-color:#007bff;border-radius:4px;">
              <a href="{portalUrl}" target="_blank"
                 style="display:inline-block;padding:10px 22px;font-family:'Apple SD Gothic Neo','Malgun Gothic',sans-serif;font-size:14px;font-weight:bold;color:#ffffff;text-decoration:none;border-radius:4px;">포털에서 확인</a>
            </td>
          </tr>
        </table>
      </div>
      """;
  }

  /// <summary>알림창에 실을 한 줄. 태그를 걷고 앞부분만 남긴다(안 걷으면 알림창에 태그가 그대로 뜬다).</summary>
  private static string Preview(string? html) {
    if (string.IsNullOrWhiteSpace(html)) return "(내용 없음)";

    var doc = new HtmlDocument();
    doc.LoadHtml(html);

    // 줄바꿈·겹친 공백을 한 칸으로 줄인다 — 앞이 빈 줄이면 알림창에서 그 자리가 통째로 날아간다.
    var text = WebUtility.HtmlDecode(doc.DocumentNode.InnerText ?? string.Empty);
    text = string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    if (text.Length == 0) return "(내용 없음)";

    return text.Length > PreviewLength
        ? text[..PreviewLength] + "…"
        : text;
  }
}
