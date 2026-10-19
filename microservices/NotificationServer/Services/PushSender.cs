using System.Text.Json;

using JSini.Shared.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NotificationServer.Data;
using NotificationServer.DTOs;
using NotificationServer.Options;
using WebPush;

namespace NotificationServer.Services;

/// <summary>Web Push 발송.</summary>
public interface IPushSender
{
    /// <summary>주인 목록에게 보낸다(주인 한 명이 기기 여럿일 수 있다). sentBy 는 기록용이고 시스템이 보낸 것은 null.</summary>
    Task<SendPushResultDto> SendAsync(
        SendPushDto request, string? sentBy = null, CancellationToken ct = default);
}

/// <summary>Web Push 발송 구현체. 404·410 을 준 구독은 죽은 것으로 보고 바로 지운다.
/// 대상은 받기만 하고 고르지 않는다(팀·회사 로직은 부르는 쪽).</summary>
public class PushSender : IPushSender
{
    private readonly AppDbContext _db;
    private readonly VapidOptions _vapid;
    private readonly PushDeliveryOptions _delivery;
    private readonly INotificationPreferenceService _preferences;
    private readonly INotificationPolicyService _policies;
    private readonly IAvatarIconResolver _avatars;
    private readonly IAccountEmailResolver _addresses;
    private readonly IEmailSender _email;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<PushSender> _logger;

    /// <summary>푸시 서비스 연결의 이름. 등록(제한 시간 포함)은 Program.cs 에 있다.</summary>
    public const string HttpClientName = "webpush";

    public PushSender(
        AppDbContext db,
        IOptions<VapidOptions> vapid,
        IOptions<PushDeliveryOptions> delivery,
        INotificationPreferenceService preferences,
        INotificationPolicyService policies,
        IAvatarIconResolver avatars,
        IAccountEmailResolver addresses,
        IEmailSender email,
        IConfiguration config,
        IHttpClientFactory http,
        ILogger<PushSender> logger)
    {
        _db = db;
        _vapid = vapid.Value;
        _delivery = delivery.Value;
        _preferences = preferences;
        _policies = policies;
        _avatars = avatars;
        _addresses = addresses;
        _email = email;
        _config = config;
        _http = http;
        _logger = logger;
    }

    /// <summary>포털 계정의 주인 종류. 구독·설정이 이 값으로 저장된다.</summary>
    private const string OwnerTypePortal = "jsini";

    // ── 못 보낸 까닭 ──────────────────────────
    // 화면이 이 글자로 거르고 묶으므로 상수로 둔다.

    private const string ReasonNoSubscription = "구독한 기기 없음";
    private const string ReasonOptedOut = "본인이 푸시를 끔";

    /// <summary>푸시는 켜고 댓글 갈래만 껐을 때(「다 껐다」와 가린다).</summary>
    private const string ReasonCommentOptedOut = "본인이 댓글 알림을 끔";
    private const string ReasonExpired = "구독 만료(정리함)";
    private const string ReasonDeliveryFailed = "전달 실패";
    private const string ReasonNoVapid = "서버에 VAPID 설정 없음";

    /// <summary>알림관리 정책으로 빠졌을 때(본인이 끈 것과 고칠 자리가 다르다).</summary>
    private const string ReasonPolicyExcluded = "알림관리 정책에서 제외";

    /// <summary>이벤트 자체를 껐을 때. 역할과 무관하게 아무에게도 안 간다.</summary>
    private const string ReasonEventDisabled = "알림관리에서 이벤트를 끔";

    /// <summary>보낼 사람을 확정한다(역할 펴기·제외·알림관리 정책). 푸시를 끈 사람은 여기서 빼지 않는다 — 판정은 GetPushDisabledAsync 한 곳.</summary>
    private async Task<ExpandedOwners> ExpandOwnersAsync(
        SendPushDto request, NotificationPolicyDecision decision, CancellationToken ct)
    {
        var picked = (request.Owners ?? new List<OwnerRefDto>())
            .Where(o => !string.IsNullOrWhiteSpace(o.OwnerType) && !string.IsNullOrWhiteSpace(o.OwnerKey))
            .ToList();

        var roles = (request.Roles ?? new List<string>())
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!decision.Unrestricted)
        {
            roles = decision.TargetsRoles
                ? [.. decision.RoleIds]
                : [.. roles.Where(r => decision.RoleIds.Contains(r, StringComparer.OrdinalIgnoreCase))];
        }

