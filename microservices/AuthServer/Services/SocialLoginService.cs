using System.Security.Cryptography;
using System.Text.Json;
using AuthServer.Data;
using AuthServer.DTOs;
using AuthServer.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using JSini.Shared.DTOs;

namespace AuthServer.Services;

/// <summary>공급자가 준 「이 사람은 누구인가」. 주인을 찾는 열쇠는 ProviderUserId 뿐이다.</summary>
public sealed record SocialIdentity(
    string Provider, string ProviderUserId, string? Email, bool EmailVerified, string? Name,
    string? PictureUrl = null);

/// <summary>소셜로 들어왔을 때 갈리는 세 갈래.</summary>
public enum SocialLoginStatus
{
    /// <summary>쓸 수 있는 계정을 찾았다. 여느 로그인과 똑같이 마무리하면 된다.</summary>
    Ok,

    /// <summary>처음 온 사람이라 <b>가입 신청</b>을 만들었다. 승인 전까지 로그인은 안 된다.</summary>
    Pending,

    /// <summary>실패. 사용자에게 보여 줄 문구가 함께 온다.</summary>
    Failed,
}

/// <summary>소셜 로그인 한 판의 결과.</summary>
/// <param name="Status">어느 갈래인가.</param>
/// <param name="Account"><see cref="SocialLoginStatus.Ok"/> 일 때 그 계정.</param>
/// <param name="Pending"><see cref="SocialLoginStatus.Pending"/> 일 때 무엇을 만들었는지.</param>
/// <param name="Message">실패했을 때 보여 줄 문구.</param>
public sealed record SocialLoginOutcome(
    SocialLoginStatus Status,
    Account? Account = null,
    SocialSignupPendingDto? Pending = null,
    string? Message = null);

