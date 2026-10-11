using System.Text.Json;

using JSini.Shared.DTOs;
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
    /// <param name="request">보낼 대상과 내용.</param>
    /// <param name="sentBy">
    /// 보낸 사람(포털 로그인 아이디). <b>기록에만 쓴다</b> — 누가 보냈는지는
    /// 발송 뒤에 가장 먼저 묻는 것이고, 그때 로그에 없으면 답할 길이 없다.
    /// 시스템이 저절로 보내는 것은 <c>null</c> 이다.
    /// </param>
    /// <param name="ct">
    /// 끊김표. <b>끊겨도 여기까지의 기록은 남는다</b> — 구현부의 저장 주석 참고.
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
    private readonly INotificationPolicyService _policies;
    private readonly IAvatarIconResolver _avatars;
    private readonly IAccountEmailResolver _addresses;
    private readonly IEmailSender _email;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<PushSender> _logger;

    /// <summary>
    /// 푸시 서비스로 나가는 연결의 이름. 등록은 <c>Program.cs</c> 에 있다 —
    /// <b>연결을 물려 쓰고 제한 시간을 못 박는 것</b>이 거기 있는 이유다.
    /// </summary>
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

    /// <summary>포털 계정을 가리키는 주인 종류. 구독도 설정도 이 값으로 저장된다.</summary>
    private const string OwnerTypePortal = "jsini";

    // ── 못 보낸 까닭 ──────────────────────────────────────────
    //
    // **글자를 상수로 둔다.** 화면이 이 값으로 거르고 묶어 세므로(실패 사유별
    // 건수) 자리마다 다르게 적으면 같은 원인이 여러 갈래로 흩어진다.

    private const string ReasonNoSubscription = "구독한 기기 없음";
    private const string ReasonOptedOut = "본인이 푸시를 끔";

    /// <summary>
    /// 푸시는 켜 두었는데 <b>댓글 갈래만</b> 껐을 때. 위의 것과 <b>가려 적는다</b> —
    /// 「왜 저 사람만 안 왔나」의 답이 「다 껐다」와 「댓글만 껐다」로 갈린다.
    /// </summary>
    private const string ReasonCommentOptedOut = "본인이 댓글 알림을 끔";
    private const string ReasonExpired = "구독 만료(정리함)";
    private const string ReasonDeliveryFailed = "전달 실패";
    private const string ReasonNoVapid = "서버에 VAPID 설정 없음";

    /// <summary>
    /// 「알림관리」가 정한 역할에 안 들어 빠졌을 때. <b>본인이 끈 것과 가려
    /// 적는다</b> — 고칠 자리가 서로 다르다(한쪽은 포털관리의 알림관리,
    /// 다른 쪽은 그 사람의 환경설정).
    /// </summary>
    private const string ReasonPolicyExcluded = "알림관리 정책에서 제외";

    /// <summary>이벤트 자체를 꺼 두었을 때. 역할과 상관없이 아무에게도 안 간다.</summary>
    private const string ReasonEventDisabled = "알림관리에서 이벤트를 끔";

    /// <summary>
    /// 보낼 사람 목록을 확정한다 — <b>역할을 사람으로 펴고, 뺄 사람을 덜고,
    /// 겹치는 사람을 하나로 줄인다.</b> 「알림관리」 정책도 여기서 건다.
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
    ///
    /// <para>
    /// [정책이 하는 일은 <b>대상의 성격에 따라 갈린다</b>]
    /// </para>
    ///
    /// <para>
    /// 역할로 가는 알림(<c>ROLE</c>)은 정책의 역할이 부르는 쪽이 적어 둔 역할을
    /// <b>대신한다</b> — 설정 파일(<c>DeployNotify:RoleId</c> 따위)을 고치지 않고
    /// 화면에서 수신 역할을 바꾸는 길이 이것 하나다. 반면 <b>사람을 짚어 보낸
    /// 몫</b>(<c>Owners</c>)은 그대로 둔다: AI 작업 결과가 시킨 본인에게 돌아가는
    /// 것 같은 몫은 「회사가 정하는 수신자」가 아니라 그 일의 일부라, 역할로
    /// 가리면 기능이 통째로 깨진다.
    /// </para>
    ///
    /// <para>
    /// 당사자에게 가는 알림(<c>USER</c>)은 반대다. 짚어 보낸 몫이 곧 그 알림의
    /// 전부라, 거기에 거름막을 걸지 않으면 정책이 아무 일도 못 한다 — 「쪽지
    /// 알림은 이 역할들만 받는다」를 적을 자리가 사라진다.
    /// </para>
    ///
    /// <para>
    /// <b>푸시를 끈 사람은 여기서 빼지 않는다.</b> 그 판정은 아래 한 곳
    /// (<c>GetPushDisabledAsync</c>)에 있어야 「왜 안 왔나」가 기록에 남는다.
    /// </para>
    /// </remarks>
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
                // 역할 대상 — 정책이 받는 역할을 정한다.
                ? [.. decision.RoleIds]
                // 당사자 대상 — 역할로 보내는 몫이 있다면 정책 안의 것만 남긴다.
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
                // 오류가 아니다 — 그 역할인 사람이 없는 것뿐이다. 다만 조용히 0 명이
                // 되는 상황은 알아챌 수 있어야 한다(배포 알림이 같은 자리에 로그를 남긴다).
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
        //
        // **포털 계정만 가린다.** 역할표는 포털 계정의 것이라 `ownerType` 이
        // `jsini` 가 아닌 주인은 역할을 물을 길이 없다. 가릴 근거가 없는데
        // 막으면 틀리는 방향이 조용히 안 가는 쪽이 된다
        // (`INotificationPolicyService` 머리말).
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

        // **주인 종류는 보지 않고 키만 대조한다.** 뺄 사람은 언제나 포털 계정
        // 아이디로 오고, 같은 아이디가 다른 종류로도 등록돼 있다면 그 역시
        // 같은 사람이다.
        var owners = picked
            .Concat(fromRoles)
            .Where(o => !excluded.Contains(o.OwnerKey))
            .DistinctBy(o => (o.OwnerType, o.OwnerKey))
            .ToList();

        // 보내기로 한 사람은 「빠진 사람」에서 덜어 낸다 — 역할로도 걸려 있는
        // 사람이 짚은 몫에서 빠졌다고 「안 갔다」로 기록되면 거짓이 된다.
        var sending = owners.Select(o => (o.OwnerType, o.OwnerKey)).ToHashSet();

        return new ExpandedOwners(owners, [.. dropped.Where(d => !sending.Contains(d))]);
    }

    /// <summary>
    /// 역할을 펴고 정책으로 거른 결과.
    /// </summary>
    /// <param name="Owners">실제로 보낼 주인들.</param>
    /// <param name="PolicyExcluded">
    /// 「알림관리」 정책에 안 들어 빠진 주인들. <b>버리지 않고 들고 나온다</b> —
    /// 「저 사람만 왜 안 왔나」의 답이 기록에 남아야 한다.
    /// </param>
    private sealed record ExpandedOwners(
        List<OwnerRefDto> Owners,
        List<(string OwnerType, string OwnerKey)> PolicyExcluded);

    /// <inheritdoc />
    public async Task<SendPushResultDto> SendAsync(
        SendPushDto request, string? sentBy = null, CancellationToken ct = default)
    {
        // **이번 발송을 묶는 열쇠.** 이 호출로 생기는 모든 줄이 같은 값을 든다 —
        // 「내 알림함」이 그것으로 묶어 한 줄로 보여 주고, 읽음도 그 단위다.
        var batchId = Guid.NewGuid().ToString();

        // **얼굴을 맨 앞에서 채운다.** 부르는 쪽은 사람의 아이디까지만 알고
        // 사진이 어디 있는지는 모른다 (IAvatarIconResolver 머리말).
        //
        // 한동안 이것이 **발송 직전**에 있었다. 한 번만 풀려고 그랬는데(아래
        // 반복은 기기마다 도는 자리다) 그 자리는 아래 되돌아가는 갈래 넷보다
        // 뒤였다 — 대상이 없다 · 다들 껐다 · 구독이 없다 · VAPID 가 없다.
        // 그래서 **못 보낸 줄에는 얼굴이 안 실렸고**, 하필 그 사람들이
        // 알림함을 가장 많이 보는 사람들이다(푸시를 못 받으니까).
        //
        // 여기서도 한 번뿐이다. 되돌아가는 갈래에서 조회 하나가 더 붙지만
        // 드문 길이고, 그 대가로 알림함의 얼굴이 성공·실패를 가리지 않는다.
        await FillIconAsync(request.Message, ct);

        // ── 알림관리(회사의 규칙) ───────────────────────────
        //
        // **본인 설정보다 먼저 본다.** 정책에서 빠진 사람을 먼저 덜어 내야
        // 「본인이 껐다」는 기록이 실제로 껐던 사람에게만 남는다. 순서가
        // 뒤집히면 발송 이력의 사유가 뒤섞여, 「왜 안 왔나」에 엉뚱한 답이 뜬다.
        //
        // **정책을 못 찾으면 제한 없이 보낸다.** 조용히 막지 않는 것이 이
        // 설계의 가장 중요한 성질이다(`INotificationPolicyService` 머리말).
        var decision = await _policies.ResolveAsync(
            request.Message.EventCode, request.Message.Category, NotificationChannel.Push, ct);

        if (decision.Blocked)
        {
            // 보낼 뻔한 사람들을 **까닭과 함께** 남긴다. 아무 기록도 없으면
            // 「그날 알림이 왜 하나도 안 왔나」를 되짚을 방법이 없다.
            var intended = await ExpandOwnersAsync(request, NotificationPolicyDecision.Free, ct);

            await LogAsync(request, sentBy, batchId, intended.Owners
                .Select(o => (o.OwnerType, o.OwnerKey))
                .ToList(), ReasonEventDisabled, ct);

            // **메일 곁가지도 안 탄다.** 이벤트를 끈 것은 「이 알림을 보내지
            // 마라」이지 「앱 푸시만 보내지 마라」가 아니다 — 곁가지를 부르는
            // 자리가 이 되돌아감 뒤에 있는 까닭이 그것이다.
            return new SendPushResultDto
            {
                PolicyExcluded = intended.Owners.Count,
                Message = "알림관리에서 이 이벤트를 꺼 두어 보내지 않았습니다.",
            };
        }

        // ── 메일 곁가지 ─────────────────────────────────────
        //
        // **아래 되돌아가는 갈래들보다 먼저다.** VAPID 가 없다 · 구독한 기기가
        // 없다 · 다들 푸시를 껐다 — 메일을 켜 둔 뜻은 정확히 그런 사람에게도
        // 닿자는 것이라, 뒤에 두면 가장 필요한 사람에게 안 간다
        // (`FanOutEmailAsync` 머리말).
        var mailed = await FanOutEmailAsync(request, sentBy, ct);

        // 메일 통수를 결과에 싣고, 푸시가 한 통도 못 갔을 때 **메일은 갔다**는
        // 것을 말해 준다 — 안 그러면 화면이 「보낼 대상이 없습니다」만 띄우고
        // 관리자는 아무것도 안 나간 줄 안다.
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
            // **이것도 기록에 남긴다.** 화면에서는 「보냈는데 아무 일도 없었다」로
            // 보이는 갈래라, 남기지 않으면 나중에 그 시각에 무슨 일이 있었는지
            // 되짚을 방법이 없다.
            await LogAsync(request, sentBy, batchId, (await ExpandOwnersAsync(request, decision, ct))
                .Owners
                .Select(o => (o.OwnerType, o.OwnerKey))
                .ToList(), ReasonNoVapid, ct);

            // 조용히 성공한 척하지 않는다. 설정이 반쪽이면 그렇게 말한다.
            return WithMail(new SendPushResultDto
            {
                Message = "VAPID 설정이 없어 푸시를 보낼 수 없습니다. " +
                          "Vapid:Subject·PublicKey·PrivateKey 를 확인하세요."
            });
        }

        // **역할로 적어 온 대상을 사람으로 편다.** 부르는 쪽이 아니라 여기서 푸는
        // 까닭은 `SendPushDto.Roles` 머리말에 있다. 정책이 역할을 더하고 더는
        // 것도 같은 자리에서 한다.
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

                // **「정책이 막았다」와 「처음부터 대상이 없었다」를 가려 말한다.**
                // 고칠 자리가 서로 다르고, 뭉뚱그리면 설정을 존중한 결과가
                // 고장으로 읽힌다.
                Message = policyExcluded > 0
                    ? "알림관리 정책에 해당하는 역할의 사람이 없어 보내지 않았습니다."
                    : "보낼 대상이 없습니다.",
            });
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

                return WithMail(new SendPushResultDto
                {
                    OptedOut = optedOut,
                    PolicyExcluded = policyExcluded,
                    Message = "대상이 모두 푸시 알림을 끄고 있습니다."
                });
            }

            // 남은 사람에게는 보내되, **빠진 사람도 기록한다** — 「저 사람만
            // 왜 안 왔나」의 답이 여기 있다.
            await LogAsync(request, sentBy, batchId, pushDisabled.ToList(), ReasonOptedOut, ct);
        }

        // ── 갈래 스위치 ─────────────────────────────────────
        //
        // **댓글 알림은 따로 끌 수 있다.** 위의 것은 푸시 전체를 끄는 스위치이고
        // 이것은 그 아래 갈래 하나다 — 헬프데스크에 글을 자주 쓰는 사람은 답글마다
        // 울리는 것을 버거워하지만, 그렇다고 배포·쪽지까지 막으려는 것은 아니다.
        //
        // **알림구분으로 가른다.** 「요청이 올라왔다」(HELPDESK)는 처리할 사람에게
        // 역할로 가는 업무 알림이라 여기 걸리지 않는다. 걸리는 것은 내가 쓴 글에
        // 달린 답(HELPDESK_COMMENT) 하나다.
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

            return WithMail(new SendPushResultDto
            {
                OwnersWithoutSubscription = ownersWithout,
                OptedOut = optedOut,
                PolicyExcluded = policyExcluded,
                Message = "대상의 구독이 없습니다. 브라우저에서 알림을 허용했는지 확인하세요."
            });
        }

        // 구독이 하나도 없는 사람들. 위의 「하나도 없다」 갈래에 안 걸리는
        // 부분 집합이라 여기서 따로 남긴다.
        await LogAsync(request, sentBy, batchId, owners
            .Where(o => !withSubs.Contains((o.OwnerType, o.OwnerKey)))
            .Select(o => (o.OwnerType, o.OwnerKey))
            .ToList(), ReasonNoSubscription, ct);

        // **언제까지 배달할 것인가.** 이 두 값이 「오래 안 켜다 켜면 한꺼번에 쏟아진다」
        // 를 막는 손잡이다 — 자세한 사정은 PushDeliveryOptions 머리말에 있다.
        var ttl = _delivery.ClampTtl(request.Message.TtlSeconds);
        var topic = BuildTopic(request.Message);

        var payload = BuildPayload(request.Message, batchId, ttl);

        // **연결은 팩토리가 들고 있는 것을 쓴다.** 인자 없이 만들면 이 클라이언트가
        // HttpClient 를 스스로 하나 만들고, 그러면 알림 한 통마다 푸시 서비스와
        // TLS 손잡기를 다시 한다 (Program.cs 의 등록 주석).
        var client = new WebPushClient(_http.CreateClient(HttpClientName));
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

        // 부르는 쪽이 도중에 끊었나. **끊겨도 여기까지의 기록은 남겨야 한다** —
        // 아래 저장 주석에 까닭이 있다.
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
                // **부르는 쪽이 끊은 것은 이 기기의 실패가 아니다.** 시도가 끝나지
                // 않았으므로 세지 않고, 여기까지의 기록만 남기고 나온다.
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

        // **끊겼을 때는 취소표를 넘기지 않는다.** 그대로 넘기면 저장까지 함께
        // 취소되어 **이 발송의 기록이 통째로 사라진다** — 「몇 대까지 갔고 어디서
        // 멈췄나」가 가장 알고 싶은 갈래인데 하필 그때 아무것도 안 남는다.
        await _db.SaveChangesAsync(cancelled ? CancellationToken.None : ct);

        return WithMail(new SendPushResultDto
        {
            Sent = sent,
            Failed = failed,
            Removed = dead.Count,
            OwnersWithoutSubscription = ownersWithout,
            OptedOut = optedOut,
            PolicyExcluded = policyExcluded,
            // 하나도 못 보냈으면 이유를 말한다. 결과 숫자만 주면 화면이 "보낸 알림이
            // 없습니다" 밖에 할 말이 없다.
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

    /// <summary>
    /// 같은 알림을 <b>메일로도</b> 낸다. 보낸 통수를 돌려준다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// [무엇을 푸는가 — 「이메일」 체크가 듣지 않던 이벤트들]
    /// </para>
    ///
    /// <para>
    /// 알림관리 화면의 「이메일」 칸은 한동안 이벤트 여섯에서 <b>잠겨</b> 있었다
    /// (배포 완료 · 헬프데스크 요청 · 생일 · 기상 · 새 기기 구독 · 시험 발송).
    /// 그 이벤트들은 보내는 쪽이 <see cref="SendAsync"/> 만 부르고
    /// <c>/emails/send</c> 는 부르지 않아서, 체크를 켜 봐야 메일을 낼 사람이
    /// 아무 데도 없었기 때문이다 — 열어 두면 「켰는데 왜 안 오지」가 된다.
    /// 그 빈자리를 여기서 메운다.
    /// </para>
    ///
    /// <para>
    /// [<b>적어 둔 때만</b> 나간다 — 푸시와 기본값이 반대다]
    /// </para>
    ///
    /// <para>
    /// 정책 줄이 없는 이벤트를 푸시에서는 <b>제한 없음</b>으로 본다(지금까지와
    /// 똑같이 보낸다). 메일은 그 반대로 둔다 — <b>「이메일」을 켠 역할이 하나도
    /// 없으면 한 통도 안 낸다.</b> 같은 기본값을 쓰면 이 코드가 올라가는 날
    /// 배포·생일·기상 알림이 <b>전 직원 메일함으로</b> 쏟아진다. 조용히 막지
    /// 않는 것이 중요한 만큼 <b>조용히 늘리지 않는 것</b>도 중요하고, 늘리는
    /// 쪽은 되돌릴 수가 없다.
    /// </para>
    ///
    /// <para>
    /// [푸시가 못 갔어도 메일은 나간다]
    /// </para>
    ///
    /// <para>
    /// 부르는 자리를 <see cref="SendAsync"/> 의 <b>앞쪽</b>에 둔다 — VAPID 가 없다 ·
    /// 구독한 기기가 없다 · 다들 푸시를 껐다로 되돌아가는 갈래보다 먼저다.
    /// 뒤에 두면 <b>푸시를 못 받는 사람에게 메일도 안 가는데</b>, 메일을 켜 둔
    /// 뜻은 정확히 그 반대다.
    /// </para>
    ///
    /// <para>
    /// [그래도 지키는 것 둘]
    /// </para>
    ///
    /// <list type="bullet">
    ///   <item><description><b>이벤트를 꺼 두었으면(<c>Blocked</c>) 안 낸다.</b>
    ///   부르는 자리가 그 판정 뒤다.</description></item>
    ///   <item><description><b>본인이 메일을 껐으면 안 낸다</b>
    ///   (<c>email_enabled</c>). 순서는 푸시와 같다 — 정책 → 본인 설정.</description></item>
    /// </list>
    ///
    /// <para>
    /// <b>던지지 않는다.</b> SMTP 가 막힌 것이 푸시 발송을 통째로 깨뜨리면,
    /// 곁가지 하나를 더한 값으로는 너무 비싸다. 못 보낸 것은 기록
    /// (<see cref="EmailSendLog"/>)과 로그에 남긴다.
    /// </para>
    /// </remarks>
    private async Task<int> FanOutEmailAsync(SendPushDto request, string? sentBy, CancellationToken ct)
    {
        // **곁가지가 줄기를 못 꺾는다.** 아래에는 DB 질의가 넷 있고, 그중 어느
        // 하나가 터지면 <b>푸시가 통째로 실패한다</b> — 메일을 곁들이려다 앱
        // 알림까지 못 가게 하는 것은 값이 맞지 않는다. 안쪽의 `try` 는 SMTP 만
        // 감싸므로 그 바깥을 여기서 한 번 더 받는다.
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

    /// <summary>
    /// <see cref="FanOutEmailAsync"/> 의 알맹이. <b>던질 수 있다</b> — 받는 것은
    /// 저쪽이다.
    /// </summary>
    private async Task<int> SendFanOutMailAsync(SendPushDto request, string? sentBy, CancellationToken ct)
    {
        var decision = await _policies.ResolveAsync(
            request.Message.EventCode, request.Message.Category, NotificationChannel.Email, ct);

        // `Unrestricted` 를 **여기서는 「내지 마라」로 읽는다** — 머리말의
        // 「적어 둔 때만 나간다」. `MailsFromPush` 가 거짓이면 부르는 쪽이
        // `/emails/send` 로 제 틀의 메일을 따로 내고 있다(두 통 가면 안 된다).
        if (decision is not { Blocked: false, Unrestricted: false, MailsFromPush: true }) return 0;

        // **푸시와 같은 셈을 쓴다.** 역할을 사람으로 펴는 것도, 짚어 보낸 몫에
        // 거름막을 거는 것도 저쪽과 한 글자도 달라서는 안 된다 — 어긋나면
        // 「푸시는 왔는데 메일은 안 왔다」가 설명할 수 없는 일이 된다.
        var owners = (await ExpandOwnersAsync(request, decision, ct)).Owners;

        // 포털 계정만 메일을 낼 수 있다. 다른 주인 종류는 계정이 없어 주소를
        // 물을 길이 없다(푸시는 구독만 있으면 가므로 저쪽에서는 남는다).
        var loginIds = owners
            .Where(o => string.Equals(o.OwnerType, OwnerTypePortal, StringComparison.OrdinalIgnoreCase))
            .Select(o => o.OwnerKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (loginIds.Count == 0) return 0;

        // **본인이 끈 것이 마지막 말이다.** 행이 없으면 켜짐이라 「끈 사람」을
        // 묻는다(NotificationPreferenceService 머리말).
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

        // 주소 꼴이 아닌 것은 뺀다 — SMTP 가 거절하면 **그 통에 묶인 사람이
        // 모두** 못 받는다. 한 사람의 오타가 나머지를 끌고 가지 않게 한다.
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

        var body = ComposeMailBody(request.Message);

        try
        {
            // 평문을 넘기고 **회사 메일 꼴은 틀이 입힌다** — 직발송이 같은
            // 자리에서 하는 일이다(`NoticeEmailTemplate` 머리말).
            await _email.SendAsync(
                string.Join(",", recipients), title,
                NoticeEmailTemplate.Render(title, body),
                html: true, attachments: null,
                textBody: NoticeEmailTemplate.PlainAlternative(title, body));

            // **표에도 남긴다.** 로그 파일만으로는 「이 사람에게 무엇이 나갔나」에
            // 답할 수 없다(EmailSendLog 머리말).
            //
            // **취소표를 넘기지 않는다.** 메일은 이미 나갔다 — 여기서 부르는
            // 쪽의 취소에 걸려 기록을 못 남기면 「보낸 적 없다」로 남는다.
            // 푸시 흐름이 마지막 저장에서 내린 것과 같은 판단이다.
            await WriteMailLogAsync(recipients, title, body, sentBy,
                success: true, failureReason: null);

            _logger.LogInformation(
                "메일 곁가지 {Count}통. event={Event} 메일꺼둠={Off}",
                recipients.Count, request.Message.EventCode ?? request.Message.Category,
                loginIds.Count - wanted.Count);

            return recipients.Count;
        }
        catch (Exception ex)
        {
            // **푸시까지 깨뜨리지 않는다**(머리말). 못 보낸 것도 남긴다 —
            // 안 남기면 「그 시각에 아무 일도 없었다」로 보이고, 그것이
            // 「보낸 적 없다」와 구분되지 않는다.
            _logger.LogError(ex, "메일 곁가지를 보내지 못했습니다. event={Event} to={To}",
                request.Message.EventCode ?? request.Message.Category, string.Join(",", recipients));

            await WriteMailLogAsync(recipients, title, body, sentBy,
                success: false, failureReason: "메일 서버가 받지 않음");

            return 0;
        }
    }

    /// <summary>
    /// 메일 곁가지의 자국을 표에 적는다. <b>무슨 일이 있어도 던지지 않는다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 기록을 적다 터진 것이 <b>푸시 발송을 통째로 깨뜨리는</b> 것은 값이 맞지
    /// 않는다 — 특히 실패 갈래에서 여기가 또 던지면, 메일을 못 보낸 것을
    /// 삼키려고 세운 <c>catch</c> 를 그대로 빠져나간다.
    /// </para>
    /// <para>
    /// <b>취소표를 넘기지 않는다.</b> 이 자리에 왔다는 것은 메일을 보내려는
    /// 시도가 이미 끝났다는 뜻이라, 부르는 쪽이 끊었다고 기록까지 없애면
    /// 「그 시각에 아무 일도 없었다」가 되고 그것이 「보낸 적 없다」와
    /// 구분되지 않는다(푸시 흐름의 마지막 저장과 같은 판단이다).
    /// </para>
    /// </remarks>
    private async Task WriteMailLogAsync(
        List<string> recipients, string title, string body, string? sentBy,
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

    /// <summary>
    /// 푸시 한 통을 <b>메일 본문(평문)</b>으로 편다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 푸시는 제목 한 줄과 본문 두어 줄이 전부라, 그대로 옮기면 메일함에서
    /// <b>무엇을 하라는 것인지</b>가 빠진다 — 푸시는 누르면 그 화면이 열리지만
    /// 메일은 그 길이 없다. 그래서 <c>Url</c> 을 <b>눌러서 갈 수 있는 주소</b>로
    /// 펴서 끝에 붙인다.
    /// </para>
    /// <para>
    /// <b>앞머리(<c>Portal:BaseUrl</c>)가 비면 링크 줄을 통째로 뺀다.</b>
    /// <c>/admin/...</c> 한 조각이나 <c>localhost</c> 주소가 적힌 메일은
    /// 아무도 못 연다 — 쪽지 전환 메일이 같은 자리에서 내린 판단이다.
    /// </para>
    /// </remarks>
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

        // 본문도 링크도 없는 알림이 있다(제목만 쓰는 것들). 빈 메일을 보내느니
        // 제목을 한 번 더 적는다 — 틀이 제목을 큰 글씨로 올리지만, 평문 갈래로
        // 읽는 사람에게는 그것도 안 보인다.
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

            // **보낼 때 함께 보관한다.** 나중에 제목으로 갈래를 되짚으려 하면
            // 문구 한 번 다듬는 것으로 옛 줄과 새 줄이 갈라진다
            // (`PushSendLog.Category` 머리말).
            Category = PushCategories.Normalize(request.Message?.Category),
            IsSuccess = success,
            FailureReason = reason,
            SentBy = sentBy,

            // **띄운 얼굴도 함께 보관한다.** 이 값이 없으면 알림함은 그 얼굴을
            // 되짚을 길이 없다 — 부르는 쪽이 준 것은 아이디뿐이고 그것도 어디에
            // 안 남는다(`PushSendLog.Icon` 머리말).
            Icon = IconForLog(request.Message?.Icon),
        };

    /// <summary>
    /// 기록에 남길 아이콘 주소. <b>한 시간짜리 열쇠(<c>?t=</c>)는 떼어 낸다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 그 열쇠는 <b>로그인해 있지 않은 기기</b>가 사진 한 장을 열 수 있게 실어
    /// 보내는 것이라 60분이면 시효가 끝난다(<c>AvatarIconTokenFactory</c>).
    /// 알림창은 그 안에 뜨고 마니 상관이 없지만, <b>기록은 몇 달을 남는다.</b>
    /// </para>
    /// <para>
    /// 떼지 않으면 <b>알림함의 얼굴이 한 시간 뒤에 조용히 그림자로 바뀐다.</b>
    /// 셸의 중계가 열쇠를 <c>Bearer</c> 로 그대로 올려 보내는데(<c>FileDownload</c>
    /// 의 <c>HandleAvatarAsync</c>), 그러면 <b>지금 사람의 신원 대신 그 열쇠가
    /// 쓰인다</b> — 시효가 지난 열쇠는 401 이고, 중계는 그림자로 물러선다.
    /// 로그인해서 보고 있어도 그렇다. 실제로 확인했다.
    /// </para>
    /// <para>
    /// 떼고 나면 그 주소는 <b>지금 보는 사람의 신원</b>으로 열린다. 알림함을
    /// 보는 사람은 언제나 로그인해 있으므로 그것으로 충분하다.
    /// </para>
    /// <para>
    /// <b>우리가 만든 얼굴 주소일 때만 손댄다.</b> 부르는 쪽이 <c>Icon</c> 에
    /// 직접 적어 넣은 주소에는 뜻이 있는 질의가 달려 있을 수 있다.
    /// </para>
    /// </remarks>
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
