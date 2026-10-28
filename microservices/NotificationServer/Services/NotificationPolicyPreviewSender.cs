using JSini.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using NotificationServer.Data;
using NotificationServer.DTOs;

namespace NotificationServer.Services;

/// <summary>알림관리의 미리받아보기. 이벤트가 실제로 일어나지 않아도 그 이벤트의 알림 한 통을 앱 푸시·이메일로 보낸다.
/// 본인 받아보기는 정책을 안 거치고, 대상자 받아보기는 <b>저장된</b> 정책으로 푼 사람에게만 간다(PreviewAsync 와 같은 셈).</summary>
public interface INotificationPolicyPreviewSender
{
    /// <summary>이벤트가 없으면 <c>null</c>. 보낼 수 없는 상태(꺼짐 · 제한 없음)는 <see cref="InvalidOperationException"/>.</summary>
    Task<NotificationPolicyPreviewSendResultDto?> SendAsync(
        string eventCode, bool toTargets, string actor, string? actorEmail, CancellationToken ct = default);
}

/// <inheritdoc cref="INotificationPolicyPreviewSender" />
public sealed class NotificationPolicyPreviewSender(
    AppDbContext db,
    INotificationPolicyService policies,
    IPushSender push,
    IEmailSender email,
    IAccountEmailResolver addresses,
    IConfiguration config,
    ILogger<NotificationPolicyPreviewSender> logger) : INotificationPolicyPreviewSender
{
    private const string PolicyScreen = "/admin/system/notify-policy";

    /// <summary>눌렀을 때 열 화면. 없으면 알림관리로 돌아온다.</summary>
    private static readonly Dictionary<string, string> Screens = new(StringComparer.OrdinalIgnoreCase)
    {
        [NotificationEvents.ServerPing] = "/admin/status/server-ping",
    };

    /// <inheritdoc />
    public async Task<NotificationPolicyPreviewSendResultDto?> SendAsync(
        string eventCode, bool toTargets, string actor, string? actorEmail, CancellationToken ct = default)
    {
        var code = NotificationEvents.Normalize(eventCode);
        if (code is null) return null;

        var ev = await db.NotificationEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == code && !e.IsDeleted, ct);

        if (ev is null) return null;

        List<string> pushIds;
        List<string> mailTo;

        if (toTargets)
        {
            var preview = await policies.PreviewAsync(code, ct)
                          ?? throw new InvalidOperationException("이벤트를 찾을 수 없습니다.");

            if (preview.EventDisabled)
                throw new InvalidOperationException("이 이벤트를 꺼 두어 대상자가 없습니다. 켠 뒤에 보내십시오.");

            // 역할을 안 건 이벤트는 대상을 보내는 쪽이 정한다 — 여기서는 누구인지 알 수 없다.
            if (preview.Unrestricted)
                throw new InvalidOperationException("받는 역할을 하나도 걸지 않아 대상자를 정할 수 없습니다. 역할을 고른 뒤에 보내십시오.");

            pushIds = ev.SupportsPush
                ? [.. preview.Recipients.Where(r => r.PushAllowed).Select(r => r.UserId)]
                : [];

            // 본인이 메일을 꺼 둔 사람은 뺀다 — 실제 발송과 같은 규칙이어야 미리 본 것이 실제로 가는 것이 된다.
            mailTo = ev.SupportsEmail
                ? [.. preview.Recipients
                    .Where(r => r.EmailAllowed && !r.EmailOptedOut && !string.IsNullOrWhiteSpace(r.Email))
                    .Select(r => r.Email!.Trim())]
                : [];
        }
        else
        {
            pushIds = ev.SupportsPush ? [actor] : [];

            // 본인 받아보기는 주소 하나로 보내므로 「메일을 꺼 뒀다」에 막히지 않는다(보고서 메일 미리받아보기와 같다).
            mailTo = [];
            if (ev.SupportsEmail)
            {
                var found = await addresses.ByLoginIdsAsync([actor], ct);
                var address = found.TryGetValue(actor, out var a) ? a : actorEmail;
                if (!string.IsNullOrWhiteSpace(address)) mailTo.Add(address.Trim());
            }
        }

        mailTo = [.. mailTo
            .Where(a => System.Net.Mail.MailAddress.TryCreate(a, out _))
            .Distinct(StringComparer.OrdinalIgnoreCase)];

        var title = $"[미리받아보기] {ev.Name}";
        var body = string.IsNullOrWhiteSpace(ev.Description) ? ev.Name : ev.Description.Trim();
        var url = Screens.TryGetValue(code, out var screen) ? screen : PolicyScreen;

        var result = new NotificationPolicyPreviewSendResultDto
        {
            PushTargets = pushIds.Count,
            MailTargets = mailTo.Count,
        };

        if (pushIds.Count > 0)
        {
            // 이벤트 코드·구분을 싣지 않는다 — 실으면 정책이 다시 걸러 본인 받아보기가 막히고, 메일 곁가지가 한 통 더 낸다.
            var sent = await push.SendAsync(new SendPushDto
            {
                Owners = [.. pushIds.Select(id => new OwnerRefDto
                {
                    OwnerType = NotificationPolicyService.OwnerTypePortal,
                    OwnerKey = id,
                })],
                Message = new PushMessageDto
                {
                    Title = title,
                    Body = body,
                    Url = url,
                    Tag = $"notify-preview-{code}",
                    IconOwnerKey = actor,
                    TtlSeconds = 3600,
                },
            }, sentBy: actor, ct);

            result.PushSent = sent.Sent;
            result.PushNote = sent.Message;
        }

        if (mailTo.Count > 0)
        {
            var mailBody = $"{body}\n\n알림관리에서 {actor} 님이 보낸 미리받아보기입니다. 실제 알림은 이 이벤트가 일어날 때 나갑니다.";
            // 메일 단추는 절대 주소여야 한다. 기준 주소가 없으면 단추 없이 보낸다(PushSender 의 LinkOf 와 같다).
            var baseUrl = (config["Portal:BaseUrl"] ?? string.Empty).Trim().TrimEnd('/');
            var link = baseUrl.Length > 0 ? baseUrl + url : null;
            var label = link is null ? null : "포털에서 보기";

            var html = NoticeEmailTemplate.Render(title, mailBody, senderName: null, actionUrl: link, actionLabel: label);
            var text = NoticeEmailTemplate.PlainAlternative(title, mailBody, senderName: null, actionUrl: link, actionLabel: label);

            // 주소가 서로 보이지 않게 한 통씩 보낸다.
            foreach (var to in mailTo)
            {
                if (ct.IsCancellationRequested) break;

                var ok = true;
                try
                {
                    await email.SendAsync(to, title, html, html: true, attachments: null, textBody: text);
                    result.Mailed++;
                }
                catch (Exception ex)
                {
                    ok = false;
                    result.MailFailed++;
                    logger.LogError(ex, "미리받아보기 메일을 보내지 못했습니다. event={Event} to={To}", code, to);
                }

                await EmailSendLog.WriteAsync(db, logger, [to], title, mailBody, html: false,
                    actor, ok, ok ? null : "메일 서버가 받지 않음", CancellationToken.None);
            }
        }

        result.Message = Summarize(result, ev.SupportsPush, ev.SupportsEmail);

        logger.LogInformation(
            "알림 미리받아보기({Scope}): {Event} · 푸시 {PushTargets}명/{PushSent}기기 · 메일 {Mailed}/{MailTargets}통 (요청: {Actor})",
            toTargets ? "대상자" : "본인", code, result.PushTargets, result.PushSent,
            result.Mailed, result.MailTargets, actor);

        return result;
    }

    private static string Summarize(NotificationPolicyPreviewSendResultDto r, bool push, bool mail)
    {
        var parts = new List<string>();

        if (push)
        {
            parts.Add(r.PushTargets == 0
                ? "앱 푸시: 받을 사람 없음"
                : r.PushSent > 0
                    ? $"앱 푸시 {r.PushTargets}명 · 기기 {r.PushSent}대"
                    : $"앱 푸시 0대 — {r.PushNote ?? "보내지 못했습니다."}");
        }

        if (mail)
        {
            parts.Add(r.MailTargets == 0
                ? "이메일: 받을 주소 없음"
                : r.MailFailed > 0
                    ? $"이메일 {r.Mailed}통 (실패 {r.MailFailed})"
                    : $"이메일 {r.Mailed}통");
        }

        return string.Join(" · ", parts);
    }
}
