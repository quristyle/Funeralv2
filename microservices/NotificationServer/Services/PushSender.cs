using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NotificationServer.Data;
using NotificationServer.DTOs;
using NotificationServer.Options;
using WebPush;

namespace NotificationServer.Services;

/// <summary>
/// Web Push 발송.
/// </summary>
public interface IPushSender
{
    /// <summary>
    /// 주인 목록에게 보낸다. 주인 한 명이 기기 여러 대를 가질 수 있다.
    /// </summary>
    /// <param name="sentBy">
    /// 보낸 사람(포털 로그인 아이디). <b>기록에만 쓴다</b> — 누가 보냈는지는
    /// 발송 뒤에 가장 먼저 묻는 것이고, 그때 로그에 없으면 답할 길이 없다.
    /// 시스템이 저절로 보내는 것은 <c>null</c> 이다.
    /// </param>
    Task<SendPushResultDto> SendAsync(
        SendPushDto request, string? sentBy = null, CancellationToken ct = default);
}

/// <summary>
/// Web Push 발송 구현체
/// </summary>
/// <remarks>
/// <b>죽은 구독을 정리하는 것이 이 클래스의 절반이다.</b> 브라우저 구독은 사용자가
/// 브라우저를 지우거나 권한을 끄면 조용히 무효가 되고, 그 뒤로는 발송마다 실패가 쌓인다.
/// 푸시 서비스가 404·410 을 주면 "이 구독은 없다" 는 확정 신호이므로 바로 지운다.
///
/// <para>
/// 예전 헬프데스크 구현도 같은 일을 했지만, 대상을 고르는 로직(팀·회사·관리자 전체)이
/// 발송 코드와 얽혀 있어 헬프데스크 밖에서 쓸 수 없었다. 여기서는 <b>대상을 받기만</b> 한다.
/// </para>
/// </remarks>
public class PushSender : IPushSender
{
    private readonly AppDbContext _db;
    private readonly VapidOptions _vapid;
    private readonly PushDeliveryOptions _delivery;
    private readonly INotificationPreferenceService _preferences;
    private readonly IAvatarIconResolver _avatars;
    private readonly ILogger<PushSender> _logger;

    public PushSender(
        AppDbContext db,
        IOptions<VapidOptions> vapid,
        IOptions<PushDeliveryOptions> delivery,
        INotificationPreferenceService preferences,
        IAvatarIconResolver avatars,
        ILogger<PushSender> logger)
    {
        _db = db;
        _vapid = vapid.Value;
        _delivery = delivery.Value;
        _preferences = preferences;
        _avatars = avatars;
        _logger = logger;
    }

    /// <summary>포털 계정을 가리키는 주인 종류. 구독도 설정도 이 값으로 저장된다.</summary>
    private const string OwnerTypePortal = "jsini";

    // ── 못 보낸 까닭 ──────────────────────────────────────────
    //
    // **글자를 상수로 둔다.** 화면이 이 값으로 거르고 묶어 세므로(실패 사유별
    // 건수) 자리마다 다르게 적으면 같은 원인이 여러 갈래로 흩어진다.

    private const string ReasonNoSubscription = "구독한 기기 없음";
    private const string ReasonOptedOut = "본인이 푸시를 끔";
    private const string ReasonExpired = "구독 만료(정리함)";
    private const string ReasonDeliveryFailed = "전달 실패";
    private const string ReasonNoVapid = "서버에 VAPID 설정 없음";

