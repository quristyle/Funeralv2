using System.Net;

using HelpDeskServer.Data;
using HelpDeskServer.Models;
using HtmlAgilityPack;
using JSini.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace HelpDeskServer.Services;

/// <summary>
/// <b>내가 쓴 요청글에 댓글이 달렸거나, 내가 쓴 댓글에 답글이 달렸다</b>를 당사자에게 알린다 — 앱 푸시와 이메일.
/// </summary>
/// <remarks>
/// <para>
/// [왜 헬프데스크 제 구독이 아니라 알림 서비스로 보내는가]
/// </para>
/// <para>
/// 댓글 알림은 오래도록 헬프데스크가 제 표(<c>pushsubscription</c>)로 보냈다
/// (<c>PushUtil.SendPushMsg</c>). 그 표는 <b>옛 Vue 포털이 구독을 만들어 채우던
/// 것</b>인데 그 화면은 이제 없다 — 지금 포털(<c>:5557</c>)에서 알림을 켜면
/// 구독은 <b>알림 서비스</b>의 <c>scom.push_subscriptions</c> 에 들어간다.
/// 그래서 옛 길로 보낸 댓글 알림은 <b>받을 기기가 한 대도 없는 채로</b> 조용히
/// 성공했다. 새 길로 보내야 실제로 도착한다.
/// </para>
/// <para>
/// 이메일은 처음부터 없었다. 요청 <b>등록</b>과 <b>완료</b>에는 메일이 나가는데
/// (<c>EMailUtil</c>) 댓글에는 없어서, 글을 올린 사람은 답이 달린 것을 <b>그
/// 요청글을 다시 열어 봐야</b> 알았다.
/// </para>
///
/// <para>
/// [댓글에 답글이 달렸을 때의 알림]
/// </para>
/// <para>
/// 댓글에 답글이 달리면(<c>ParentCommentId</c>), 그 부모 댓글의 작성자에게 답글 알림을 보낸다.
/// 요청글 작성자와 부모 댓글 작성자가 같은 사람일 때는 답글 알림 한 통만 보내어 중복 수신을 막는다.
/// </para>
///
/// <para>
/// [끄는 것은 받는 사람이 정한다 — 여기서 묻지 않는다]
/// </para>
/// <para>
/// 「댓글 알림을 받을지」 스위치는 알림 서비스의 설정 표에 있고, 그 판정도
/// 거기서 한다(<c>PushSender</c> · <c>EmailEndpoints</c>). 여기서는 알림구분
/// (<see cref="PushCategories.HelpDeskComment"/>)만 실어 보낸다 — 부르는 쪽마다
/// 「이 사람이 껐나」를 기억하게 하면 한 곳만 잊어도 새는 설정이 된다.
/// </para>
///
/// <para>
/// [자기 글에 자기가 단 답글도 보낸다 (2026-10-12 고침)]
/// </para>
/// <para>
/// 처음에는 <c>ownerLoginId == actorLoginId</c> 면 조용히 돌아섰다. 「내가 방금
/// 친 글이 내 휴대폰을 울리면 시끄럽다」는 까닭이었고, 코드만 읽으면 그럴듯해
/// 보인다. 틀린 것은 로직이 아니라 <b>「글쓴이와 답하는 사람은 다른 사람」이라는
/// 전제</b>였다 — 이 회사에서는 <b>한 사람이 두 모자를 쓴다</b>. 요청글을 올린
/// 사람이 곧 시스템 관리자라 답글도 제가 단다.
/// </para>
/// <para>
/// 그래서 그 걸러냄이 <b>가장 흔한 길이 아니라 유일한 길을 막았다</b>:
/// 운영 확인(2026-10-12) — 요청글 8건에 달린 댓글 5건이 <b>전부</b>
/// <c>quristyle</c> 이 제 글에 단 것이고, <c>scom.push_send_logs</c> 의
/// <c>HELPDESK_COMMENT</c> 는 <b>0 줄</b>이었다. 돌아서는 자리가
/// <c>_gateway.PostAsync</c> <b>앞</b>이라 실패 줄조차 안 남아, 「보낼 곳이
/// 없었다」와 「보내지 않기로 했다」를 기록에서 가를 수도 없었다.
/// </para>
/// <para>
/// <b>끄는 자리는 따로 있다.</b> 시끄러운 사람은 환경설정의 「댓글 앱알림」·
/// 「댓글 이메일」을 끄고(<c>comment_push_enabled</c> ·
/// <c>comment_email_enabled</c>), 회사 차원에서는 알림관리에서
/// <c>HELPDESK_COMMENT</c> 이벤트를 끈다. 부르는 쪽에 걸러냄을 두면 그것은
/// <b>알림관리에도 환경설정에도 안 보이고 기록도 안 남는</b> — 아무도 끌 수 없고
/// 아무도 켤 수 없는 설정이 된다(<c>docs/notify-policy.md</c>).
/// 처리 상태 알림이 같은 날 같은 고침을 받았다
/// (<see cref="RequestStatusNotifier"/>).
/// </para>
///
/// <para>
/// [못 보내도 댓글은 남는다]
/// </para>
/// <para>
/// 알림 서비스가 죽어 있어도 <b>예외를 위로 던지지 않는다.</b> 댓글은 이미
/// 저장됐는데 알림 때문에 화면에 「남기지 못했습니다」가 뜨면, 사람은 같은 말을
/// 한 번 더 쓴다. 사유는 로그로만 남긴다.
/// </para>
/// </remarks>
public interface ICommentNotifier {
  /// <summary>
  /// 댓글 하나를 알린다. 당사자를 포털 계정으로 풀지 못하면 아무것도 하지 않는다.
  /// </summary>
  /// <param name="comment">방금 저장한 댓글. 아이디가 채워져 있어야 한다(주소에 싣는다).</param>
  /// <param name="request">그 댓글이 달린 요청글.</param>
  /// <param name="authorName">댓글을 쓴 사람의 표시 이름. 제목 줄에 든다.</param>
  /// <param name="actorLoginId">
  /// 지금 부른 포털 계정. <b>받는 사람을 고르는 데는 쓰지 않는다</b> — 알림에
  /// 얹을 얼굴(<c>iconOwnerKey</c>)을 정할 뿐이다.
  /// </param>
  /// <param name="ct">끊김표.</param>
  Task NotifyAsync(
      ImprovementComment comment, ImprovementRequest request,
      string authorName, string? actorLoginId, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class CommentNotifier : ICommentNotifier {
  /// <summary>알림 서비스에 적어 보내는 부른 이 이름(<c>X-User-Id</c>).</summary>
  private const string CallerId = "HELPDESK_COMMENT";

  /// <summary>
  /// 알림·메일이 본문에 실을 글자 수 상한. 알림창은 어차피 더 못 보여 주고,
  /// 메일은 본문 전체를 따로 싣는다.
  /// </summary>
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

    // **앱 알림과 메일이 같은 한 줄을 쓴다.** 두 벌로 두면 한쪽만 고쳐져
    // 어긋나고, 어긋나는 쪽은 언제나 「알림을 눌렀더니 엉뚱한 화면」이다.
    // 뒤의 조각(`#comment-…`)은 상세 화면이 댓글마다 달아 둔 자리 이름이다.
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

        // 얼굴은 **댓글을 쓴 사람**의 것이다 — 알림 아이콘이 답해야 하는 물음은
        // 「누가 말을 걸었나」 하나다.
        iconOwnerKey = actorLoginId,

        // 같은 요청글의 댓글은 알림창에서 겹쳐 그린다. 답글이 연달아 달릴 때
        // 알림이 쌓여 화면을 덮는 것을 막는다.
        tag = $"helpdesk-comment-{request.Id}",

        category = PushCategories.HelpDeskComment,

        // 알림관리(포털관리 › 시스템관리)가 역할·길을 매다는 열쇠다. 지금은
        // 알림구분과 같은 글자라 안 적어도 같은 줄을 찾지만, 적어 두면 구분을
        // 더 잘게 가르는 날에 이 자리가 흔들리지 않는다.
        eventCode = NotificationEvents.HelpDeskComment
      }
    };

