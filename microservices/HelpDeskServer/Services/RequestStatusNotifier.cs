using System.Net;

using HelpDeskServer.Models;
using JSini.Shared.DTOs;

namespace HelpDeskServer.Services;

/// <summary>
/// 요청글의 <b>처리 상태가 바뀐 것</b>을 당사자에게 알린다 — 접수 · 완료 · 종료.
/// </summary>
/// <remarks>
/// <para>
/// [무엇이 잘못돼 있었나 (2026-10-12)]
/// </para>
/// <para>
/// 「완료」를 눌러도 글을 쓴 사람의 휴대폰이 울리지 않았다. 코드에는 보내는
/// 줄이 분명히 있었다 — <c>PushUtil.SendPushMsg(…, customerSubscriptions, …)</c>.
/// 그런데 그 구독 목록을 담은 표(<c>helpdesk.pushsubscriptions</c>)는
/// <b>옛 Vue 포털이 채우던 것</b>이고 그 화면은 2026-09-05 에 걷어냈다. 지금
/// 포털(<c>:5557</c>)에서 알림을 켜면 구독은 알림 서비스의
/// <c>scom.push_subscriptions</c> 에 들어간다.
/// </para>
/// <para>
/// 그래서 완료 알림은 <b>받을 기기가 0 대인 채로 조용히 성공</b>했다. 운영
/// 확인(2026-10-12): <c>helpdesk.pushsubscriptions</c> 0 줄 ·
/// <c>scom.push_subscriptions</c> 에 같은 사람의 기기 4 대.
/// 메일도 같은 꼴로 죽어 있었다 — 받는 사람을 고르는 조건이
/// <c>userproperty</c> 의 <c>receiveEmail = true</c> 인데 그 표가 0 줄이라
/// 언제나 빈 목록이었다(<c>AdminService.GetCustomerEmailsForNotificationAsync</c>).
/// 댓글 알림이 먼저 같은 까닭으로 못 닿았고(<see cref="CommentNotifier"/>),
/// 이 서비스가 처리 상태 알림에 같은 고침을 한다.
/// </para>
///
/// <para>
/// [끄는 것은 받는 사람과 회사가 정한다 — 여기서 묻지 않는다]
/// </para>
/// <para>
/// 알림구분·이벤트 코드(<see cref="NotificationEvents.HelpDeskStatus"/>)만 실어
/// 보내고, 「이 사람이 받을 사람인가」는 알림 서비스가 푼다 —
/// <b>정책(포털관리 › 알림관리) → 본인 설정(환경설정) → 구독</b> 순서다
/// (<c>docs/notify-policy.md</c>). 부르는 쪽마다 그 판정을 기억하게 하면 한
/// 곳만 잊어도 새는 설정이 된다.
/// </para>
///
/// <para>
/// [받는 사람은 상태마다 다르다]
/// </para>
/// <list type="table">
///   <item><term>접수 · 완료</term><description>글을 쓴 <b>작성자</b>. 기다리는 사람이 그 사람이다.</description></item>
///   <item><term>종료</term><description>그 글의 <b>접수자</b>. 사용자가 스스로 닫은 것을 담당자가 알아야 한다.</description></item>
/// </list>
/// <para>
/// <b>누른 사람이 곧 받을 사람이어도 보낸다 (2026-10-12 고침).</b>
/// </para>
/// <para>
/// 처음에는 「제가 누른 것이 제 휴대폰을 울리면 시끄럽다」는 까닭으로
/// <c>targetLoginId == actorLoginId</c> 면 조용히 돌아섰다. 그런데 이 회사에서
/// <b>한 사람이 두 모자를 쓴다</b> — 글을 올린 사람이 곧 시스템 관리자라
/// 접수도 완료도 제가 누른다. 그래서 그 걸러냄이 <b>실제로 가장 흔한 길을
/// 통째로 막았다</b>: 요청 19 의 「완료」가 그렇게 한 통도 안 나갔고,
/// <c>scom.push_send_logs</c> 에는 <b>실패 줄조차 안 남아</b> 「보낼 곳이
/// 없었다」와 「보내지 않기로 했다」를 가를 수가 없었다.
/// </para>
/// <para>
/// 게다가 <b>요청 등록은 이미 제가 올린 글을 제게 알리고 있었다</b>(역할 기반
/// <c>HELPDESK</c>). 등록은 울리는데 완료는 안 울리는 것은 받는 사람에게
/// 규칙이 아니라 고장으로 보인다.
/// </para>
/// <para>
/// <b>끄는 자리는 따로 있다.</b> 이 알림이 시끄러운 사람은 환경설정에서
/// <c>HELPDESK_STATUS</c> 를 끈다 — 위에 적은 대로, 「누가 받나」는 부르는 쪽이
/// 아니라 <b>정책 → 본인 설정 → 구독</b>이 정한다. 부르는 쪽에 걸러냄을 하나
/// 더 두면 그것은 <b>아무도 끌 수 없고 기록에도 안 남는 설정</b>이 된다.
/// </para>
/// </remarks>
public interface IRequestStatusNotifier {
  /// <summary>
  /// 상태가 바뀐 요청글 하나를 알린다. 알릴 상태가 아니면 아무것도 하지 않는다.
  /// </summary>
  /// <param name="request">상태를 저장한 뒤의 요청글.</param>
  /// <param name="status">방금 반영한 상태.</param>
  /// <param name="assigneeName">접수자 표시 이름. 제목 줄에 든다.</param>
  /// <param name="actorLoginId">
  /// 지금 단추를 누른 포털 계정. <b>받는 사람을 고르는 데는 쓰지 않는다</b> —
  /// 알림에 얹을 얼굴(<c>iconOwnerKey</c>)을 정할 뿐이다.
  /// </param>
  /// <param name="ct">끊김표.</param>
  Task NotifyAsync(
      ImprovementRequest request, ImprovementStatus status,
      string assigneeName, string? actorLoginId, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class RequestStatusNotifier : IRequestStatusNotifier {
  /// <summary>알림 서비스에 적어 보내는 부른 이 이름.</summary>
  private const string CallerId = "HELPDESK_STATUS";

  /// <summary>알림을 누르고 들어오는 포털 주소의 앞머리.</summary>
  private const string PortalOrigin = "https://portal.jsini.co.kr";

  private readonly IRequestOwnerResolver _owners;
  private readonly INotifyGateway _gateway;
  private readonly ILogger<RequestStatusNotifier> _logger;

  /// <summary>서비스를 생성한다.</summary>
  public RequestStatusNotifier(
      IRequestOwnerResolver owners,
      INotifyGateway gateway,
      ILogger<RequestStatusNotifier> logger) {
    _owners = owners;
    _gateway = gateway;
    _logger = logger;
  }

  /// <inheritdoc />
  public async Task NotifyAsync(
      ImprovementRequest request, ImprovementStatus status,
      string assigneeName, string? actorLoginId, CancellationToken ct = default) {

    var label = Label(status);
    if (label is null) return;

    // 종료만 접수자에게 간다 — 사용자가 스스로 닫은 것을 담당자가 알아야 한다.
    var toAssignee = status == ImprovementStatus.UserCompleted;

    var targetLoginId = toAssignee
        ? await _owners.AssigneeLoginIdAsync(request, ct)
        : await _owners.OwnerLoginIdAsync(request, ct);

    if (string.IsNullOrWhiteSpace(targetLoginId)) {
      // **오류가 아니다.** 계정 연결이 없는 옛 글과 접수자 없는 글이 있다 —
      // 그때는 보낼 곳이 없을 뿐이다. 다만 조용히 0 명이 되는 자리는
      // 알아챌 수 있어야 한다.
      _logger.LogInformation(
          "요청 {RequestId} 의 {Label} 알림 대상을 포털 계정으로 풀지 못했습니다 "
          + "(customerId={CustomerId}, adminId={AdminId}, createdBy={CreatedBy}).",
          request.Id, label, request.CustomerId, request.AdminId, request.CreatedBy);
      return;
    }

    // **누른 사람이 곧 받을 사람이어도 보낸다.** 여기에는
    // `targetLoginId == actorLoginId` 면 돌아서는 줄이 있었는데, 글쓴이가 곧
    // 시스템 관리자인 이 회사에서는 그것이 **가장 흔한 길**이라 완료 알림이
    // 통째로 사라졌다(요청 19). 시끄러운 사람이 끄는 자리는 환경설정이다 —
    // 머리말의 [누른 사람이 곧 받을 사람이어도 보낸다] 참고.

    // **앱 알림과 메일이 같은 주소를 쓴다.** 두 벌로 두면 한쪽만 고쳐져
    // 어긋나고, 어긋나는 쪽은 언제나 「알림을 눌렀더니 엉뚱한 화면」이다.
    var url = $"/helpdesk/request/detail/{request.Id}";
    var title = request.Title ?? string.Empty;
    var tag = $"요청 {request.Id}";

    var push = new {
      owners = new[] { new { ownerType = "jsini", ownerKey = targetLoginId } },

      message = new {
        // 「완료 - 홍길동」. 옛 길이 쓰던 제목 그대로다 — 받는 사람이 이미
        // 아는 모양을 바꿀 까닭이 없다.
        title = $"{label} - {assigneeName}",
        body = title,
        url,

        // 얼굴은 **단추를 누른 사람**의 것이다. 알림이 답할 물음은
        // 「누가 내 글을 처리했나」 하나다.
        iconOwnerKey = actorLoginId,

        // 같은 글의 상태 알림은 알림창에서 겹쳐 그린다 — 접수 뒤에 바로
        // 완료가 오면 두 장이 쌓일 까닭이 없다.
        tag = $"helpdesk-request-{request.Id}",

        category = PushCategories.HelpDeskStatus,
        eventCode = NotificationEvents.HelpDeskStatus
      }
    };

    await _gateway.PostAsync(
        "/notifications/push", push, CallerId, $"{label} 앱푸시", tag, ct);

    // 종료 알림의 메일은 헬프데스크가 접수자 주소로 따로 보낸다
    // (`RequestEndpoints`). 여기서 또 내면 담당자에게 두 통 간다.
    if (toAssignee) return;

    var mail = new {
      toUser = targetLoginId,
      subject = $"[{label}] {title}",
      body = EmailBody(label, request, url),

      // 저쪽 DTO 의 속성 이름은 `Html` 이다. `isHtml` 로 적으면 붙지 않고
      // 조용히 기본값이 쓰여 본문이 태그 그대로 보인다.
      html = true,

      category = PushCategories.HelpDeskStatus,
      eventCode = NotificationEvents.HelpDeskStatus
    };

    await _gateway.PostAsync(
        "/emails/send", mail, CallerId, $"{label} 이메일", tag, ct);
  }

  /// <summary>
  /// 알릴 상태면 사람이 읽는 이름, 아니면 <c>null</c>.
  /// </summary>
  /// <remarks>
  /// 반려·협의·논의·삭제는 알리지 않는다 — 옛 길에서도 보내지 않았고, 여기서
  /// 늘리는 것은 이 고침의 몫이 아니다. 늘릴 날이 오면 이 표에 한 줄을 더한다.
  /// </remarks>
  private static string? Label(ImprovementStatus status) => status switch {
    ImprovementStatus.InProgress => "접수",
    ImprovementStatus.Completed => "완료",
    ImprovementStatus.UserCompleted => "종료",
    _ => null
  };

  /// <summary>
  /// 메일 본문. 헬프데스크가 보내던 틀과 같은 모양이다.
  /// </summary>
  /// <remarks>
  /// <b>본문(<c>Description</c>)은 싣지 않는다.</b> 그것은 받는 사람이 자기가
  /// 쓴 글이라 이미 알고, 서식 편집기로 쓴 HTML 이라 그대로 실으면 메일이
  /// 글쓴이의 그림까지 통째로 끌고 간다. 알아야 할 것은 <b>무엇이 어떻게
  /// 됐는지</b>와 <b>어디로 가면 보이는지</b>다.
  /// </remarks>
  private static string EmailBody(string label, ImprovementRequest request, string url) {
    var title = WebUtility.HtmlEncode(request.Title ?? string.Empty);
    var portalUrl = $"{PortalOrigin}{url}";

    return $"""
      <div style="max-width:600px;margin:0 auto;font-family:'Malgun Gothic',sans-serif;border:1px solid #e0e0e0;border-radius:8px;overflow:hidden;">
        <div style="background-color:#f8f9fa;padding:20px;border-bottom:1px solid #e0e0e0;">
          <h2 style="margin:0;color:#333;font-size:18px;">[{label}] {title}</h2>
        </div>
        <div style="padding:24px;background-color:#ffffff;">
          <p style="font-size:14px;color:#555;line-height:1.6;margin-top:0;">
            요청하신 내용이 <strong>{label}</strong> 되었습니다.
          </p>
          <div style="text-align:center;margin-top:30px;">
            <a href="{portalUrl}" target="_blank" style="display:inline-block;background-color:#007bff;color:#ffffff;text-decoration:none;padding:12px 24px;border-radius:4px;font-weight:bold;font-size:14px;">요청 글 보기</a>
          </div>
        </div>
        <div style="background-color:#f8f9fa;padding:15px 20px;text-align:center;border-top:1px solid #e0e0e0;">
          <p style="margin:0;font-size:12px;color:#888;">본 메일은 발신 전용입니다. 감사합니다.</p>
        </div>
      </div>
      """;
  }
}