    /// <summary>
    /// 보낼 사람 목록을 확정한다 — <b>역할을 사람으로 펴고, 뺄 사람을 덜고,
    /// 겹치는 사람을 하나로 줄인다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>부르는 쪽이 아니라 여기서 한다.</b> 엔드포인트에서 풀면 이 클래스를 직접
    /// 부르는 자리(배포 알림 · 구독 알림 · 앞으로 생길 것들)가 역할 칸을 조용히
    /// 흘린다 — 보낸 쪽은 보냈다고 믿는데 아무도 못 받는 갈래다.
    /// </para>
    /// <para>
    /// 역할표는 <c>accounts.id</c> 를 가리키고 구독·설정의 주인 키는
    /// <c>accounts.user_id</c>(로그인 아이디)라 둘을 이어서 꺼낸다 —
    /// <c>EmailEndpoints.ResolveRoleEmailsAsync</c> 와 같은 이음이다.
    /// </para>
    /// <para>
    /// <b>푸시를 끈 사람은 여기서 빼지 않는다.</b> 그 판정은 아래 한 곳
    /// (<c>GetPushDisabledAsync</c>)에 있어야 「왜 안 왔나」가 기록에 남는다.
    /// </para>
    /// </remarks>
    private async Task<List<OwnerRefDto>> ExpandOwnersAsync(
        SendPushDto request, CancellationToken ct)
    {
        var owners = (request.Owners ?? new List<OwnerRefDto>())
            .Where(o => !string.IsNullOrWhiteSpace(o.OwnerType) && !string.IsNullOrWhiteSpace(o.OwnerKey))
            .ToList();

        var roles = (request.Roles ?? new List<string>())
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

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
                // 오류가 아니다 — 그 역할인 사람이 없는 것뿐이다. 다만 조용히 0 명이
                // 되는 상황은 알아챌 수 있어야 한다(배포 알림이 같은 자리에 로그를 남긴다).
                _logger.LogWarning(
                    "역할 {Roles} 인 계정이 없어 푸시 대상이 늘지 않았습니다.", string.Join(",", roles));
            }