    await _gateway.PostAsync(
        "/notifications/push", payload, CallerId, $"{label} 앱푸시", $"요청 {request.Id} -> {targetLoginId}", ct);
  }

  /// <summary>이메일 한 통. 본문에 댓글 원문과 그 글로 가는 고리를 싣는다.</summary>
  /// <remarks>
  /// <b>큐가 아니라 직발송(<c>/emails/send</c>)을 쓴다.</b> 큐 쪽은 받는 사람을
  /// 주소로만 받는데, 여기서 아는 것은 <b>포털 로그인 아이디</b>다 — 주소를 푸는
  /// 표(<c>scom</c>)는 헬프데스크 DB 에 없다. 직발송은 <c>toUser</c> 로 아이디를
  /// 받아 저쪽에서 풀어 준다.
  /// </remarks>
  private async Task SendEmailAsync(
      ImprovementRequest request, string targetLoginId, string authorName,
      ImprovementComment comment, string url, bool isReply, CancellationToken ct) {

    var label = isReply ? "답글" : "댓글";

    var payload = new {
      toUser = targetLoginId,
      subject = $"[헬프데스크] {request.Title} — {authorName} 님의 {label}",
      body = EmailBody(request, authorName, comment, url, isReply),

      // 저쪽 DTO 의 속성 이름은 `Html` 이다. `isHtml` 로 적으면 붙지 않고
      // 조용히 기본값이 쓰여 본문이 태그 그대로 보인다.
      html = true,

      // 이 갈래를 끈 사람은 저쪽이 뺀다.
      category = PushCategories.HelpDeskComment,
      eventCode = NotificationEvents.HelpDeskComment
    };

    await _gateway.PostAsync(
        "/emails/send", payload, CallerId, $"{label} 이메일", $"요청 {request.Id} -> {targetLoginId}", ct);
  }

  /// <summary>
  /// 메일 본문. <b>댓글 원문을 그대로 싣는다</b> — 미리보기만 보내면 받는 사람이
  /// 메일을 읽고도 포털을 열어야 한다. 포털 확인 링크는 단추 형태로 제공한다.
  /// </summary>
  /// <remarks>
  /// 댓글은 서식 편집기로 쓴 HTML 이라 <c>html = true</c> 로 보낸다. 그림은
  /// 등록할 때 이미 파일로 옮겨져 있어(<c>FileUtil.SaveImageToFile</c>) 본문에는
  /// 주소만 남는다 — 메일 크기가 부풀지 않는다.
  /// </remarks>
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

  /// <summary>
  /// 알림창에 실을 한 줄. 태그를 걷고 앞부분만 남긴다.
  /// </summary>
  /// <remarks>
  /// <c>PushUtil.SendPushMsg</c> 가 하던 일인데, 그 길은 헬프데스크 제 구독으로
  /// 보내는 옛 길이라 여기서 다시 한다. 태그를 안 걷으면 알림창에
  /// <c>&lt;p&gt;</c> 가 그대로 뜬다.
  /// </remarks>
  private static string Preview(string? html) {
    if (string.IsNullOrWhiteSpace(html)) return "(내용 없음)";

    var doc = new HtmlDocument();
    doc.LoadHtml(html);

    // 줄바꿈과 겹친 공백을 한 칸으로 줄인다 — 알림창은 한두 줄만 보여 주는데
    // 앞이 빈 줄이면 그 자리가 통째로 날아간다.
    var text = WebUtility.HtmlDecode(doc.DocumentNode.InnerText ?? string.Empty);
    text = string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    if (text.Length == 0) return "(내용 없음)";

    return text.Length > PreviewLength
        ? text[..PreviewLength] + "…"
        : text;
  }
}