        var fromRoles = new List<OwnerRefDto>();

        if (roles.Count > 0)
        {
            var loginIds = await (
                from ra in _db.RoleAccounts
                where roles.Contains(ra.RoleId) && !ra.IsDeleted
                join a in _db.Accounts on ra.AccountId equals a.Id
                where !a.IsDeleted
                select a.UserId
            ).Distinct().ToListAsync(ct);

            if (loginIds.Count == 0)
            {
                // 0명은 오류가 아니지만 조용히 빠지지 않도록 로그를 남긴다.
                _logger.LogWarning(
                    "역할 {Roles} 인 계정이 없어 푸시 대상이 늘지 않았습니다.", string.Join(",", roles));
            }

            fromRoles.AddRange(loginIds.Select(id => new OwnerRefDto
            {
                OwnerType = OwnerTypePortal,
                OwnerKey = id
            }));
        }

        var dropped = new List<(string OwnerType, string OwnerKey)>();

        // ── 짚어 보낸 몫에 거름막 ───────────────────────────
        // 포털 계정만 거른다(역할표는 포털 계정의 것이다).
        if (decision is { Unrestricted: false, TargetsRoles: false } && picked.Count > 0)
        {
            var allowed = await _policies.GetLoginIdsInRolesAsync(decision.RoleIds, ct);

            var kept = new List<OwnerRefDto>(picked.Count);

            foreach (var owner in picked)
            {
                var portal = string.Equals(
                    owner.OwnerType, OwnerTypePortal, StringComparison.OrdinalIgnoreCase);

                if (!portal || allowed.Contains(owner.OwnerKey)) kept.Add(owner);
                else dropped.Add((owner.OwnerType, owner.OwnerKey));
            }

            picked = kept;
        }

        var excluded = (request.ExcludeOwnerKeys ?? new List<string>())
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 주인 종류는 보지 않고 키만 대조한다. 뺄 사람은 포털 계정 아이디로 온다.
        var owners = picked
            .Concat(fromRoles)
            .Where(o => !excluded.Contains(o.OwnerKey))
            .DistinctBy(o => (o.OwnerType, o.OwnerKey))
            .ToList();

        // 보낼 사람은 「정책 제외」 기록에서 뺀다(안 갔다로 찍히면 거짓이 된다).
        var sending = owners.Select(o => (o.OwnerType, o.OwnerKey)).ToHashSet();