/// <summary>구글·네이버·카카오 인가 코드를 받아 신원만 밝힌다. 비밀 열쇠는 서버끼리만 오간다.</summary>
/// PKCE 를 쓰지 않는다 — state 를 셸의 HttpOnly 쿠키와 대조해 위조를 막는다(네이버는 PKCE 를 받지도 않는다).
public sealed class SocialLoginService(
    AppDbContext db,
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<SocialLoginOptions> options,
    AccountMailClient mail,
    SignupNotifyClient push,
    SocialAvatarImporter avatars,
    MailPhotoToken mailPhotos,
    IConfiguration configuration,
    ILogger<SocialLoginService> logger)
{
    /// <summary>계정 안내 메일에 찍히는 보낸이 표시. 가입 신청과 같은 자리라 같은 값을 쓴다.</summary>
    private const string Sender = "AUTH_SIGNUP";

    private SocialLoginOptions Options => options.CurrentValue;

    // ── 설정 읽기 ───────────────────────────────────────────────

    /// <summary>지금 쓸 수 있는 공급자 목록. 아이디가 채워진 것만 나온다.</summary>
    /// 설정이 빈 공급자의 단추를 그리면 눌러 본 사람이 고장으로 신고한다.
    public IReadOnlyList<SocialProviderDto> UsableProviders()
    {
        if (!Options.Enabled)
        {
            return [];
        }

        return
        [
            .. Options.Providers
                .Where(p => p.Value.IsUsable)
                .Select(p => new SocialProviderDto(
                    p.Key.ToLowerInvariant(),
                    string.IsNullOrWhiteSpace(p.Value.DisplayName) ? p.Key : p.Value.DisplayName))
                .OrderBy(p => p.DisplayName, StringComparer.Ordinal)
        ];
    }

    /// <summary>설정에서 공급자 하나를 찾는다. 없거나 못 쓰면 <c>null</c>.</summary>
    public SocialProviderOptions? Find(string? provider)
    {
        if (!Options.Enabled || string.IsNullOrWhiteSpace(provider))
        {
            return null;
        }

        var found = Options.Providers.FirstOrDefault(p =>
            string.Equals(p.Key, provider, StringComparison.OrdinalIgnoreCase)).Value;

        return found is { IsUsable: true } ? found : null;
    }

    /// <summary>인가 화면 주소를 만든다. 설정이 없는 공급자면 null.</summary>
    /// redirectUri 는 셸이 정해 넘긴다 — 공급자 콘솔 등록값과 글자 하나까지 같아야 하고, AuthServer 는 포털의 바깥 주소를 모른다.
    public string? BuildAuthorizeUrl(string provider, string redirectUri, string state)
    {
        var settings = Find(provider);
        if (settings is null)
        {
            return null;
        }

        var query = new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["client_id"] = settings.ClientId,
            ["redirect_uri"] = redirectUri,
            ["state"] = state,
        };

        if (!string.IsNullOrWhiteSpace(settings.Scope))
        {
            query["scope"] = settings.Scope;
        }

        foreach (var (key, value) in settings.ExtraAuthorizeParams)
        {
            query[key] = value;
        }

        return Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(
            settings.AuthorizeUrl, query);
    }

    // ── 공급자에게 물어보기 ─────────────────────────────────────

    /// <summary>인가 코드를 토큰으로 바꾸고 프로필을 읽는다.</summary>
    /// 실패 이유는 사용자에게 뭉뚱그리고, 공급자의 오류 문구를 그대로 띄우지 않는다(우리 설정 상태가 밖에 드러난다).
    public async Task<(SocialIdentity? Identity, string? Error)> ResolveIdentityAsync(
        SocialLoginRequestDto request, CancellationToken ct = default)
    {
        var settings = Find(request.Provider);
        if (settings is null)
        {
            return (null, "지금은 쓸 수 없는 로그인 방식입니다.");
        }

        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.RedirectUri))
        {
            return (null, "인증 정보가 모자랍니다. 다시 시도해 주세요.");
        }

        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(15);

        string accessToken;
        try
        {
            accessToken = await ExchangeCodeAsync(client, settings, request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogError(ex, "{Provider} 토큰 교환에 실패했다.", request.Provider);
            return (null, "소셜 로그인 서버에 연결하지 못했습니다. 잠시 뒤 다시 시도해 주세요.");
        }
        catch (InvalidOperationException ex)
        {
            // 공급자가 「코드가 못 쓴다」고 답한 경우다. 뒤로 가기로 같은 코드를
            // 두 번 쓰면 흔히 여기 온다 — 고장이 아니라 다시 누르면 되는 상황이다.
            logger.LogWarning(ex, "{Provider} 가 인가 코드를 거절했다.", request.Provider);
            return (null, "인증이 만료되었습니다. 다시 시도해 주세요.");
        }

        JsonElement profile;
        try
        {
            profile = await ReadProfileAsync(client, settings, accessToken, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogError(ex, "{Provider} 프로필을 읽지 못했다.", request.Provider);
            return (null, "소셜 계정 정보를 읽지 못했습니다. 잠시 뒤 다시 시도해 주세요.");
        }

        var providerUserId = ReadPath(profile, settings.IdPath);
        if (string.IsNullOrWhiteSpace(providerUserId))
        {
            // 응답 모양이 바뀌었거나 설정의 IdPath 가 틀렸다. 이 값이 없으면
            // 주인을 찾을 열쇠가 없으므로 **절대 그냥 통과시키지 않는다.**
            logger.LogError(
                "{Provider} 프로필에서 사용자 번호({Path})를 찾지 못했다. 설정의 IdPath 를 확인한다.",
                request.Provider, settings.IdPath);

            return (null, "소셜 계정 정보를 읽지 못했습니다. 관리자에게 문의해 주세요.");
        }

        var email = Normalize(ReadPath(profile, settings.EmailPath));
        var verified = string.Equals(
            ReadPath(profile, settings.EmailVerifiedPath), "true", StringComparison.OrdinalIgnoreCase);

        return (new SocialIdentity(
            request.Provider.ToLowerInvariant(),
            providerUserId,
            email,
            verified,
            Normalize(ReadPath(profile, settings.NamePath)),
            IsDefaultPicture(profile, settings) ? null : SafePicture(ReadPath(profile, settings.PicturePath))), null);
    }

    /// <summary>공급자 사진이 기본 그림인가. 알려 주는 칸을 먼저 보고, 없으면 알려진 기본 그림 주소와 견준다.</summary>
    private static bool IsDefaultPicture(JsonElement profile, SocialProviderOptions settings)
    {
        if (string.Equals(ReadPath(profile, settings.PictureIsDefaultPath), "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var url = ReadPath(profile, settings.PicturePath);
        return url is { Length: > 0 }
            && settings.DefaultPictureUrls.Any(d =>
                !string.IsNullOrWhiteSpace(d) && url.Contains(d.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>사진 주소는 https 절대 주소만 남긴다. 관리자 화면 img 에 그대로 걸린다.</summary>
    /// 카카오는 secure_resource=true 를 빼면 http 주소를 주는데, https 로 올려 쓴다(섞인 콘텐츠 차단 회피).
    private static string? SafePicture(string? url)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (uri.Scheme == Uri.UriSchemeHttp)
        {
            uri = new UriBuilder(uri) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri;
        }

        return uri.Scheme == Uri.UriSchemeHttps ? uri.AbsoluteUri : null;
    }

    /// <summary>인가 코드 → access token. 공급자 셋이 모두 폼 POST 를 받는다.</summary>
    private static async Task<string> ExchangeCodeAsync(
        HttpClient client,
        SocialProviderOptions settings,
        SocialLoginRequestDto request,
        CancellationToken ct)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = settings.ClientId,
            ["code"] = request.Code,
            ["redirect_uri"] = request.RedirectUri,
        };

        // 카카오는 콘솔에서 켜지 않으면 비밀 열쇠 자체가 없다. 빈 값을 실어
        // 보내면 그쪽이 「형식이 틀렸다」로 거절하므로 있을 때만 붙인다.
        if (!string.IsNullOrWhiteSpace(settings.ClientSecret))
        {
            form["client_secret"] = settings.ClientSecret;
        }

        // 네이버는 토큰 교환에서도 state 를 본다. 나머지 공급자는 무시한다.
        if (!string.IsNullOrWhiteSpace(request.State))
        {
            form["state"] = request.State;
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, settings.TokenUrl)
        {
            Content = new FormUrlEncodedContent(form),
        };

        // 구글·네이버·카카오는 JSON 을 주지만 깃허브는 이 헤더가 없으면 폼 문자열을 준다. 공급자를 더 붙일 때를 위해 항상 보낸다.
        message.Headers.Accept.Add(
            new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await client.SendAsync(message, ct);

        var body = await response.Content.ReadAsStringAsync(ct);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        if (root.TryGetProperty("access_token", out var token)
            && token.GetString() is { Length: > 0 } value)
        {
            return value;
        }

        // 네이버는 200 에 오류를 담아 보내므로 상태 코드만 보면 토큰 없이 다음 단계로 간다.
        throw new InvalidOperationException(
            $"토큰 응답에 access_token 이 없다 ({(int)response.StatusCode}): {Clip(body)}");
    }

    /// <summary>access token 으로 프로필을 읽는다.</summary>
    private static async Task<JsonElement> ReadProfileAsync(
        HttpClient client, SocialProviderOptions settings, string accessToken, CancellationToken ct)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, settings.UserInfoUrl);
        message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", accessToken);

        using var response = await client.SendAsync(message, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        using var document = JsonDocument.Parse(body);

        // using 으로 문서를 닫으므로 값을 복사해서 내보낸다. 안 하면 읽는
        // 쪽에서 「이미 닫힌 문서」 예외가 난다.
        return document.RootElement.Clone();
    }

    // ── 계정 찾기 · 만들기 ──────────────────────────────────────

    /// <summary>신원으로 계정을 찾고, 없으면 승인 대기(PENDING) 계정을 만든다.</summary>
    /// 소셜만 즉시 통과시키면 승인제를 우회하게 되므로 바로 로그인시키지 않는다. Auth:Social:AutoApprove 를 켜면 ACTIVE 로 만든다.
    public async Task<SocialLoginOutcome> SignInAsync(
        SocialLoginRequestDto request, CancellationToken ct = default)
    {
        var (identity, error) = await ResolveIdentityAsync(request, ct);
        if (identity is null)
        {
            return new SocialLoginOutcome(SocialLoginStatus.Failed, Message: error);
        }

        // 1) 이미 연결된 계정이 있나. **사용자 번호로만 찾는다**(엔티티 머리말).
        var link = await db.AccountSocialLogins
            .FirstOrDefaultAsync(
                l => l.Provider == identity.Provider && l.ProviderUserId == identity.ProviderUserId,
                ct);

        if (link is not null)
        {
            var owner = await db.Accounts.FirstOrDefaultAsync(a => a.Id == link.AccountId, ct);

            if (owner is null)
            {
                // 외래 키가 막고 있어 정상 경로로는 생기지 않는다. 생겼다면
                // 조용히 새 계정을 만들어 주면 안 된다 — 그 순간 남의 연결이
                // 새 계정으로 옮겨 붙는 셈이 된다.
                logger.LogError("소셜 연결의 주인 계정이 없다: {AccountId}", link.AccountId);
                return new SocialLoginOutcome(
                    SocialLoginStatus.Failed, Message: "계정 정보를 찾지 못했습니다. 관리자에게 문의해 주세요.");
            }

            // 공급자 쪽에서 이름·이메일을 바꿨을 수 있다. 화면에 띄우는 값이라
            // 들어올 때마다 맞춰 둔다.
            link.Email = identity.Email ?? link.Email;
            link.DisplayName = identity.Name ?? link.DisplayName;
            link.LastLoginAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            return new SocialLoginOutcome(SocialLoginStatus.Ok, owner);
        }

        // 2) 확인된 이메일로 기존 계정에 붙일 것인가. **기본은 안 붙인다.**
        if (Options.LinkByVerifiedEmail
            && identity.EmailVerified
            && !string.IsNullOrWhiteSpace(identity.Email))
        {
            var matched = await FindByEmailAsync(identity.Email, ct);

            if (matched is not null)
            {
                db.AccountSocialLogins.Add(NewLink(matched.Id, identity, DateTime.UtcNow));
                await db.SaveChangesAsync(ct);

                logger.LogInformation(
                    "확인된 이메일로 소셜 계정을 이어 붙였다: {LoginId} ← {Provider}",
                    matched.UserId, identity.Provider);

                return new SocialLoginOutcome(SocialLoginStatus.Ok, matched);
            }
        }

        // 3) 처음 온 사람이다. 가입 신청을 만든다.
        return await CreateSignupAsync(identity, ct);
    }

    /// <summary>결합된 소셜 계정으로 로그인했을 때 기존 계정과 결합 계정으로 결합 알림(앱알림 및 이메일)을 모두 발송합니다.</summary>
    public async Task NotifyMergedLoginAsync(
        Account account, string provider, string? clientIp, CancellationToken ct = default)
    {
        try
        {
            // 이 계정에 결합(SocialMerge) 이력이 있는지 조회
            var mergeDetails = await db.AccountProfileDetails
                .Where(d => d.AccountId == account.Id && (d.DetailType == "SocialMerge" || d.DetailType == "MergedSocial"))
                .ToListAsync(ct);

            // 해당 공급자로 결합된 이력이 없으면 발송 대상 아님
            var matchedMerge = mergeDetails.FirstOrDefault(d => string.Equals(d.Content, provider, StringComparison.OrdinalIgnoreCase))
                ?? mergeDetails.FirstOrDefault();

            if (matchedMerge is null)
            {
                return;
            }

            var providerName = DisplayNameOf(provider);
            var userName = account.UserName ?? account.RealName ?? account.UserId;
            var loginTimeStr = DateTime.UtcNow.AddHours(9).ToString("yyyy-MM-dd HH:mm:ss");

            // 결합 계정 정보 파싱 (Label = pendingLoginId, Remark = pendingEmail|pendingPhone|...)
            var mergedLoginId = matchedMerge.Label;
            string? mergedEmail = null;
            if (!string.IsNullOrWhiteSpace(matchedMerge.Remark))
            {
                var parts = matchedMerge.Remark.Split('|');
                if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]))
                {
                    mergedEmail = parts[0];
                }
            }

            // 소셜 연결 테이블에서 소셜 계정 이메일도 확인
            if (string.IsNullOrWhiteSpace(mergedEmail))
            {
                var socialLogin = await db.AccountSocialLogins
                    .FirstOrDefaultAsync(l => l.AccountId == account.Id && l.Provider == provider, ct);
                mergedEmail = socialLogin?.Email;
            }

            // 기존 계정 이메일
            var primaryEmail = await db.AccountProfileDetails
                .Where(d => d.AccountId == account.Id && d.DetailType == "Email")
                .OrderByDescending(d => d.IsPrimary)
                .Select(d => d.Content)
                .FirstOrDefaultAsync(ct);

            // 1) 앱 알림 발송: 기존 계정(account.UserId) 및 결합 계정(mergedLoginId) 모두 발송
            var pushTargets = new List<string> { account.UserId };
            if (!string.IsNullOrWhiteSpace(mergedLoginId) && !pushTargets.Contains(mergedLoginId, StringComparer.OrdinalIgnoreCase))
            {
                pushTargets.Add(mergedLoginId);
            }

            var pushTitle = "[보안알림] 결합된 소셜 계정으로 로그인되었습니다";
            var pushBody = $"'{userName}' 계정에 결합된 {providerName} 소셜 계정으로 로그인되었습니다. (IP: {clientIp ?? "알 수 없음"})";

            await push.NotifyOwnersAsync(
                pushTargets,
                pushTitle,
                pushBody,
                "/admin/system/account",
                account.UserId,
                "AUTH_SOCIAL_LOGIN",
                ct);

            // 2) 이메일 알림 발송: 기존 계정 및 결합 계정 이메일 모두 발송
            var emailTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(primaryEmail)) emailTargets.Add(primaryEmail);
            if (!string.IsNullOrWhiteSpace(mergedEmail)) emailTargets.Add(mergedEmail);

            var mailSubject = $"[JSini 포털] 결합된 {providerName} 소셜 계정으로 로그인이 감지되었습니다";
            var mailBody = $"""
                <p>안녕하세요, <b>{Escape(userName)}</b> ({Escape(account.UserId)}) 님.</p>
                <p>회원님의 포털 계정에 결합된 <b>{Escape(providerName)}</b> 소셜 계정으로 새로운 로그인이 발생하였습니다.</p>
                <table style="border-collapse: collapse; margin: 16px 0; font-size: 14px;">
                    <tr><td style="padding: 6px 12px; background: #f5f5f5; border: 1px solid #ddd;"><b>로그인 일시</b></td><td style="padding: 6px 12px; border: 1px solid #ddd;">{loginTimeStr} (KST)</td></tr>
                    <tr><td style="padding: 6px 12px; background: #f5f5f5; border: 1px solid #ddd;"><b>소셜 제공자</b></td><td style="padding: 6px 12px; border: 1px solid #ddd;">{Escape(providerName)}</td></tr>
                    <tr><td style="padding: 6px 12px; background: #f5f5f5; border: 1px solid #ddd;"><b>접속 IP</b></td><td style="padding: 6px 12px; border: 1px solid #ddd;">{Escape(clientIp ?? "–")}</td></tr>
                    <tr><td style="padding: 6px 12px; background: #f5f5f5; border: 1px solid #ddd;"><b>포털 계정</b></td><td style="padding: 6px 12px; border: 1px solid #ddd;">{Escape(account.UserId)}</td></tr>
                </table>
                <p>본인이 로그인한 것이 아니라면 즉시 비밀번호를 변경하시고 관리자에게 문의해 주십시오.</p>
                """;

            foreach (var email in emailTargets)
            {
                await mail.SendAsync(
                    email,
                    mailSubject,
                    mailBody,
                    "AUTH_SOCIAL_LOGIN",
                    NotificationEvents.AccountMail,
                    ct);
            }

            logger.LogInformation(
                "결합 소셜 로그인 알림 발송 완료: {UserId} ({Provider}), 푸시대상={PushTargets}, 메일대상={EmailTargets}",
                account.UserId, provider, string.Join(",", pushTargets), string.Join(",", emailTargets));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "결합 소셜 로그인 알림 발송 중 예외 발생: {UserId} ({Provider})", account.UserId, provider);
        }
    }

    /// <summary>로그인한 사람이 자기 계정에 소셜 계정을 덧붙인다. 이 길이 없으면 두 번째 계정이 생긴다.</summary>
    public async Task<(bool Ok, string? Error, SocialLinkDto? Link)> LinkAsync(
        Account account, SocialLoginRequestDto request, CancellationToken ct = default)
    {
        var (identity, error) = await ResolveIdentityAsync(request, ct);
        if (identity is null)
        {
            return (false, error, null);
        }

        var existing = await db.AccountSocialLogins
            .FirstOrDefaultAsync(
                l => l.Provider == identity.Provider && l.ProviderUserId == identity.ProviderUserId,
                ct);

        if (existing is not null)
        {
            // 이미 자기 것이면 성공으로 본다 — 두 번 눌렀을 뿐이다.
            if (existing.AccountId == account.Id)
            {
                existing.Email = identity.Email ?? existing.Email;
                existing.DisplayName = identity.Name ?? existing.DisplayName;
                await db.SaveChangesAsync(ct);

                return (true, null, ToDto(existing, DisplayNameOf(existing.Provider)));
            }

            // 남의 계정에 붙어 있다. **누구에게 붙어 있는지는 알려 주지 않는다** —
            // 그것을 알려 주면 이 경로가 「이 소셜 계정을 쓰는 사람이 있나」를
            // 확인해 주는 도구가 된다.
            return (false, "이미 다른 계정에 연결된 소셜 계정입니다.", null);
        }

        var link = NewLink(account.Id, identity, lastLoginAt: null);
        db.AccountSocialLogins.Add(link);

        await AddLinkedPictureAsync(account, identity, ct);

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "소셜 계정을 연결했다: {LoginId} ← {Provider}", account.UserId, identity.Provider);

        return (true, null, ToDto(link, DisplayNameOf(link.Provider)));
    }

    /// <summary>연결한 소셜 계정의 프로필 사진을 사진 목록에 한 장 더한다. 대표는 비어 있을 때만 채운다.</summary>
    /// 이미 정한 대표 사진을 연결 한 번으로 갈아 끼우지 않으며, 사진이 실패해도 연결은 그대로 된다.
    private async Task AddLinkedPictureAsync(Account account, SocialIdentity identity, CancellationToken ct)
    {
        if (identity.PictureUrl is not { Length: > 0 } picture)
        {
            return;
        }

        var settings = Find(identity.Provider);
        var createdBy = account.UserId;

        var imported = await avatars.ImportAsync(
            picture, settings?.PictureHosts ?? [], identity.Provider, createdBy,
            groupId: account.AvatarGroupId, ct: ct);

        if (imported is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(account.AvatarGroupId))
        {
            account.AvatarGroupId = imported.GroupId;
        }

        var avatar = await db.AccountProfileDetails
            .Where(d => d.AccountId == account.Id && d.DetailType == "Avatar")
            .OrderByDescending(d => d.IsPrimary)
            .FirstOrDefaultAsync(ct);

        // 대표 사진이 이미 있으면 거기서 멈춘다 — 사진 관리에 한 장 늘었을 뿐이다.
        if (avatar is not null && !string.IsNullOrWhiteSpace(avatar.Content))
        {
            logger.LogInformation(
                "{Provider} 프로필 사진을 사진 관리에 더했다: {LoginId} (대표는 그대로)",
                identity.Provider, account.UserId);
            return;
        }

        // 대표가 비어 있다. FileServer 가 이미 대표로 정했으면(그룹에 대표가 없었다)
        // 그대로 쓰고, 아니면 이 사진을 대표로 지정한다 — 둘이 어긋나면 사진 관리의
        // 별표와 헤더의 얼굴이 서로 다른 사진을 가리킨다.
        if (!imported.IsRepresentative)
        {
            await avatars.SetRepresentativeAsync(imported.GroupId, imported.FileId, createdBy, ct);
        }

        if (avatar is null)
        {
            db.AccountProfileDetails.Add(Detail(account.Id, "Avatar", imported.DownloadUrl));
        }
        else
        {
            avatar.Content = imported.DownloadUrl;
            avatar.IsPrimary = true;
        }

        logger.LogInformation(
            "{Provider} 프로필 사진을 대표 사진으로 걸었다: {LoginId}", identity.Provider, account.UserId);
    }

    /// <summary>설정에 적힌 공급자 이름. 설정에서 지워졌으면 열쇠를 그대로 돌려준다.</summary>
    public string DisplayNameOf(string provider)
    {
        var found = Options.Providers.FirstOrDefault(p =>
            string.Equals(p.Key, provider, StringComparison.OrdinalIgnoreCase)).Value;

        return string.IsNullOrWhiteSpace(found?.DisplayName) ? provider : found.DisplayName;
    }

    /// <summary>「연결된 소셜 계정」 한 줄로 바꾼다.</summary>
    public static SocialLinkDto ToDto(AccountSocialLogin link, string displayName) => new()
    {
        Id = link.Id,
        Provider = link.Provider,
        DisplayName = displayName,
        AccountName = link.DisplayName,
        Email = link.Email,
        LinkedAt = link.CreatedAt,
        LastLoginAt = link.LastLoginAt,
    };

    // ── 가입 신청 만들기 ────────────────────────────────────────

    /// <summary>소셜 신원으로 승인 대기 계정을 만든다. 가입 신청 화면과 같은 표·같은 칸에 써서 함께 뜬다.</summary>
    private async Task<SocialLoginOutcome> CreateSignupAsync(
        SocialIdentity identity, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var providerName = DisplayNameOf(identity.Provider);
        var userName = identity.Name ?? $"{providerName} 사용자";
        var loginId = await IssueLoginIdAsync(identity, ct);

        var account = new Account
        {
            UserId = loginId,
            UserName = userName,
            RealName = userName,

            // 본인은 이 비밀번호를 모른다. 무작위로 채워 두고, 비밀번호 찾기로 정하게 한다.
            Password = PasswordHasher.Hash(
                RandomNumberGenerator.GetString(
                    "abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789", 32)),

            // 90일 정책의 기준. 안 채우면 이 계정만 만료 시계가 없다.
            PasswordChangedAt = now,
        };

        db.Accounts.Add(account);

        var approved = Options.AutoApprove;

        db.AccountProfileDetails.Add(Detail(
            account.Id,
            SignupService.StatusDetail,
            approved ? SignupService.StatusActive : SignupService.StatusPending));

        if (!string.IsNullOrWhiteSpace(identity.Email))
        {
            db.AccountProfileDetails.Add(Detail(account.Id, "Email", identity.Email));
        }

        // 승인하는 사람이 「이 사람이 어디로 들어왔는지」를 볼 수 있어야 한다.
        // 가입 신청 화면이 이 칸을 그대로 띄운다.
        db.AccountProfileDetails.Add(Detail(
            account.Id,
            SignupService.NoteDetail,
            $"{providerName} 계정으로 신청 ({identity.Email ?? "이메일 없음"})"));

        db.AccountProfileDetails.Add(Detail(account.Id, "HomePath", Options.HomePath));

        // 우리 서버로 옮겨 둔 사진. 메일은 공급자 주소가 아니라 이것을 싣는다.
        string? storedPictureId = null;

        // 가입 신청 목록·알림이 얼굴을 그리는 데 쓴다(`SignupService.PictureDetail`).
        if (identity.PictureUrl is { Length: > 0 } picture)
        {
            db.AccountProfileDetails.Add(Detail(account.Id, SignupService.PictureDetail, picture));

            // 같은 사진을 우리 FileServer 로 옮겨 **계정 대표 사진**으로 건다.
            // 승인 뒤 헤더·조직도·알림 아이콘이 이 얼굴로 뜬다. 못 옮겨도
            // 신청은 그대로 간다 — 사진은 나중에 [내 정보] 에서 올리면 된다.
            var settings = Find(identity.Provider);
            var imported = await avatars.ImportAsync(
                picture, settings?.PictureHosts ?? [], identity.Provider, Sender, ct: ct);

            if (imported is not null)
            {
                account.AvatarGroupId = imported.GroupId;
                db.AccountProfileDetails.Add(Detail(account.Id, "Avatar", imported.DownloadUrl));
                storedPictureId = imported.FileId;
            }
        }

        db.AccountSocialLogins.Add(NewLink(account.Id, identity, lastLoginAt: null));

        // 자동 승인이면 관리자의 승인 단추를 거치지 않으므로 기본 역할을
        // 여기서 붙인다. 안 그러면 이 길로 들어온 사람만 메뉴가 비어 있다.
        if (approved)
        {
            await SignupDefaultRoles.AddAsync(db, configuration, logger, account.Id, Sender, ct);
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "소셜 가입 신청이 들어왔다: {LoginId} ({Provider}, 자동승인 {Auto})",
            loginId, identity.Provider, approved);

        // 알림은 곁들이는 일이다. 못 보내도 신청은 이미 저장되었다.
        // 메일과 푸시를 **둘 다** 보낸다 — 메일은 자리를 비운 관리자에게 남는
        // 기록이고, 푸시는 그 자리에서 곧바로 승인하게 하는 길이다.
        var notifyRole = configuration["Auth:Signup:NotifyRole"] ?? "SYSTEM_ADMINISTRATOR";

        var mailed = await mail.SendToRoleAsync(
            notifyRole,
            "[JSini 포털] 소셜 계정으로 가입 신청이 들어왔습니다",
            SignupMailBody(loginId, userName, identity, providerName, now, storedPictureId),
            Sender, NotificationEvents.Signup, ct);

        if (!mailed)
        {
            logger.LogError("소셜 가입 신청 알림 메일을 보내지 못했다: {LoginId}", loginId);
        }

        await push.NotifyRoleAsync(
            notifyRole,
            account.Id,
            "새 가입 신청",
            $"{userName} 님이 {providerName} 계정으로 가입을 신청했습니다. 눌러서 승인하세요.",
            loginId,
            Sender,
            ct);

        if (approved)
        {
            // 자동 승인이면 방금 만든 계정이 곧바로 쓸 수 있는 상태다.
            return new SocialLoginOutcome(SocialLoginStatus.Ok, account);
        }

        return new SocialLoginOutcome(
            SocialLoginStatus.Pending,
            Pending: new SocialSignupPendingDto(loginId, userName, identity.Email));
    }

    /// <summary>새 계정의 로그인 아이디를 짓는다. 비어 있는 것이 나올 때까지 센다.</summary>
    /// 이메일 앞부분을 먼저 쓰고, 겹치면 공급자_번호 앞 10자리로 물러선다. 공급자 번호를 통째로 쓰지 않는다 — 사람이 받아 적을 수 없다.
    private async Task<string> IssueLoginIdAsync(SocialIdentity identity, CancellationToken ct)
    {
        // 이메일 앞부분 → 앞부분_공급자 → 앞부분2 … → 공급자_번호앞10자리 순으로 써 본다.
        // 네이버 번호는 43자 무작위라 사람이 외우거나 받아 적을 수 없다.
        var candidates = new List<string>();
        string? local = null;

        if (identity.Email is { Length: > 0 } email)
        {
            var part = Sanitize(email.Split('@')[0]);
            if (part.Length >= 3)
            {
                local = part;
                candidates.Add(part);
                candidates.Add(Sanitize($"{part}_{identity.Provider}"));
            }
        }

        var shortId = identity.ProviderUserId.Length > 10
            ? identity.ProviderUserId[..10]
            : identity.ProviderUserId;
        var fallback = Sanitize($"{identity.Provider}_{shortId}");
        var providerSeed = fallback.Length >= 3 ? fallback : $"{identity.Provider}_user";

        foreach (var candidate in candidates)
        {
            if (!await db.Accounts.AnyAsync(a => a.UserId == candidate, ct))
            {
                return candidate;
            }
        }

        if (local is not null)
        {
            for (var i = 2; i < 100; i++)
            {
                var next = $"{local}{i}";
                if (!await db.Accounts.AnyAsync(a => a.UserId == next, ct))
                {
                    return next;
                }
            }
        }

        if (!await db.Accounts.AnyAsync(a => a.UserId == providerSeed, ct))
        {
            return providerSeed;
        }

        var seed = providerSeed;
        for (var i = 2; i < 1000; i++)
        {
            var next = $"{seed}{i}";
            if (!await db.Accounts.AnyAsync(a => a.UserId == next, ct))
            {
                return next;
            }
        }

        // 여기까지 오는 것은 사실상 불가능하지만, 겹치는 아이디를 저장해
        // 예외로 끝내는 것보다는 알아볼 수 없는 아이디가 낫다.
        return $"{identity.Provider}_{Guid.NewGuid():N}"[..32];
    }

    /// <summary>계정 하나를 이메일로 찾는다. <c>Email</c> 프로필 칸이 정본이다.</summary>
    private async Task<Account?> FindByEmailAsync(string email, CancellationToken ct)
    {
        var ids = await db.AccountProfileDetails
            .Where(d => d.DetailType == "Email" && d.Content != null
                && d.Content.ToLower() == email.ToLower())
            .Select(d => d.AccountId)
            .Distinct()
            .ToListAsync(ct);

        // **둘 이상이면 붙이지 않는다.** 같은 이메일을 쓰는 계정이 여럿이면
        // 어느 쪽 주인인지 고를 근거가 없고, 잘못 고르면 남의 계정에 붙는다.
        if (ids.Count != 1)
        {
            if (ids.Count > 1)
            {
                logger.LogWarning("같은 이메일을 쓰는 계정이 여럿이라 자동 연결을 건너뛴다: {Count}건", ids.Count);
            }

            return null;
        }

        return await db.Accounts.FirstOrDefaultAsync(a => a.Id == ids[0], ct);
    }

    // ── 잔손 ───────────────────────────────────────────────────

    private static AccountSocialLogin NewLink(
        string accountId, SocialIdentity identity, DateTime? lastLoginAt) => new()
        {
            AccountId = accountId,
            Provider = identity.Provider,
            ProviderUserId = identity.ProviderUserId,
            Email = identity.Email,
            DisplayName = identity.Name,
            LastLoginAt = lastLoginAt,
        };

    private static AccountProfileDetail Detail(string accountId, string type, string content) => new()
    {
        AccountId = accountId,
        DetailType = type,
        Content = content,
        IsPrimary = true,
    };

    /// <summary>점으로 내려가며 JSON 값을 문자열로 꺼낸다.</summary>
    /// 카카오의 사용자 번호는 숫자라 문자열만 읽으면 빈 값이 나온다.
    private static string? ReadPath(JsonElement root, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var current = root;
        foreach (var part in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.ValueKind != JsonValueKind.Object
                || !current.TryGetProperty(part, out var next))
            {
                return null;
            }

            current = next;
        }

        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString(),
            JsonValueKind.Number => current.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null,
        };
    }

    /// <summary>로그인 아이디로 쓸 수 있는 글자만 남긴다.</summary>
    private static string Sanitize(string value)
    {
        var kept = new string([.. value.ToLowerInvariant()
            .Where(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.')]);

        return kept.Length <= 40 ? kept : kept[..40];
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Escape(string? value) =>
        System.Net.WebUtility.HtmlEncode(value ?? string.Empty);

    /// <summary>관리자 알림 메일에 싣는 포털 주소. Portal:PublicUrl 을 먼저 쓴다.</summary>
    /// 개발 장비에서 처리된 신청도 운영 관리자에게 가므로 운영 주소가 먼저다.
    private string PublicPortalUrl =>
        (configuration["Portal:PublicUrl"] is { Length: > 0 } pub
            ? pub
            : configuration["Portal:BaseUrl"] ?? "http://localhost:5557").TrimEnd('/');

    /// <summary>관리자에게 가는 소셜 가입 신청 메일 본문.</summary>
    /// HTML 메일은 회사 틀을 안 씌우므로 표와 인라인 스타일로 짓고, 사진은 MailPhotoToken 을 붙인 포털 중계 주소로 건다.
    private string SignupMailBody(
        string loginId, string userName, SocialIdentity identity, string providerName, DateTime requestedAt,
        string? storedPictureId)
    {
        var portal = PublicPortalUrl;
        var approveUrl = $"{portal}/admin/system/signup";
        var initial = string.IsNullOrWhiteSpace(userName) ? "?" : userName.Trim()[..1];
        // 한국 시각 +9. 시간대 이름으로 풀면 시간대 자료가 없는 컨테이너에서 예외가 난다.
        var at = DateTime.SpecifyKind(requestedAt, DateTimeKind.Utc).AddHours(9);

        var (chipBack, chipFore, chipBorder) = identity.Provider switch
        {
            "kakao" => ("#fee500", "#191600", "#fee500"),
            "naver" => ("#03c75a", "#ffffff", "#03c75a"),
            "google" => ("#ffffff", "#1f1f1f", "#dadce0"),
            _ => ("#f3f4f6", "#374151", "#e5e7eb"),
        };

        var photoKey = storedPictureId is null ? null : mailPhotos.Create(storedPictureId);
        var photoUrl = photoKey is null
            ? null
            : $"{portal}/files/avatar/{Uri.EscapeDataString(storedPictureId!)}?o=1&t={Uri.EscapeDataString(photoKey)}";

        var face = photoUrl is { Length: > 0 } picture
            ? $"""<img src="{Escape(picture)}" width="64" height="64" alt="{Escape(initial)}" style="display:block;width:64px;height:64px;border-radius:32px;object-fit:cover;background:#e5e7eb;border:0;" />"""
            : $"""<div style="width:64px;height:64px;border-radius:32px;background:#e5e7eb;color:#6b7280;font-size:26px;font-weight:700;line-height:64px;text-align:center;">{Escape(initial)}</div>""";

        return $"""
            <div style="margin:0;padding:24px 12px;background:#f5f5f7;font-family:'Apple SD Gothic Neo','Malgun Gothic',sans-serif;color:#1c1c1e;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:520px;margin:0 auto;background:#ffffff;border:1px solid #e8e8ed;">
                <tr>
                  <td style="padding:28px 28px 8px;">
                    <div style="font-size:12px;letter-spacing:.08em;color:#6e6e73;">JSini 포털 · 가입 신청</div>
                    <div style="margin-top:6px;font-size:20px;font-weight:700;">새 가입 신청이 들어왔습니다</div>
                  </td>
                </tr>
                <tr>
                  <td style="padding:16px 28px;">
                    <table role="presentation" cellpadding="0" cellspacing="0">
                      <tr>
                        <td style="vertical-align:middle;">{face}</td>
                        <td style="vertical-align:middle;padding-left:16px;">
                          <div style="font-size:17px;font-weight:700;">{Escape(userName)}</div>
                          <div style="margin-top:6px;">
                            <span style="display:inline-block;padding:2px 10px;border-radius:999px;border:1px solid {chipBorder};background:{chipBack};color:{chipFore};font-size:12px;font-weight:700;">{Escape(providerName)} 계정으로 신청</span>
                          </div>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
                <tr>
                  <td style="padding:0 28px;">
                    <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="font-size:14px;border-top:1px solid #e8e8ed;">
                      <tr><td style="padding:10px 0;color:#6e6e73;width:90px;">아이디</td><td style="padding:10px 0;">{Escape(loginId)}</td></tr>
                      <tr><td style="padding:10px 0;color:#6e6e73;border-top:1px solid #f0f0f3;">이메일</td><td style="padding:10px 0;border-top:1px solid #f0f0f3;">{Escape(identity.Email ?? "-")}</td></tr>
                      <tr><td style="padding:10px 0;color:#6e6e73;border-top:1px solid #f0f0f3;">신청 시각</td><td style="padding:10px 0;border-top:1px solid #f0f0f3;">{at:yyyy-MM-dd HH:mm}</td></tr>
                    </table>
                  </td>
                </tr>
                <tr>
                  <td style="padding:20px 28px 28px;">
                    <a href="{Escape(approveUrl)}" style="display:inline-block;padding:12px 22px;background:#0a0a0a;color:#ffffff;font-size:14px;font-weight:700;text-decoration:none;">가입 신청 확인하기</a>
                    <div style="margin-top:14px;font-size:12px;line-height:1.6;color:#6e6e73;">
                      포털의 [계정 관리 → 가입 신청] 에서 승인하거나 거절할 수 있습니다.
                      승인 전에는 이 사람이 로그인할 수 없습니다.
                    </div>
                  </td>
                </tr>
              </table>
            </div>
            """;
    }

    /// <summary>로그에 남길 본문을 잘라 둔다. 토큰 응답이 통째로 남는 것을 막는다.</summary>
    private static string Clip(string value) =>
        value.Length <= 200 ? value : value[..200];
}