            owners.AddRange(loginIds.Select(id => new OwnerRefDto
            {
                OwnerType = OwnerTypePortal,
                OwnerKey = id
            }));
        }

        var excluded = (request.ExcludeOwnerKeys ?? new List<string>())
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // **주인 종류는 보지 않고 키만 대조한다.** 뺄 사람은 언제나 포털 계정
        // 아이디로 오고, 같은 아이디가 다른 종류로도 등록돼 있다면 그 역시
        // 같은 사람이다.
        return owners
            .Where(o => !excluded.Contains(o.OwnerKey))
            .DistinctBy(o => (o.OwnerType, o.OwnerKey))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<SendPushResultDto> SendAsync(
        SendPushDto request, string? sentBy = null, CancellationToken ct = default)
    {
        // **이번 발송을 묶는 열쇠.** 이 호출로 생기는 모든 줄이 같은 값을 든다 —
        // 「내 알림함」이 그것으로 묶어 한 줄로 보여 주고, 읽음도 그 단위다.
        var batchId = Guid.NewGuid().ToString();

        if (!_vapid.IsConfigured)
        {
            // **이것도 기록에 남긴다.** 화면에서는 「보냈는데 아무 일도 없었다」로
            // 보이는 갈래라, 남기지 않으면 나중에 그 시각에 무슨 일이 있었는지
            // 되짚을 방법이 없다.
            await LogAsync(request, sentBy, batchId, (await ExpandOwnersAsync(request, ct))
                .Select(o => (o.OwnerType, o.OwnerKey))
                .ToList(), ReasonNoVapid, ct);

            // 조용히 성공한 척하지 않는다. 설정이 반쪽이면 그렇게 말한다.
            return new SendPushResultDto
            {
                Message = "VAPID 설정이 없어 푸시를 보낼 수 없습니다. " +
                          "Vapid:Subject·PublicKey·PrivateKey 를 확인하세요."
            };
        }

        // **역할로 적어 온 대상을 사람으로 편다.** 부르는 쪽이 아니라 여기서 푸는
        // 까닭은 `SendPushDto.Roles` 머리말에 있다.
        var owners = await ExpandOwnersAsync(request, ct);

        if (owners.Count == 0)
        {
            return new SendPushResultDto { Message = "보낼 대상이 없습니다." };
        }

        // 본인이 푸시를 끈 사람은 여기서 빠진다.
        //
        // 구독을 지우지 않고 발송만 멈추는 방식이라, 이 판정을 하지 않으면 스위치가
        // 아무 일도 하지 않는다. **부르는 쪽이 이것을 기억하게 하지 않는다** —
        // 한 곳만 잊으면 새는 설정이 된다 (NotificationPreferenceService 머리말).
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

                return new SendPushResultDto
                {
                    OptedOut = optedOut,
                    Message = "대상이 모두 푸시 알림을 끄고 있습니다."
                };
            }

            // 남은 사람에게는 보내되, **빠진 사람도 기록한다** — 「저 사람만
            // 왜 안 왔나」의 답이 여기 있다.
            await LogAsync(request, sentBy, batchId, pushDisabled.ToList(), ReasonOptedOut, ct);
        }

        // 주인 목록으로 구독을 모은다.
        //
        // (OwnerType, OwnerKey) 쌍이 여러 개라 EF 로 한 번에 묶기가 지저분하다.
        // 종류별로 나눠 IN 질의를 돌린다 — 종류는 두세 개뿐이다.
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

            return new SendPushResultDto
            {
                OwnersWithoutSubscription = ownersWithout,
                OptedOut = optedOut,
                Message = "대상의 구독이 없습니다. 브라우저에서 알림을 허용했는지 확인하세요."
            };
        }

        // 구독이 하나도 없는 사람들. 위의 「하나도 없다」 갈래에 안 걸리는
        // 부분 집합이라 여기서 따로 남긴다.
        await LogAsync(request, sentBy, batchId, owners
            .Where(o => !withSubs.Contains((o.OwnerType, o.OwnerKey)))
            .Select(o => (o.OwnerType, o.OwnerKey))
            .ToList(), ReasonNoSubscription, ct);

        // **얼굴을 여기서 채운다.** 부르는 쪽은 사람의 아이디까지만 알고
        // 사진이 어디 있는지는 모른다 (IAvatarIconResolver 머리말).
        //
        // 발송 직전 한 번뿐이다 — 아래 반복은 기기마다 도는 자리라 그 안에서
        // 풀면 같은 사람의 사진을 기기 수만큼 조회하게 된다.
        await FillIconAsync(request.Message, ct);

        // **언제까지 배달할 것인가.** 이 두 값이 「오래 안 켜다 켜면 한꺼번에 쏟아진다」
        // 를 막는 손잡이다 — 자세한 사정은 PushDeliveryOptions 머리말에 있다.
        var ttl = _delivery.ClampTtl(request.Message.TtlSeconds);
        var topic = BuildTopic(request.Message);

        var payload = BuildPayload(request.Message, batchId, ttl);
        var client = new WebPushClient();
        var vapid = new VapidDetails(_vapid.Subject, _vapid.PublicKey, _vapid.PrivateKey);

        // 라이브러리 기본 TTL 은 28일이라 **반드시 덮어야 한다.** 옵션 이름은
        // WebPushClient 가 정한 것이고(headers · vapidDetails · TTL), 모르는 이름을
        // 넣으면 ArgumentException 으로 튕긴다.
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

        foreach (var sub in subscriptions)
        {
            ct.ThrowIfCancellationRequested();

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
        await _db.SaveChangesAsync(ct);

        return new SendPushResultDto
        {
            Sent = sent,
            Failed = failed,
            Removed = dead.Count,
            OwnersWithoutSubscription = ownersWithout,
            OptedOut = optedOut,
            // 하나도 못 보냈으면 이유를 말한다. 결과 숫자만 주면 화면이 "보낸 알림이
            // 없습니다" 밖에 할 말이 없다.
            Message = sent > 0
                ? null
                : dead.Count > 0 && failed == 0
                    ? "구독이 만료되어 정리했습니다. 알림을 다시 구독해 주세요."
                    : failed > 0
                        ? "구독한 기기에 알림을 전달하지 못했습니다. 구독을 해제한 뒤 다시 등록해 보세요."
                        : null
        };
    }

    /// <summary>
    /// 기록 한 줄을 만든다. <b>저장하지는 않는다</b> — 부르는 쪽이 발송 흐름의
    /// <c>SaveChangesAsync</c> 한 번에 함께 담는다.
    /// </summary>
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
            IsSuccess = success,
            FailureReason = reason,
            SentBy = sentBy,
        };

    /// <summary>
    /// 보내지 <b>못한</b> 사람들을 기록하고 바로 저장한다.
    ///
    /// <para>
    /// 바로 저장하는 이유는 이 갈래들이 대부분 <c>return</c> 으로 끝나기
    /// 때문이다 — 뒤에 오는 저장에 기대면 그 줄들이 통째로 사라진다.
    /// </para>
    ///
    /// <para>
    /// <b>기록에 실패해도 발송은 계속한다.</b> 여기서 던지면 「기록을 못 남겨서
    /// 알림도 못 보낸」 것이 되고, 그 맞바꿈은 뒤집혀 있다.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// <c>IconOwnerKey</c> 가 있으면 그 사람의 얼굴을 아이콘으로 채운다.
    /// </summary>
    /// <remarks>
    /// <b>이미 <c>Icon</c> 이 있으면 건드리지 않는다.</b> 주소를 손에 들고 부른
    /// 쪽의 뜻이 먼저다 — 덮어쓰면 「아이콘을 지정했는데 얼굴이 떴다」가 된다.
    /// </remarks>
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

    /// <summary>
    /// 푸시 서비스의 대기줄에서 겹칠 열쇠(<c>Topic</c>)를 규격에 맞게 만든다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// RFC 8030 은 이 값을 <b>base64url 글자 32자 이내</b>로 가둔다. 우리 태그는
    /// 사람이 읽는 글자라(<c>weather-warning:2026-001</c>) 그대로는 못 쓴다.
    /// </para>
    /// <para>
    /// 맞으면 그대로 쓰고, 안 맞으면 <b>해시로 접는다.</b> 글자만 걸러 내는 방식은
    /// 쓰지 않는다 — 구분 기호를 떼면 서로 다른 태그가 같은 값이 되어(<c>a:1b</c> 와
    /// <c>a1:b</c>) <b>남의 알림을 밀어내는</b> 사고가 난다.
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// 브라우저의 서비스워커가 읽는 모양으로 만든다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 헬프데스크가 쓰던 키 이름(<c>title</c>·<c>body</c>·<c>url</c>·<c>icon</c>·<c>tag</c>)을
    /// 그대로 쓴다. 이미 배포된 서비스워커가 그 이름을 읽고 있어서, 바꾸면 알림이 빈 채로 뜬다.
    /// </para>
    ///
    /// <para>
    /// [<c>nid</c> — 누른 알림이 어느 것인지 알려 주는 값]
    /// </para>
    ///
    /// <para>
    /// 「내 알림함」의 열쇠(<see cref="Entities.PushSendLog.BatchId"/>)를 그대로
    /// 싣는다. 이것이 없으면 <b>알림을 눌러 화면까지 열어 본 사람도 알림함에서는
    /// 안 읽은 채로 남는다</b> — 브라우저가 알려 주는 것은 제목·본문·주소뿐이라
    /// 서버가 남긴 어느 줄을 눌렀는지 맞출 방법이 없다.
    /// </para>
    ///
    /// <para>
    /// 주소(<c>url</c>)에 붙여 보내지 않는 이유는 <b>그 주소가 기록에도 남기</b>
    /// 때문이다(<c>push_send_logs.url</c>). 표시를 섞어 두면 알림함에서 그 주소를
    /// 다시 열 때도 읽음 표시가 따라다닌다. 표시를 붙이는 일은 <b>누른 그 순간에</b>
    /// 서비스워커가 한다(<c>push-sw.js</c>).
    /// </para>
    /// </remarks>
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
            // [sentAt · expiresAt — 늦게 도착한 것을 서비스워커가 알아보게 한다]
            //
            // TTL 은 푸시 서비스에게 「이때까지만 들고 있어라」고 부탁하는 값이지
            // 지켜진다는 보장이 아니다. 실제로 FCM 은 기기가 절전에서 깨는 순간
            // **줄에 남은 것을 한꺼번에** 흘려 보내고, 그중에는 이미 시효가 지난
            // 것도 섞인다. 브라우저는 우리가 언제 보냈는지 알려 주지 않으므로
            // (push 이벤트에 시각이 없다) **보낸 시각을 페이로드에 실어 준다** —
            // 그것이 있어야 워커가 「지금 알림」과 「밀려 있던 알림」을 가른다
            // (push-sw.js 의 지난 알림 묶음).
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
