using System.Net;

using HelpDeskServer.Models;
using JSini.Shared.DTOs;

namespace HelpDeskServer.Services;

/// <summary>
/// 새 요청글을 그 회사를 맡은 헬프데스크 관리자(포털관리 「사용처 담당자」 HELP_DESK + <c>HELPDESK_MANAGER</c> 역할)에게 앱 푸시와 메일로 알린다.
/// 등록 알림(<c>HELPDESK</c>)은 역할 단위라 맡은 회사를 모른다 — 그 길로는 담당자가 제 회사의 새 글을 알 수 없었다.
/// </summary>
public interface IManagedRequestNotifier {
  /// <summary>방금 저장한 요청글 하나를 알린다. 글쓴이 본인은 뺀다. 실패해도 던지지 않는다.</summary>
  Task NotifyAsync(ImprovementRequest request, string requesterName, string? authorLoginId, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class ManagedRequestNotifier : IManagedRequestNotifier {
  private const string CallerId = "HELPDESK_MANAGED";
  private const string PortalOrigin = "https://portal.jsini.co.kr";

  private readonly IPortalCompanyDirectory _companies;
  private readonly INotifyGateway _gateway;
  private readonly ILogger<ManagedRequestNotifier> _logger;

  /// <summary>서비스를 생성한다.</summary>
  public ManagedRequestNotifier(
      IPortalCompanyDirectory companies,
      INotifyGateway gateway,
      ILogger<ManagedRequestNotifier> logger) {
    _companies = companies;
    _gateway = gateway;
    _logger = logger;
  }

  /// <inheritdoc />
  public async Task NotifyAsync(
      ImprovementRequest request, string requesterName, string? authorLoginId, CancellationToken ct = default) {

    if (string.IsNullOrWhiteSpace(request.CompanyId)) return;

    var managers = (await _companies.GetManagerLoginIdsAsync(request.CompanyId, RequestCompanyPolicy.ManagerRole, ct))
        .Where(id => !string.Equals(id, authorLoginId, StringComparison.OrdinalIgnoreCase))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    if (managers.Count == 0) {
      // 담당자를 안 걸어 둔 회사면 정상이다. 다만 「왜 안 왔나」를 물을 때 여기서 갈린다.
      _logger.LogInformation(
          "요청 {RequestId} 의 회사 {CompanyId} 에 알릴 헬프데스크 담당자가 없습니다.",
          request.Id, request.CompanyId);
      return;
    }

    var companyName = await _companies.GetNameAsync(request.CompanyId, ct) ?? request.CompanyId;
    var url = $"/helpdesk/request/detail/{request.Id}";
    var title = request.Title ?? string.Empty;
    var tag = $"요청 {request.Id}";

    var push = new {
      owners = managers.Select(id => new { ownerType = "jsini", ownerKey = id }).ToArray(),
      message = new {
        title = $"새 헬프데스크 요청 - {companyName}",
        body = $"{requesterName} 님이 요청을 등록했습니다: {title}",
        url,
        iconOwnerKey = authorLoginId,

        // 등록 알림(역할)과 같은 꼬리표다 — 둘 다 받는 사람의 알림창에서는 한 장으로 겹친다.
        tag = $"helpdesk-request-{request.Id}",

        category = PushCategories.HelpDesk,
        eventCode = NotificationEvents.HelpDeskManaged
      }
    };

    await _gateway.PostAsync("/notifications/push", push, CallerId, "담당 회사 새 요청 앱푸시", tag, ct);

    var mail = new {
      toUser = string.Join(",", managers),
      subject = $"[등록] [{companyName}] {title}",
      body = EmailBody(request, requesterName, companyName, url),

      // 저쪽 DTO 속성은 Html 이다.
      html = true,

      category = PushCategories.HelpDesk,
      eventCode = NotificationEvents.HelpDeskManaged
    };

    await _gateway.PostAsync("/emails/send", mail, CallerId, "담당 회사 새 요청 이메일", tag, ct);
  }

  private static string EmailBody(ImprovementRequest request, string requesterName, string companyName, string url) {
    var title = WebUtility.HtmlEncode(request.Title ?? string.Empty);
    var who = WebUtility.HtmlEncode(requesterName);
    var company = WebUtility.HtmlEncode(companyName);
    var portalUrl = $"{PortalOrigin}{url}";

    return $"""
      <div style="max-width:600px;margin:0 auto;font-family:'Malgun Gothic',sans-serif;border:1px solid #e0e0e0;border-radius:8px;overflow:hidden;">
        <div style="background-color:#f8f9fa;padding:20px;border-bottom:1px solid #e0e0e0;">
          <h2 style="margin:0;color:#333;font-size:18px;">[등록] {title}</h2>
        </div>
        <div style="padding:24px;background-color:#ffffff;">
          <p style="font-size:14px;color:#555;line-height:1.6;margin-top:0;">
            맡고 계신 <strong>{company}</strong> 에 {who} 님이 새 요청을 <strong>등록</strong>했습니다.
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