        return new ExpandedOwners(owners, [.. dropped.Where(d => !sending.Contains(d))]);
    }

    /// <summary>역할을 펴고 정책으로 거른 결과.</summary>
    /// <param name="Owners">정책을 통과한 주인들.</param>
    /// <param name="PolicyExcluded">정책에서 빠진 주인들(「왜 안 왔나」 기록용).</param>
    private sealed record ExpandedOwners(
        List<OwnerRefDto> Owners,
        List<(string OwnerType, string OwnerKey)> PolicyExcluded);

    /// <inheritdoc />
    public async Task<SendPushResultDto> SendAsync(
        SendPushDto request, string? sentBy = null, CancellationToken ct = default)
    {
        // 이 발송의 묶음 열쇠(알림함이 한 줄로 묶는다).
        var batchId = Guid.NewGuid().ToString();

        // 아이콘은 못 보낸 줄에도 붙게 맨 앞에서 채운다(조회가 하나 더 든다).
        await FillIconAsync(request.Message, ct);

        // ── 알림관리(회사의 규칙) ───────────────────────────
        // 본인 설정보다 먼저 본다(정책에서 빠진 사람을 먼저 덜어야 「본인이 껐다」 기록이 맞다). 정책이 없으면 제한 없이 보낸다.
        var decision = await _policies.ResolveAsync(
            request.Message.EventCode, request.Message.Category, NotificationChannel.Push, ct);

        if (decision.Blocked)
        {
            // 보낼 뻔한 사람들을 까닭과 함께 남긴다.
            var intended = await ExpandOwnersAsync(request, NotificationPolicyDecision.Free, ct);

            await LogAsync(request, sentBy, batchId, intended.Owners
                .Select(o => (o.OwnerType, o.OwnerKey))
                .ToList(), ReasonEventDisabled, ct);

            // 메일도 막는다(이벤트를 끈 것은 이 알림을 보내지 말라는 뜻이다).
            return new SendPushResultDto
            {
                PolicyExcluded = intended.Owners.Count,
                Message = "알림관리에서 이 이벤트를 꺼 두어 보내지 않았습니다.",
            };
        }

        // ── 메일 곁가지 ─────────────────────────────────────
        // 푸시 되돌아가는 갈래보다 먼저다(푸시를 못 받는 사람에게 메일이 닿아야 한다).
        var mailed = await FanOutEmailAsync(request, sentBy, ct);

        // 푸시가 한 통도 못 가도 메일이 갔음을 화면에 알린다.
        SendPushResultDto WithMail(SendPushResultDto result)
        {
            result.Mailed = mailed;

            if (mailed > 0)
            {
                result.Message = string.IsNullOrWhiteSpace(result.Message)
                    ? $"이메일 {mailed}통을 보냈습니다."
                    : $"{result.Message} (이메일 {mailed}통은 보냈습니다.)";
            }

            return result;
        }

        if (!_vapid.IsConfigured)
        {
            // VAPID 없음도 기록에 남긴다(화면에서는 「보냈는데 아무 일도 없었다」로 보인다).
            await LogAsync(request, sentBy, batchId, (await ExpandOwnersAsync(request, decision, ct))
                .Owners
                .Select(o => (o.OwnerType, o.OwnerKey))
                .ToList(), ReasonNoVapid, ct);

            // 설정이 반쪽이면 성공으로 숨기지 않고 알린다.
            return WithMail(new SendPushResultDto
            {
                Message = "VAPID 설정이 없어 푸시를 보낼 수 없습니다. " +
                          "Vapid:Subject·PublicKey·PrivateKey 를 확인하세요."
            });
        }

        // 역할로 적어 온 대상을 사람으로 편다.
        var expanded = await ExpandOwnersAsync(request, decision, ct);
        var owners = expanded.Owners;
        var policyExcluded = expanded.PolicyExcluded.Count;

        if (policyExcluded > 0)
        {
            await LogAsync(request, sentBy, batchId, expanded.PolicyExcluded, ReasonPolicyExcluded, ct);
        }

        if (owners.Count == 0)
        {
            return WithMail(new SendPushResultDto
            {
                PolicyExcluded = policyExcluded,

                // 정책이 막은 것과 처음부터 대상이 없던 것을 가린다.
                Message = policyExcluded > 0
                    ? "알림관리 정책에 해당하는 역할의 사람이 없어 보내지 않았습니다."
                    : "보낼 대상이 없습니다.",
            });
        }

        // 본인이 푸시를 끈 사람은 뺀다(구독은 지우지 않고 발송만 멈춘다).
        var pushDisabled = await _preferences.GetPushDisabledAsync(owners, ct);
        var optedOut = 0;
        if (pushDisabled.Count > 0)
        {
            var before = owners.Count;
            owners = owners
                .Where(o => !pushDisabled.Contains((o.OwnerType, o.OwnerKey)))
                .ToList();
            optedOut = before - owners.Count;

            if (owners.Count == 0)
            {
                await LogAsync(request, sentBy, batchId, pushDisabled.ToList(), ReasonOptedOut, ct);

                return WithMail(new SendPushResultDto
                {
                    OptedOut = optedOut,
                    PolicyExcluded = policyExcluded,
                    Message = "대상이 모두 푸시 알림을 끄고 있습니다."
                });
            }

            // 빠진 사람도 기록한다(「저 사람만 왜 안 왔나」의 답).
            await LogAsync(request, sentBy, batchId, pushDisabled.ToList(), ReasonOptedOut, ct);
        }

        // ── 갈래 스위치 ─────────────────────────────────────
        // 댓글 갈래 스위치. 알림구분 HELPDESK_COMMENT 만 걸린다(HELPDESK 는 역할로 가서 걸리지 않는다).
        if (string.Equals(
                PushCategories.Normalize(request.Message.Category),
                PushCategories.HelpDeskComment,
                StringComparison.Ordinal))
        {
            var commentDisabled = await _preferences.GetCommentPushDisabledAsync(owners, ct);

            if (commentDisabled.Count > 0)
            {
                var before = owners.Count;
                owners = owners
                    .Where(o => !commentDisabled.Contains((o.OwnerType, o.OwnerKey)))
                    .ToList();
                optedOut += before - owners.Count;

                await LogAsync(request, sentBy, batchId, commentDisabled.ToList(),
                    ReasonCommentOptedOut, ct);

                if (owners.Count == 0)
                {
                    return WithMail(new SendPushResultDto
                    {
                        OptedOut = optedOut,
                        PolicyExcluded = policyExcluded,
                        Message = "대상이 모두 댓글 알림을 끄고 있습니다."
                    });
                }
            }
        }

        // 구독을 모은다. 주인 종류별로 나눠 IN 질의를 돈다.
        var subscriptions = new List<Entities.PushSubscription>();
        foreach (var group in owners.GroupBy(o => o.OwnerType))
        {
            var keys = group.Select(o => o.OwnerKey).Distinct().ToList();
            var found = await _db.PushSubscriptions
                .Where(s => s.OwnerType == group.Key && keys.Contains(s.OwnerKey))
                .ToListAsync(ct);
            subscriptions.AddRange(found);
        }

        var withSubs = subscriptions
            .Select(s => (s.OwnerType, s.OwnerKey))
            .ToHashSet();
        var ownersWithout = owners
            .Count(o => !withSubs.Contains((o.OwnerType, o.OwnerKey)));

        if (subscriptions.Count == 0)
        {
            await LogAsync(request, sentBy, batchId, owners
                .Select(o => (o.OwnerType, o.OwnerKey))
                .ToList(), ReasonNoSubscription, ct);

            return WithMail(new SendPushResultDto
            {
                OwnersWithoutSubscription = ownersWithout,
                OptedOut = optedOut,
                PolicyExcluded = policyExcluded,
                Message = "대상의 구독이 없습니다. 브라우저에서 알림을 허용했는지 확인하세요."
            });
        }

        // 구독이 없는 사람만 따로 남긴다(구독이 하나라도 있을 때).
        await LogAsync(request, sentBy, batchId, owners
            .Where(o => !withSubs.Contains((o.OwnerType, o.OwnerKey)))
            .Select(o => (o.OwnerType, o.OwnerKey))
            .ToList(), ReasonNoSubscription, ct);

        // 수명과 긴급도. 오래 꺼진 기기가 켜질 때 한꺼번에 쏟아지는 것을 막는 손잡이다.
        var ttl = _delivery.ClampTtl(request.Message.TtlSeconds);
        var topic = BuildTopic(request.Message);

        var payload = BuildPayload(request.Message, batchId, ttl);

        // 팩토리 연결을 쓴다(인자 없이 만들면 한 통마다 TLS 를 다시 맺는다).
        var client = new WebPushClient(_http.CreateClient(HttpClientName));
        var vapid = new VapidDetails(_vapid.Subject, _vapid.PublicKey, _vapid.PrivateKey);

        // 라이브러리 기본 TTL(28일)을 덮는다. 모르는 옵션 이름은 ArgumentException 이 난다.
        var headers = new Dictionary<string, object> { ["Urgency"] = _delivery.ResolveUrgency() };
        if (topic is not null) headers["Topic"] = topic;

        var sendOptions = new Dictionary<string, object>
        {
            ["vapidDetails"] = vapid,
            ["TTL"] = ttl,
            ["headers"] = headers,
        };

        var sent = 0;
        var failed = 0;
        var dead = new List<Entities.PushSubscription>();

        // 부르는 쪽이 끊었나(끊겨도 기록은 남긴다).
        var cancelled = false;

        foreach (var sub in subscriptions)
        {
            if (ct.IsCancellationRequested)
            {
                cancelled = true;
                break;
            }

            try
            {
                var target = new WebPush.PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                await client.SendNotificationAsync(target, payload, sendOptions, ct);

                sub.LastSentAt = DateTime.UtcNow;
                sub.FailureCount = 0;
                sent++;

                _db.PushSendLogs.Add(Row(request, sentBy, batchId, sub.OwnerType, sub.OwnerKey,
                    sub.Endpoint, success: true, reason: null));
            }
            catch (WebPushException ex) when (
                ex.StatusCode == System.Net.HttpStatusCode.NotFound ||
                ex.StatusCode == System.Net.HttpStatusCode.Gone)
            {
                // 확정적으로 없는 구독이다. 세지 않고 바로 지운다.
                dead.Add(sub);
                _db.PushSendLogs.Add(Row(request, sentBy, batchId, sub.OwnerType, sub.OwnerKey,
                    sub.Endpoint, success: false, reason: ReasonExpired));
                _logger.LogInformation(
                    "죽은 구독을 지웁니다. owner={Type}:{Key} status={Status}",
                    sub.OwnerType, sub.OwnerKey, (int)ex.StatusCode);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // 취소는 기기 실패가 아니라서 세지 않는다.
                cancelled = true;
                break;
            }
            catch (Exception ex)
            {
                // 일시적인 문제일 수 있다(네트워크·푸시 서비스 장애). 세어 두고 넘어간다.
                sub.FailureCount += 1;
                failed++;
                _db.PushSendLogs.Add(Row(request, sentBy, batchId, sub.OwnerType, sub.OwnerKey,
                    sub.Endpoint, success: false, reason: ReasonDeliveryFailed));
                _logger.LogWarning(ex,
                    "푸시 발송 실패. owner={Type}:{Key} 연속실패={Count}",
                    sub.OwnerType, sub.OwnerKey, sub.FailureCount);
            }
        }

        if (dead.Count > 0) _db.PushSubscriptions.RemoveRange(dead);

        // 끊겨도 기록은 저장한다(취소표를 넘기면 기록이 통째로 사라진다).
        await _db.SaveChangesAsync(cancelled ? CancellationToken.None : ct);

        return WithMail(new SendPushResultDto
        {
            Sent = sent,
            Failed = failed,
            Removed = dead.Count,
            OwnersWithoutSubscription = ownersWithout,
            OptedOut = optedOut,
            PolicyExcluded = policyExcluded,
            // 하나도 못 보냈으면 이유를 말한다(숫자만으로는 화면이 원인을 짚지 못한다).
            Message = sent > 0
                ? null
                : dead.Count > 0 && failed == 0
                    ? "구독이 만료되어 정리했습니다. 알림을 다시 구독해 주세요."
                    : failed > 0
                        ? "구독한 기기에 알림을 전달하지 못했습니다. 구독을 해제한 뒤 다시 등록해 보세요."
                        : null
        });
    }

    // ── 메일 곁가지 ─────────────────────────────────────────

    /// <summary>같은 알림을 메일로도 낸다. 보낸 통수를 돌려준다. 던지지 않는다(SMTP 장애가 푸시를 깨지 않게).
    /// 정책 줄이 없으면 이메일을 켠 역할이 없을 때 한 통도 안 낸다(푸시와 기본값이 반대다).</summary>
    private async Task<int> FanOutEmailAsync(SendPushDto request, string? sentBy, CancellationToken ct)
    {
        // 곁가지가 푸시를 깨지 않도록 바깥을 한 번 더 받는다.
        try
        {
            return await SendFanOutMailAsync(request, sentBy, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "메일 곁가지를 푸는 중에 실패했습니다. event={Event}",
                request.Message.EventCode ?? request.Message.Category);
            return 0;
        }
    }

    /// <summary>FanOutEmailAsync 의 알맹이. 던질 수 있다 — 받는 것은 저쪽이다.</summary>
    private async Task<int> SendFanOutMailAsync(SendPushDto request, string? sentBy, CancellationToken ct)
    {
        var decision = await _policies.ResolveAsync(
            request.Message.EventCode, request.Message.Category, NotificationChannel.Email, ct);

        // Unrestricted 도 「내지 않음」으로 읽는다(MailsFromPush 가 거짓이면 부르는 쪽이 따로 내므로 두 통이 가면 안 된다).
        if (decision is not { Blocked: false, Unrestricted: false, MailsFromPush: true }) return 0;

        // 역할 펴기와 제외는 푸시와 같다.
        var owners = (await ExpandOwnersAsync(request, decision, ct)).Owners;

        // 포털 계정만 메일을 낸다. 다른 주인 종류는 주소를 물을 계정이 없다.
        var loginIds = owners
            .Where(o => string.Equals(o.OwnerType, OwnerTypePortal, StringComparison.OrdinalIgnoreCase))
            .Select(o => o.OwnerKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (loginIds.Count == 0) return 0;

        // ── 메일은 켠 역할 안에서만 나간다 ──────────────
        // 짚어 보낸 몫도 항상 거른다(배포 알림이 슈퍼관리자 전원을 Owners 로 싣는다. 틀리면 되돌릴 수 없다).
        var allowed = await _policies.GetLoginIdsInRolesAsync(decision.RoleIds, ct);

        var outside = loginIds.Where(id => !allowed.Contains(id)).ToList();

        if (outside.Count > 0)
        {
            _logger.LogInformation(
                "메일 곁가지: 「이메일」을 켠 역할 밖이라 {Count}명을 뺐습니다. event={Event}",
                outside.Count, request.Message.EventCode ?? request.Message.Category);

            loginIds = loginIds.Where(allowed.Contains).ToList();
        }

        if (loginIds.Count == 0) return 0;

        // 본인이 끈 것이 마지막 말이다. 행이 없으면 켜짐이다(NotificationPreferenceService 머리말).
        var mailOff = await _preferences.GetEmailDisabledLoginIdsAsync(loginIds, ct);
        var wanted = loginIds.Where(id => !mailOff.Contains(id)).ToList();

        if (wanted.Count == 0)
        {
            _logger.LogInformation(
                "메일 곁가지: 대상 {Count}명이 모두 이메일을 꺼 두어 보내지 않았습니다. event={Event}",
                loginIds.Count, request.Message.EventCode ?? request.Message.Category);
            return 0;
        }

        var addressOf = await _addresses.ByLoginIdsAsync(wanted, ct);

            // 주소 꼴이 아닌 것은 뺀다(SMTP 가 한 주소를 거절하면 묶인 사람 모두 못 받는다).
        var recipients = addressOf.Values
            .Where(a => System.Net.Mail.MailAddress.TryCreate(a, out _))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (recipients.Count == 0)
        {
            _logger.LogWarning(
                "메일 곁가지: 받는 사람 {Count}명에 쓸 수 있는 메일 주소가 없습니다. event={Event}",
                wanted.Count, request.Message.EventCode ?? request.Message.Category);
            return 0;
        }

        var title = string.IsNullOrWhiteSpace(request.Message.Title)
            ? "알림"
            : request.Message.Title.Trim();

        var (actionUrl, actionLabel) = ResolveActionLink(request.Message);
        var body = (request.Message.Body ?? string.Empty).Trim();
        if (body.Length == 0) body = title;

        // 평문을 넘기고 회사 메일 꼴은 틀이 입힌다(NoticeEmailTemplate 머리말).
        var html = NoticeEmailTemplate.Render(title, body, senderName: null, actionUrl: actionUrl, actionLabel: actionLabel);
        var text = NoticeEmailTemplate.PlainAlternative(title, body, senderName: null, actionUrl: actionUrl, actionLabel: actionLabel);
        var logBody = actionUrl is not null ? $"{body}\n\n{actionLabel}: {actionUrl}" : body;

        var sent = 0;

        // ── 한 사람에 한 통씩 ───────────────────────────
        // 주소가 서로 보이지 않게 한 통씩 보낸다.
        foreach (var to in recipients)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                await _email.SendAsync(to, title, html, html: true, attachments: null, textBody: text);

                // 보내자마자 기록한다(끝에 모아 쓰면 도중 실패 때 나간 메일이 안 나간 것으로 남는다).
                await WriteMailLogAsync([to], title, logBody, sentBy,
                    success: true, failureReason: null);

                sent++;
            }
            catch (Exception ex)
            {
                // 한 사람의 실패가 나머지를 막지 않게 계속 보낸다.
                _logger.LogError(ex, "메일 곁가지를 보내지 못했습니다. event={Event} to={To}",
                    request.Message.EventCode ?? request.Message.Category, to);

                await WriteMailLogAsync([to], title, logBody, sentBy,
                    success: false, failureReason: "메일 서버가 받지 않음");
            }
        }

        _logger.LogInformation(
            "메일 곁가지 {Sent}/{Total}통. event={Event} 역할밖={Outside} 메일꺼둠={Off}",
            sent, recipients.Count, request.Message.EventCode ?? request.Message.Category,
            outside.Count, loginIds.Count - wanted.Count);

        return sent;
    }

    /// <summary>메일 곁가지의 발송 기록을 쓴다. 던지지 않으며 취소표를 넘기지 않는다(메일은 이미 나갔다).</summary>
    private async Task WriteMailLogAsync(
        IReadOnlyList<string> recipients, string title, string body, string? sentBy,
        bool success, string? failureReason)
    {
        try
        {
            await EmailSendLog.WriteAsync(
                _db, _logger, recipients, title, body, html: false,
                sentBy, success, failureReason, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "메일 곁가지의 발송 기록을 남기지 못했습니다. to={To}",
                string.Join(",", recipients));
        }
    }

    /// <summary>푸시 한 통을 메일 평문으로 편다. Url 은 절대 주소로 펴서 끝에 붙인다.
    /// Portal:BaseUrl 이 비면 링크 줄을 뺀다(상대 주소가 적힌 메일은 아무도 못 연다).</summary>
    private string ComposeMailBody(PushMessageDto message)
    {
        var lines = new List<string>();

        var body = (message.Body ?? string.Empty).Trim();
        if (body.Length > 0) lines.Add(body);

        if (LinkOf(message.Url) is { Length: > 0 } url)
        {
            if (lines.Count > 0) lines.Add(string.Empty);
            lines.Add($"포털에서 보기: {url}");
        }

        // 본문도 링크도 없으면 제목을 적는다(평문에는 제목 스타일이 없다).
        return lines.Count > 0 ? string.Join("\n", lines) : message.Title.Trim();
    }

    /// <summary>푸시의 <c>Url</c> 을 메일에 적을 수 있는 절대 주소로. 못 만들면 <c>null</c>.</summary>
    private string? LinkOf(string? url)
    {
        var path = (url ?? string.Empty).Trim();
        if (path.Length == 0) return null;

        // 부르는 쪽이 이미 절대 주소를 적었으면 그대로 쓴다.
        if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        var baseUrl = (_config["Portal:BaseUrl"] ?? string.Empty).Trim().TrimEnd('/');
        if (baseUrl.Length == 0) return null;

        return path.StartsWith('/') ? baseUrl + path : $"{baseUrl}/{path}";
    }

    /// <summary>메일 하단 단추의 주소와 라벨. 날씨 알림이면 「날씨 확인하기」, 그 밖은 「포털에서 보기」.</summary>
    private (string? Url, string? Label) ResolveActionLink(PushMessageDto message)
    {
        var url = LinkOf(message.Url);
        if (string.IsNullOrWhiteSpace(url)) return (null, null);

        var isWeather = string.Equals(message.Category, PushCategories.Weather, StringComparison.OrdinalIgnoreCase)
            || string.Equals(message.EventCode, NotificationEvents.Weather, StringComparison.OrdinalIgnoreCase)
            || (message.Url?.Contains("/weather", StringComparison.OrdinalIgnoreCase) ?? false);

        var label = isWeather ? "날씨 확인하기" : "포털에서 보기";
        return (url, label);
    }

    /// <summary>기록 한 줄을 만든다(저장은 부르는 쪽이 한다).</summary>
    private static Entities.PushSendLog Row(
        SendPushDto request, string? sentBy, string batchId,
        string ownerType, string ownerKey,
        string? endpoint, bool success, string? reason) => new()
        {
            SentAt = DateTime.UtcNow,
            BatchId = batchId,
            OwnerType = ownerType,
            OwnerKey = ownerKey,
            Endpoint = endpoint,
            Title = request.Message?.Title,
            Body = request.Message?.Body,
            Url = request.Message?.Url,

            // 보낼 때 함께 보관한다(제목으로 되짚으면 문구를 고칠 때 줄이 갈라진다).
            Category = PushCategories.Normalize(request.Message?.Category),
            IsSuccess = success,
            FailureReason = reason,
            SentBy = sentBy,

            // 띄운 얼굴도 보관한다(아이디만으로는 얼굴을 되짚을 수 없다).
            Icon = IconForLog(request.Message?.Icon),
        };

    /// <summary>기록용 아이콘 주소. 우리 얼굴 주소의 ?t= 열쇠(60분 시효)는 뗀다 — 기록은 몇 달 남는다. 직접 적은 주소는 건드리지 않는다.</summary>
    internal static string? IconForLog(string? icon)
    {
        if (string.IsNullOrWhiteSpace(icon)) return icon;

        var query = icon.IndexOf('?');
        if (query < 0) return icon;

        return icon.AsSpan(0, query).StartsWith(AvatarPathPrefix, StringComparison.OrdinalIgnoreCase)
            ? icon[..query]
            : icon;
    }

    /// <summary>얼굴 주소의 앞머리. <c>AvatarIconResolver</c> 가 만드는 모양이다.</summary>
    private const string AvatarPathPrefix = "/files/avatar/";

    /// <summary>못 보낸 사람들을 기록하고 바로 저장한다(갈래 대부분이 return 으로 끝나 뒤 저장에 기대면 줄이 사라진다). 기록 실패는 삼킨다.</summary>
    private async Task LogAsync(
        SendPushDto request, string? sentBy, string batchId,
        IReadOnlyList<(string OwnerType, string OwnerKey)> owners,
        string reason, CancellationToken ct)
    {
        if (owners.Count == 0) return;

        foreach (var (type, key) in owners)
        {
            _db.PushSendLogs.Add(Row(request, sentBy, batchId, type, key, null, success: false, reason));
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "푸시 발송 기록을 남기지 못했습니다.");
        }
    }

    /// <summary>IconOwnerKey 로 얼굴을 채운다. 이미 Icon 이 있으면 건드리지 않는다.</summary>
    private async Task FillIconAsync(PushMessageDto message, CancellationToken ct)
    {
        if (message is null
            || !string.IsNullOrWhiteSpace(message.Icon)
            || string.IsNullOrWhiteSpace(message.IconOwnerKey))
        {
            return;
        }

        message.Icon = await _avatars.ResolveAsync(message.IconOwnerKey, ct);
    }

    /// <summary>Topic 을 규격에 맞게 만든다. 32자 이내면 그대로, 아니면 해시로 접는다(글자만 거르면 서로 다른 태그가 같은 값이 되어 남의 알림을 밀어낸다).</summary>
    private static string? BuildTopic(PushMessageDto? message)
    {
        var raw = message?.Topic;
        if (string.IsNullOrWhiteSpace(raw)) raw = message?.Tag;
        if (string.IsNullOrWhiteSpace(raw)) return null;

        raw = raw.Trim();

        var safe = raw.Length <= 32 && raw.All(
            c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_');
        if (safe) return raw;

        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(raw));

        // base64url 로 접고 32자로 자른다. 24바이트면 32자가 정확히 나온다.
        return Convert.ToBase64String(hash, 0, 24)
            .Replace('+', '-')
            .Replace('/', '_');
    }

    /// <summary>서비스워커가 읽는 페이로드. 키 이름(title·body·url·icon·tag)을 바꾸면 빈 알림이 뜬다.
    /// nid 는 알림함 줄의 열쇠다(url 에 읽음 표시를 섞지 않고 push-sw.js 가 누른 순간 붙인다).</summary>
    private static string BuildPayload(PushMessageDto message, string batchId, int ttlSeconds)
    {
        var now = DateTimeOffset.UtcNow;

        var payload = new Dictionary<string, object?>
        {
            ["title"] = message.Title,
            ["body"] = message.Body,
            ["url"] = message.Url,
            ["icon"] = message.Icon,
            ["tag"] = message.Tag,
            ["nid"] = batchId,
            // sentAt·expiresAt — TTL 은 부탁이다. 절전에서 깬 기기가 밀린 알림을 한꺼번에 흘리므로 워커가 지금 것과 밀린 것을 가른다.
            ["sentAt"] = now.ToUnixTimeMilliseconds(),
            ["expiresAt"] = now.AddSeconds(ttlSeconds).ToUnixTimeMilliseconds(),
        };

        if (message.Data is { Count: > 0 })
        {
            payload["data"] = message.Data;
        }

        return JsonSerializer.Serialize(payload);
    }
}
