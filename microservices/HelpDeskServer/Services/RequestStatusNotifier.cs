using System.Net;

using HelpDeskServer.Models;
using JSini.Shared.DTOs;

namespace HelpDeskServer.Services;

/// <summary>요청글의 처리 상태(접수·완료·종료)가 바뀌면 당사자에게 앱 푸시와 메일로 알린다. 받는 사람은 정책 → 본인 설정 → 구독 순서로 알림 서비스가 정하고 여기서 묻지 않는다.
/// 누른 사람이 곧 받을 사람이어도 보낸다 — 글쓴이가 곧 담당자인 회사라 걸러내면 가장 흔한 알림이 사라진다. 푸시는 알림 서비스로 보낸다(헬프데스크 자체 구독표는 쓰지 않는다).</summary>
public interface IRequestStatusNotifier {
  /// <summary>상태가 바뀐 요청글 하나를 알린다. 알릴 상태가 아니면 아무것도 하지 않는다. actorLoginId 는 받는 사람이 아니라 알림 아이콘용이다.</summary>
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
      // 계정 연결이 없는 옛 글과 접수자 없는 글은 보낼 곳이 없을 뿐이다. 다만 0명이 되는 자리는 알아챌 수 있어야 한다.
      _logger.LogInformation(
          "요청 {RequestId} 의 {Label} 알림 대상을 포털 계정으로 풀지 못했습니다 "
          + "(customerId={CustomerId}, adminId={AdminId}, createdBy={CreatedBy}).",
          request.Id, label, request.CustomerId, request.AdminId, request.CreatedBy);
      return;
    }

    // 누른 사람이 곧 받을 사람이어도 보낸다(글쓴이가 관리자인 회사라 가장 흔한 길이다). 끄는 자리는 환경설정이다.

    // 앱 알림과 메일이 같은 주소를 쓴다 — 두 벌이면 한쪽만 고쳐져 엉뚱한 화면으로 간다.
    var url = $"/helpdesk/request/detail/{request.Id}";
    var title = request.Title ?? string.Empty;
    var tag = $"요청 {request.Id}";

    var push = new {
      owners = new[] { new { ownerType = "jsini", ownerKey = targetLoginId } },

      message = new {
        // 「완료 - 홍길동」 제목 그대로다. 받는 사람이 아는 모양을 바꿀 까닭이 없다.
        title = $"{label} - {assigneeName}",
        body = title,
        url,

        // 얼굴은 단추를 누른 사람 것이다. 알림이 답할 물음은 「누가 내 글을 처리했나」 하나다.
        iconOwnerKey = actorLoginId,

        // 같은 글의 상태 알림은 알림창에서 겹쳐 그린다.
        tag = $"helpdesk-request-{request.Id}",

        category = PushCategories.HelpDeskStatus,
        eventCode = NotificationEvents.HelpDeskStatus
      }
    };

    await _gateway.PostAsync(
        "/notifications/push", push, CallerId, $"{label} 앱푸시", tag, ct);

    // 종료 메일은 헬프데스크가 접수자 주소로 따로 보낸다(RequestEndpoints). 여기서 또 내면 두 통 간다.
    if (toAssignee) return;

    var mail = new {
      toUser = targetLoginId,
      subject = $"[{label}] {title}",
      body = EmailBody(label, request, url),

      // 저쪽 DTO 속성은 Html 이다. isHtml 로 적으면 조용히 기본값이 쓰여 태그가 그대로 보인다.
      html = true,

      category = PushCategories.HelpDeskStatus,
      eventCode = NotificationEvents.HelpDeskStatus
    };

    await _gateway.PostAsync(
        "/emails/send", mail, CallerId, $"{label} 이메일", tag, ct);
  }

  /// <summary>알릴 상태면 사람이 읽는 이름, 아니면 null. 반려·협의·논의·삭제는 보내지 않는다.</summary>
  private static string? Label(ImprovementStatus status) => status switch {
    ImprovementStatus.InProgress => "접수",
    ImprovementStatus.Completed => "완료",
    ImprovementStatus.UserCompleted => "종료",
    _ => null
  };

  /// <summary>메일 본문. 본문(Description)은 싣지 않는다 — 받는 사람이 쓴 글이고 서식 HTML 이 메일을 부풀린다. 무엇이 어떻게 됐는지와 보이는 곳만 싣는다.</summary>
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
