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
    /// <summary>주인 목록에게 보낸다. 주인 한 명이 기기 여러 대를 가질 수 있다.</summary>
    Task<SendPushResultDto> SendAsync(SendPushDto request, CancellationToken ct = default);
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
    private readonly INotificationPreferenceService _preferences;
    private readonly ILogger<PushSender> _logger;

    public PushSender(
        AppDbContext db,
        IOptions<VapidOptions> vapid,
        INotificationPreferenceService preferences,
        ILogger<PushSender> logger)
    {
        _db = db;
        _vapid = vapid.Value;
        _preferences = preferences;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SendPushResultDto> SendAsync(SendPushDto request, CancellationToken ct = default)
    {
        if (!_vapid.IsConfigured)
        {
            // 조용히 성공한 척하지 않는다. 설정이 반쪽이면 그렇게 말한다.
            return new SendPushResultDto
            {
                Message = "VAPID 설정이 없어 푸시를 보낼 수 없습니다. " +
                          "Vapid:Subject·PublicKey·PrivateKey 를 확인하세요."
            };
        }

        var owners = (request.Owners ?? new List<OwnerRefDto>())
            .Where(o => !string.IsNullOrWhiteSpace(o.OwnerType) && !string.IsNullOrWhiteSpace(o.OwnerKey))
            .ToList();

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
                return new SendPushResultDto
                {
                    OptedOut = optedOut,
                    Message = "대상이 모두 푸시 알림을 끄고 있습니다."
                };
            }
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
            return new SendPushResultDto
            {
                OwnersWithoutSubscription = ownersWithout,
                OptedOut = optedOut,
                Message = "대상의 구독이 없습니다. 브라우저에서 알림을 허용했는지 확인하세요."
            };
        }

        var payload = BuildPayload(request.Message);
        var client = new WebPushClient();
        var vapid = new VapidDetails(_vapid.Subject, _vapid.PublicKey, _vapid.PrivateKey);

        var sent = 0;
        var failed = 0;
        var dead = new List<Entities.PushSubscription>();

        foreach (var sub in subscriptions)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var target = new WebPush.PushSubscription(sub.Endpoint, sub.P256dh, sub.Auth);
                await client.SendNotificationAsync(target, payload, vapid, ct);

                sub.LastSentAt = DateTime.UtcNow;
                sub.FailureCount = 0;
                sent++;
            }
            catch (WebPushException ex) when (
                ex.StatusCode == System.Net.HttpStatusCode.NotFound ||
                ex.StatusCode == System.Net.HttpStatusCode.Gone)
            {
                // 확정적으로 없는 구독이다. 세지 않고 바로 지운다.
                dead.Add(sub);
                _logger.LogInformation(
                    "죽은 구독을 지웁니다. owner={Type}:{Key} status={Status}",
                    sub.OwnerType, sub.OwnerKey, (int)ex.StatusCode);
            }
            catch (Exception ex)
            {
                // 일시적인 문제일 수 있다(네트워크·푸시 서비스 장애). 세어 두고 넘어간다.
                sub.FailureCount += 1;
                failed++;
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
    /// 브라우저의 서비스워커가 읽는 모양으로 만든다.
    /// </summary>
    /// <remarks>
    /// 헬프데스크가 쓰던 키 이름(<c>title</c>·<c>body</c>·<c>url</c>·<c>icon</c>·<c>tag</c>)을
    /// 그대로 쓴다. 이미 배포된 서비스워커가 그 이름을 읽고 있어서, 바꾸면 알림이 빈 채로 뜬다.
    /// </remarks>
    private static string BuildPayload(PushMessageDto message)
    {
        var payload = new Dictionary<string, object?>
        {
            ["title"] = message.Title,
            ["body"] = message.Body,
            ["url"] = message.Url,
            ["icon"] = message.Icon,
            ["tag"] = message.Tag
        };

        if (message.Data is { Count: > 0 })
        {
            payload["data"] = message.Data;
        }

        return JsonSerializer.Serialize(payload);
    }
}
