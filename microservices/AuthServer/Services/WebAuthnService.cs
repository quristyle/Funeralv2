using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AuthServer.Data;
using AuthServer.DTOs;
using AuthServer.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AuthServer.Services;

/// <summary>검증 결과. 실패하면 사람에게 보여 줄 까닭이 함께 온다.</summary>
/// <param name="Ok">통과했는가</param>
/// <param name="Message">실패했을 때의 까닭</param>
/// <param name="Credential">로그인 검증이 찾아낸 패스키</param>
public readonly record struct WebAuthnResult(
    bool Ok,
    string? Message = null,
    AccountWebAuthnCredential? Credential = null);

/// <summary>
/// 패스키(WebAuthn) — 도전값을 내고, 돌아온 서명을 검증한다.
///
/// <para>
/// <b>왜 라이브러리를 안 쓰나.</b> FIDO2 라이브러리가 짊어지는 것 대부분은
/// <b>증명서(attestation) 검증</b>이다 — 「이 인증기가 진짜 유비키인가」를
/// 제조사 인증서 사슬로 따지는 일이고, 그러려면 MDS(메타데이터 서비스)를
/// 주기적으로 받아 와야 한다. 우리는 그것을 요구하지 않는다
/// (<c>attestation: "none"</c>). 사내 포털에서 필요한 것은 「이 계정의 그
/// 기기가 맞는가」 하나고, 그 답은 <b>공개 키 서명 검증</b>이 준다.
/// </para>
///
/// <para>
/// 증명서를 빼면 남는 일이 세 가지뿐이라 직접 한다.
/// </para>
///
/// <list type="number">
///   <item><b>문맥(clientDataJSON)</b> — 무슨 동작인지 · 우리가 낸 도전값인지 ·
///         우리 오리진에서 왔는지.</item>
///   <item><b>인증기 자료(authenticatorData)</b> — 우리 RP ID 의 해시인지 ·
///         사용자가 실제로 기기 앞에 있었는지(UP) · 본인 확인을 했는지(UV).</item>
///   <item><b>서명</b> — <c>authData ‖ SHA256(clientDataJSON)</c> 에 대한 서명.</item>
/// </list>
///
/// <para>
/// <b>CBOR 을 풀지 않는다.</b> 등록 응답의 <c>attestationObject</c> 는 CBOR 이지만,
/// 브라우저가 <c>getPublicKey()</c>·<c>getAuthenticatorData()</c> 로 그 안의
/// 필요한 조각을 이미 풀어서 준다(WebAuthn Level 3). 그래서 서버는 SPKI DER 과
/// 원시 바이트만 받는다 — 서명 검증에서 가장 틀리기 쉬운 자리가 통째로 사라진다.
/// 그 두 메서드가 없는 옛 브라우저는 패스키를 등록할 수 없고,
/// <b>화면이 그렇게 말한다</b>(조용히 실패하지 않는다).
/// </para>
/// </summary>
public sealed class WebAuthnService(
    AppDbContext db,
    IMemoryCache cache,
    IConfiguration config,
    ILogger<WebAuthnService> logger)
{
    /// <summary>도전값이 살아 있는 시간. 사람이 지문을 대는 데 드는 시간을 넉넉히 덮는다.</summary>
    private static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(5);

    /// <summary>캐시 열쇠 접두사. 다른 용도의 열쇠와 섞이지 않게 한다.</summary>
    private const string CachePrefix = "webauthn:challenge:";

    /// <summary>ECDSA P-256 + SHA-256. 휴대폰 지문·얼굴이 거의 다 이것이다.</summary>
    public const int AlgEs256 = -7;

    /// <summary>RSASSA-PKCS1 + SHA-256. 윈도우 Hello 가 이것을 쓰는 경우가 있다.</summary>
    public const int AlgRs256 = -257;

    /// <summary>
    /// 이 서버가 자기를 뭐라고 부르는가 (RP ID). <b>브라우저의 호스트와 같거나
    /// 그 상위 도메인이어야 한다</b> — 어긋나면 브라우저가 등록 자체를 거절한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AuthServer 는 게이트웨이 뒤에 있고 포털은 그 앞의 BFF 라, <b>요청만 보고는
    /// 브라우저의 주소를 알 수 없다.</b> 그래서 설정에서 읽는다.
    /// </para>
    ///
    /// <para>
    /// <b>기본값을 <c>Portal:BaseUrl</c> 에서 꺼낸다.</b> 그 값은 이미 환경마다
    /// 맞춰져 있고(운영 compose 가 <c>https://portal.jsini.co.kr</c> 를 넣는다)
    /// 비밀번호 재설정 메일의 링크도 그것을 쓴다. 같은 뜻의 값을 두 군데 적어
    /// 두면 도메인을 옮기는 날 한쪽만 고치게 되고, 그때 증상은 <b>「등록해 둔
    /// 지문이 갑자기 안 먹는다」</b>다 — 원인을 찾기가 아주 어렵다.
    /// </para>
    ///
    /// <para>
    /// 포털과 다른 도메인을 써야 하면 <c>WebAuthn:RelyingPartyId</c> 로 덮는다.
    /// </para>
    /// </remarks>
    public string RelyingPartyId =>
        Nonempty(config["WebAuthn:RelyingPartyId"])
        ?? PortalUri?.Host
        ?? "localhost";

    /// <summary>기기의 열쇠 목록에 보일 이름.</summary>
    public string RelyingPartyName => config["WebAuthn:RelyingPartyName"] ?? "JSini 업무 포털";

    /// <summary>
    /// 받아 주는 오리진. <c>clientDataJSON.origin</c> 이 이 중 하나여야 한다.
    /// 비워 두면 포털 주소 하나로 본다.
    /// </summary>
    private string[] AllowedOrigins
    {
        get
        {
            var configured = config.GetSection("WebAuthn:AllowedOrigins").Get<string[]>();
            if (configured is { Length: > 0 })
            {
                return configured;
            }

            // 오리진은 **스킴·호스트·포트까지** 같아야 한다(경로는 뺀다).
            return PortalUri is { } uri
                ? [uri.GetLeftPart(UriPartial.Authority)]
                : [$"https://{RelyingPartyId}"];
        }
    }

    /// <summary>포털(브라우저가 보는 주소). 못 읽으면 <c>null</c>.</summary>
    private Uri? PortalUri =>
        Uri.TryCreate(config["Portal:BaseUrl"], UriKind.Absolute, out var uri) ? uri : null;

    private static string? Nonempty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>
    /// 본인 확인(UV)을 <b>반드시</b> 받아야 하는가.
    ///
    /// <para>
    /// 켜면 지문·얼굴·PIN 을 거치지 않은 로그인을 거절한다. 이 기능을 넣은
    /// 까닭이 「지문으로 들어가기」라 기본이 <c>true</c> 다 — 끄면 기기를
    /// 집어 든 사람이 그대로 들어온다.
    /// </para>
    /// </summary>
    private bool RequireUserVerification => config.GetValue("WebAuthn:RequireUserVerification", true);

    // ── 도전값 ───────────────────────────────────────────────────

    /// <summary>
    /// 새 패스키를 만들 때 브라우저에 넘길 설정.
    /// </summary>
    /// <param name="account">등록하는 사람</param>
    /// <param name="cancellationToken">취소 토큰</param>
    public async Task<WebAuthnOptionsDto> CreateRegisterOptionsAsync(
        Account account, CancellationToken cancellationToken = default)
    {
        var challenge = RandomNumberGenerator.GetBytes(32);
        var sessionId = Remember(challenge, account.Id);

        // 이미 등록한 기기로 또 만들지 않게 막는다. 없으면 같은 기기에 패스키가
        // 두 벌 생기고, 목록에 똑같이 생긴 줄이 둘 남아 어느 것을 지워야 할지
        // 알 수 없게 된다.
        var existing = await db.Set<AccountWebAuthnCredential>()
            .Where(c => c.AccountId == account.Id && !c.IsDeleted)
            .Select(c => c.CredentialId)
            .ToListAsync(cancellationToken);

        return new WebAuthnOptionsDto
        {
            SessionId = sessionId,
            PublicKey = new Dictionary<string, object?>
            {
                ["challenge"] = Base64Url.Encode(challenge),
                ["rp"] = new Dictionary<string, object?>
                {
                    ["id"] = RelyingPartyId,
                    ["name"] = RelyingPartyName,
                },
                ["user"] = new Dictionary<string, object?>
                {
                    // 계정 아이디를 손잡이로 쓴다. 아이디를 치지 않고 들어올 때
                    // 인증기가 이 값을 돌려주고, 그것이 「누구인가」의 답이 된다.
                    ["id"] = Base64Url.Encode(Encoding.UTF8.GetBytes(account.Id)),
                    ["name"] = account.UserId,
                    ["displayName"] = account.RealName ?? account.UserName ?? account.UserId,
                },
                // 우리가 검증할 수 있는 것만 적는다. EdDSA(-8) 는 .NET 이
                // 기본으로 못 읽으므로 빼 둔다 — 적어 두면 그것으로 만든 뒤
                // 로그인에서만 실패한다.
                ["pubKeyCredParams"] = new object[]
                {
                    new Dictionary<string, object?> { ["type"] = "public-key", ["alg"] = AlgEs256 },
                    new Dictionary<string, object?> { ["type"] = "public-key", ["alg"] = AlgRs256 },
                },
                ["timeout"] = (int)ChallengeLifetime.TotalMilliseconds,
                // 증명서를 요구하지 않는다 — 머리말 참고.
                ["attestation"] = "none",
                ["authenticatorSelection"] = new Dictionary<string, object?>
                {
                    // 기기에 붙박인 인증기를 권한다(지문·얼굴·Hello). 보안 열쇠를
                    // 막지는 않는다 — 막으면 그것을 쓰는 사람이 등록할 길이 없다.
                    ["authenticatorAttachment"] = "platform",
                    // 아이디를 치지 않고 들어오려면 기기가 열쇠를 들고 있어야
                    // 한다. required 로 두지 않는 것은 저장 공간이 모자란
                    // 보안 열쇠에서 등록이 통째로 거절되기 때문이다.
                    ["residentKey"] = "preferred",
                    ["requireResidentKey"] = false,
                    ["userVerification"] = RequireUserVerification ? "required" : "preferred",
                },
                ["excludeCredentials"] = existing
                    .Select(id => new Dictionary<string, object?>
                    {
                        ["type"] = "public-key",
                        ["id"] = id,
                    })
                    .ToArray(),
            },
        };
    }

    /// <summary>
    /// 패스키로 들어올 때 브라우저에 넘길 설정.
    /// </summary>
    /// <param name="username">
    /// 아이디를 적어 넣은 경우. 그러면 그 사람의 패스키만 후보로 준다
    /// (<c>allowCredentials</c>). 비면 <b>기기가 스스로 고르게</b> 둔다 —
    /// 아이디를 치지 않고 지문만으로 들어오는 길이 그것이다.
    /// </param>
    /// <param name="cancellationToken">취소 토큰</param>
    public async Task<WebAuthnOptionsDto> CreateLoginOptionsAsync(
        string? username, CancellationToken cancellationToken = default)
    {
        var challenge = RandomNumberGenerator.GetBytes(32);

        List<string> allowed = [];
        if (!string.IsNullOrWhiteSpace(username))
        {
            allowed = await (
                from c in db.Set<AccountWebAuthnCredential>()
                join a in db.Accounts on c.AccountId equals a.Id
                where a.UserId == username && !c.IsDeleted
                select c.CredentialId).ToListAsync(cancellationToken);
        }

        // **아이디가 없는 계정인지 알려 주지 않는다.** 후보가 비었다고 거절하면
        // 아이디 하나로 「그 계정이 있는지」를 캐낼 수 있다. 빈 목록도 그대로
        // 내보내고, 브라우저가 맞는 열쇠를 못 찾는 것으로 끝낸다.
        var sessionId = Remember(challenge, accountId: null);

        return new WebAuthnOptionsDto
        {
            SessionId = sessionId,
            PublicKey = new Dictionary<string, object?>
            {
                ["challenge"] = Base64Url.Encode(challenge),
                ["rpId"] = RelyingPartyId,
                ["timeout"] = (int)ChallengeLifetime.TotalMilliseconds,
                ["userVerification"] = RequireUserVerification ? "required" : "preferred",
                ["allowCredentials"] = allowed
                    .Select(id => new Dictionary<string, object?>
                    {
                        ["type"] = "public-key",
                        ["id"] = id,
                    })
                    .ToArray(),
            },
        };
    }

    // ── 검증 ─────────────────────────────────────────────────────

    /// <summary>
    /// 등록 응답을 검증하고 패스키를 저장한다.
    /// </summary>
    public async Task<WebAuthnResult> RegisterAsync(
        Account account, WebAuthnRegisterDto request, CancellationToken cancellationToken = default)
    {
        if (Recall(request.SessionId) is not { } pending)
        {
            return new WebAuthnResult(false, "등록 시간이 지났습니다. 다시 시도해 주세요.");
        }

        // 도전값을 낸 사람과 지금 저장하려는 사람이 같아야 한다. 안 보면 남이
        // 받아 둔 도전값으로 **내 계정에 자기 기기를 붙일** 수 있다.
        if (pending.AccountId is not null && pending.AccountId != account.Id)
        {
            return new WebAuthnResult(false, "등록 요청이 계정과 맞지 않습니다.");
        }

        if (VerifyClientData(request.ClientDataJson, "webauthn.create", pending.Challenge) is { Ok: false } clientData)
        {
            return clientData;
        }

        if (!Base64Url.TryDecode(request.AuthenticatorData, out var authData))
        {
            return new WebAuthnResult(false, "기기가 보낸 값을 해석하지 못했습니다.");
        }

        if (VerifyAuthenticatorData(authData) is { Ok: false } authCheck)
        {
            return authCheck;
        }

        if (request.Algorithm is not (AlgEs256 or AlgRs256))
        {
            return new WebAuthnResult(false, "이 기기가 쓰는 서명 방식은 아직 받지 못합니다.");
        }

        // 읽히지 않는 공개 키를 저장하면 **등록은 성공하고 로그인만 안 된다.**
        // 여기서 한 번 열어 보고 거절한다.
        if (!TryImportPublicKey(request.PublicKey, request.Algorithm, out var reason))
        {
            logger.LogWarning("패스키 공개 키를 읽지 못했다: {Reason}", reason);
            return new WebAuthnResult(false, "기기가 보낸 공개 키를 읽지 못했습니다.");
        }

        var credentialId = request.CredentialId;
        if (string.IsNullOrWhiteSpace(credentialId))
        {
            return new WebAuthnResult(false, "기기가 자격 증명 아이디를 보내지 않았습니다.");
        }

        // 같은 자격 증명이 이미 있으면 덮지 않고 되살린다. 지웠다가 같은 기기로
        // 다시 등록하는 흐름이 흔한데, 그때 고유 색인에 걸려 실패하면
        // 사용자에게는 「지웠는데 다시 등록이 안 된다」로 보인다.
        var existing = await db.Set<AccountWebAuthnCredential>()
            .FirstOrDefaultAsync(c => c.CredentialId == credentialId, cancellationToken);

        if (existing is not null && existing.AccountId != account.Id)
        {
            return new WebAuthnResult(false, "이미 다른 계정에 등록된 기기입니다.");
        }

        var label = string.IsNullOrWhiteSpace(request.Label)
            ? DefaultLabel(request.Attachment)
            : request.Label.Trim()[..Math.Min(request.Label.Trim().Length, 60)];

        var entity = existing ?? new AccountWebAuthnCredential
        {
            AccountId = account.Id,
            CredentialId = credentialId,
        };

        entity.PublicKey = request.PublicKey;
        entity.Algorithm = request.Algorithm;
        entity.SignCount = ReadSignCount(authData);
        entity.Label = label;
        entity.Attachment = request.Attachment;
        entity.IsDeleted = false;

        if (existing is null)
        {
            db.Add(entity);
        }

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("패스키를 등록했다: {UserId} ({Label})", account.UserId, label);
        return new WebAuthnResult(true, Credential: entity);
    }

    /// <summary>
    /// <b>이미 로그인한 사람</b>이 자기 기기를 다시 대는 자리에 넘길 설정
    /// (잠금화면).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 로그인용(<see cref="CreateLoginOptionsAsync"/>)과 두 군데가 다르다.
    /// </para>
    ///
    /// <list type="number">
    ///   <item><b>후보를 숨기지 않는다.</b> 누구인지 이미 아는 자리라
    ///   「그 계정이 있는지」가 샐 걱정이 없다. 오히려 후보를 줘야
    ///   윈도우 Hello 처럼 열쇠를 스스로 들고 있지 않는 기기에서도 열린다.</item>
    ///   <item><b>도전값에 주인을 적어 둔다.</b> 그래야 받아 둔 도전값을
    ///   남에게 건네 <b>남의 지문으로 내 잠금을 푸는</b> 길이 막힌다
    ///   (검증에서 <see cref="VerifyAssertionAsync"/> 가 대조한다).</item>
    /// </list>
    /// </remarks>
    /// <param name="account">지금 로그인해 있는 사람</param>
    /// <param name="cancellationToken">취소 토큰</param>
    public async Task<WebAuthnOptionsDto> CreateVerifyOptionsAsync(
        Account account, CancellationToken cancellationToken = default)
    {
        var challenge = RandomNumberGenerator.GetBytes(32);
        var sessionId = Remember(challenge, account.Id);

        var allowed = await db.Set<AccountWebAuthnCredential>()
            .Where(c => c.AccountId == account.Id && !c.IsDeleted)
            .Select(c => c.CredentialId)
            .ToListAsync(cancellationToken);

        return new WebAuthnOptionsDto
        {
            SessionId = sessionId,
            PublicKey = new Dictionary<string, object?>
            {
                ["challenge"] = Base64Url.Encode(challenge),
                ["rpId"] = RelyingPartyId,
                ["timeout"] = (int)ChallengeLifetime.TotalMilliseconds,
                ["userVerification"] = RequireUserVerification ? "required" : "preferred",
                ["allowCredentials"] = allowed
                    .Select(id => new Dictionary<string, object?>
                    {
                        ["type"] = "public-key",
                        ["id"] = id,
                    })
                    .ToArray(),
            },
        };
    }

    /// <summary>
    /// 로그인 응답을 검증한다. 통과하면 그 패스키를 돌려준다.
    /// </summary>
    public Task<WebAuthnResult> VerifyLoginAsync(
        WebAuthnLoginDto request, CancellationToken cancellationToken = default)
        => VerifyAssertionAsync(request, expectedAccountId: null, cancellationToken);

    /// <summary>
    /// 기기 서명을 검증한다. 로그인과 <b>잠금 해제</b>가 같은 코드를 쓴다.
    /// </summary>
    /// <param name="request">브라우저의 <c>navigator.credentials.get()</c> 결과</param>
    /// <param name="expectedAccountId">
    /// <b>누구의 기기여야 하는가.</b> 로그인은 <c>null</c> 이다 — 누구인지를
    /// 이 검증이 <i>알아내는</i> 자리라 미리 정해 둘 수 없다.
    ///
    /// <para>
    /// 잠금 해제는 여기에 지금 로그인한 사람을 넣는다. 안 넣으면 <b>옆 사람이
    /// 자기 지문으로 내 잠금을 풀 수 있다</b> — 서명 자체는 멀쩡히 통과하기
    /// 때문에 어디에도 실패로 남지 않는다.
    /// </para>
    /// </param>
    /// <param name="cancellationToken">취소 토큰</param>
    public async Task<WebAuthnResult> VerifyAssertionAsync(
        WebAuthnLoginDto request, string? expectedAccountId,
        CancellationToken cancellationToken = default)
    {
        if (Recall(request.SessionId) is not { } pending)
        {
            return new WebAuthnResult(false, expectedAccountId is null
                ? "로그인 시간이 지났습니다. 다시 시도해 주세요."
                : "확인 시간이 지났습니다. 다시 시도해 주세요.");
        }

        // 도전값에 주인이 적혀 있으면(잠금 해제) 그 사람에게 낸 것이어야 한다.
        // 등록(`RegisterAsync`)이 같은 자리를 같은 이유로 본다.
        if (pending.AccountId is not null
            && expectedAccountId is not null
            && pending.AccountId != expectedAccountId)
        {
            return new WebAuthnResult(false, "확인 요청이 계정과 맞지 않습니다.");
        }

        var credential = await db.Set<AccountWebAuthnCredential>()
            .FirstOrDefaultAsync(
                c => c.CredentialId == request.CredentialId && !c.IsDeleted, cancellationToken);

        if (credential is null)
        {
            // 「없는 기기」와 「서명이 틀렸다」를 구분해 주지 않는다 —
            // 구분해 주면 등록된 기기를 골라내는 데 쓰인다.
            logger.LogInformation("등록되지 않은 패스키로 로그인 시도가 있었다.");
            return new WebAuthnResult(false, "등록된 기기가 아닙니다.");
        }

        // **잠금 해제의 알맹이가 이 세 줄이다.** 서명은 맞지만 주인이 다른
        // 열쇠를 여기서 거른다 — 없으면 옆 사람의 지문으로도 내 화면이 열린다.
        if (expectedAccountId is not null && credential.AccountId != expectedAccountId)
        {
            logger.LogWarning("남의 패스키로 잠금을 풀려는 시도가 있었다: credential={Id}", credential.Id);
            return new WebAuthnResult(false, "이 계정에 등록된 기기가 아닙니다.");
        }

        // 인증기가 계정 손잡이를 줬으면 그것도 맞춰 본다. 자격 증명 아이디만
        // 보면 충분하지만, 둘이 어긋나는 응답은 정상이 아니라 거절한다.
        if (!string.IsNullOrWhiteSpace(request.UserHandle))
        {
            if (!Base64Url.TryDecode(request.UserHandle, out var rawHandle))
            {
                return new WebAuthnResult(false, "기기가 보낸 값을 해석하지 못했습니다.");
            }

            var handle = Encoding.UTF8.GetString(rawHandle);
            if (handle != credential.AccountId)
            {
                logger.LogWarning("패스키의 계정 손잡이가 저장된 주인과 다르다.");
                return new WebAuthnResult(false, "등록된 기기가 아닙니다.");
            }
        }

        if (VerifyClientData(request.ClientDataJson, "webauthn.get", pending.Challenge) is { Ok: false } clientData)
        {
            return clientData;
        }

        if (!Base64Url.TryDecode(request.AuthenticatorData, out var authData)
            || !Base64Url.TryDecode(request.ClientDataJson, out var rawClientData)
            || !Base64Url.TryDecode(request.Signature, out var signature))
        {
            return new WebAuthnResult(false, "기기가 보낸 값을 해석하지 못했습니다.");
        }

        if (VerifyAuthenticatorData(authData) is { Ok: false } authCheck)
        {
            return authCheck;
        }

        // 서명 대상은 **인증기 자료 뒤에 문맥의 해시를 이어 붙인 것**이다.
        // 순서를 바꾸면 늘 실패하고, 실패 이유가 어디에도 안 남는다.
        var clientDataHash = SHA256.HashData(rawClientData);
        var signed = new byte[authData.Length + clientDataHash.Length];
        authData.CopyTo(signed, 0);
        clientDataHash.CopyTo(signed, authData.Length);

        if (!VerifySignature(credential, signed, signature))
        {
            logger.LogWarning("패스키 서명이 맞지 않는다: credential={Id}", credential.Id);
            return new WebAuthnResult(false, "기기 인증에 실패했습니다.");
        }

        // 복제 감지. 둘 다 0 이면 횟수를 안 세는 인증기다(애플 패스키) —
        // 그때 막으면 아이폰에서 두 번째 로그인부터 안 된다.
        var presented = ReadSignCount(authData);
        if (presented != 0 && credential.SignCount != 0 && presented <= credential.SignCount)
        {
            logger.LogWarning(
                "패스키 서명 횟수가 늘지 않았다(복제 의심): credential={Id}, 저장={Stored}, 받음={Presented}",
                credential.Id, credential.SignCount, presented);
            return new WebAuthnResult(false, "기기 인증에 실패했습니다.");
        }

        credential.SignCount = Math.Max(credential.SignCount, presented);
        credential.LastUsedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return new WebAuthnResult(true, Credential: credential);
    }

    // ── 조각 검증 ────────────────────────────────────────────────

    /// <summary>
    /// 브라우저가 서명 대상에 넣은 문맥을 본다 — 무슨 동작인지 · 우리가 낸
    /// 도전값인지 · 우리 오리진에서 왔는지.
    /// </summary>
    private WebAuthnResult VerifyClientData(string clientDataJson, string expectedType, byte[] challenge)
    {
        if (!Base64Url.TryDecode(clientDataJson, out var rawClientData))
        {
            return new WebAuthnResult(false, "기기가 보낸 값을 해석하지 못했습니다.");
        }

        JsonElement root;
        try
        {
            root = JsonDocument.Parse(rawClientData).RootElement;
        }
        catch (JsonException)
        {
            return new WebAuthnResult(false, "기기가 보낸 값을 해석하지 못했습니다.");
        }

        if (root.TryGetProperty("type", out var type) is not true || type.GetString() != expectedType)
        {
            return new WebAuthnResult(false, "요청 종류가 맞지 않습니다.");
        }

        // **고정 시간 비교를 쓴다.** 도전값 대조는 비밀값 대조라, 길이별로
        // 빨리 끝나는 비교를 쓰면 한 바이트씩 맞혀 나갈 여지가 생긴다.
        if (root.TryGetProperty("challenge", out var got) is not true
            || got.ValueKind != JsonValueKind.String
            || !Base64Url.TryDecode(got.GetString(), out var presented)
            || !CryptographicOperations.FixedTimeEquals(presented, challenge))
        {
            return new WebAuthnResult(false, "요청이 만료되었거나 위조되었습니다.");
        }

        var origin = root.TryGetProperty("origin", out var originValue) ? originValue.GetString() : null;
        if (origin is null || !AllowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
        {
            logger.LogWarning("패스키 요청의 오리진이 허용 목록에 없다: {Origin}", origin);
            return new WebAuthnResult(false, "허용되지 않은 주소에서 온 요청입니다.");
        }

        return new WebAuthnResult(true);
    }

    /// <summary>
    /// 인증기 자료의 앞머리를 본다 — RP ID 해시(32) · 깃발(1) · 서명 횟수(4).
    /// </summary>
    private WebAuthnResult VerifyAuthenticatorData(byte[] authData)
    {
        if (authData.Length < 37)
        {
            return new WebAuthnResult(false, "기기가 보낸 값이 온전하지 않습니다.");
        }

        var expected = SHA256.HashData(Encoding.UTF8.GetBytes(RelyingPartyId));
        if (!CryptographicOperations.FixedTimeEquals(authData.AsSpan(0, 32), expected))
        {
            // 설정의 RP ID 와 브라우저가 쓴 것이 다르다. 운영에서 이 로그가
            // 보이면 그것이 곧 「WebAuthn:RelyingPartyId 를 고쳐라」다.
            logger.LogWarning(
                "패스키의 RP ID 해시가 다르다. WebAuthn:RelyingPartyId({Rp}) 가 실제 접속 도메인과 같은지 확인한다.",
                RelyingPartyId);
            return new WebAuthnResult(false, "이 주소에서는 등록된 기기를 쓸 수 없습니다.");
        }

        var flags = authData[32];

        // UP(0x01) — 사람이 기기 앞에 있었다. 이것이 없으면 자동화된 요청이다.
        if ((flags & 0x01) == 0)
        {
            return new WebAuthnResult(false, "기기에서 확인 동작이 없었습니다.");
        }

        // UV(0x04) — 지문·얼굴·PIN 으로 본인을 확인했다.
        if (RequireUserVerification && (flags & 0x04) == 0)
        {
            return new WebAuthnResult(false, "지문·얼굴·PIN 확인이 필요합니다.");
        }

        return new WebAuthnResult(true);
    }

    /// <summary>인증기 자료 33~36 바이트의 서명 횟수 (빅엔디언).</summary>
    private static long ReadSignCount(byte[] authData) =>
        authData.Length < 37
            ? 0
            : ((long)authData[33] << 24) | ((long)authData[34] << 16)
              | ((long)authData[35] << 8) | authData[36];

    /// <summary>저장해 둔 공개 키로 서명을 검증한다.</summary>
    private bool VerifySignature(AccountWebAuthnCredential credential, byte[] signed, byte[] signature)
    {
        try
        {
            var spki = Convert.FromBase64String(credential.PublicKey);

            if (credential.Algorithm == AlgEs256)
            {
                using var ecdsa = ECDsa.Create();
                ecdsa.ImportSubjectPublicKeyInfo(spki, out _);

                // WebAuthn 의 ECDSA 서명은 **DER 묶음**이다. 형식을 적어 주지
                // 않으면 .NET 이 고정 길이(r‖s)로 읽어 늘 실패한다.
                return ecdsa.VerifyData(
                    signed, signature, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
            }

            if (credential.Algorithm == AlgRs256)
            {
                using var rsa = RSA.Create();
                rsa.ImportSubjectPublicKeyInfo(spki, out _);
                return rsa.VerifyData(signed, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }

            return false;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or ArgumentException)
        {
            logger.LogWarning(ex, "패스키 서명 검증 중 열쇠를 읽지 못했다: credential={Id}", credential.Id);
            return false;
        }
    }

    /// <summary>등록할 때 공개 키가 실제로 읽히는지 미리 열어 본다.</summary>
    private static bool TryImportPublicKey(string base64Spki, int algorithm, out string? reason)
    {
        try
        {
            var spki = Convert.FromBase64String(base64Spki);

            if (algorithm == AlgEs256)
            {
                using var ecdsa = ECDsa.Create();
                ecdsa.ImportSubjectPublicKeyInfo(spki, out _);
            }
            else
            {
                using var rsa = RSA.Create();
                rsa.ImportSubjectPublicKeyInfo(spki, out _);
            }

            reason = null;
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or ArgumentException)
        {
            reason = ex.Message;
            return false;
        }
    }

    /// <summary>이름을 안 붙였을 때 대신 쓸 말.</summary>
    private static string DefaultLabel(string? attachment) =>
        attachment == "cross-platform" ? "보안 열쇠" : "이 기기";

    // ── 도전값 보관 ──────────────────────────────────────────────

    /// <summary>낸 도전값을 <see cref="ChallengeLifetime"/> 동안 적어 둔다.</summary>
    private string Remember(byte[] challenge, string? accountId)
    {
        var sessionId = Base64Url.Encode(RandomNumberGenerator.GetBytes(24));

        cache.Set(
            CachePrefix + sessionId,
            new PendingChallenge(challenge, accountId),
            ChallengeLifetime);

        return sessionId;
    }

    /// <summary>
    /// 적어 둔 도전값을 꺼내고 <b>그 자리에서 지운다.</b> 남겨 두면 같은
    /// 도전값으로 두 번 로그인할 수 있다(재생 공격).
    /// </summary>
    private PendingChallenge? Recall(string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return null;
        }

        var key = CachePrefix + sessionId;
        if (!cache.TryGetValue<PendingChallenge>(key, out var pending))
        {
            return null;
        }

        cache.Remove(key);
        return pending;
    }

    /// <summary>낸 도전값 한 벌. 등록이면 누구에게 냈는지도 함께 적는다.</summary>
    private sealed record PendingChallenge(byte[] Challenge, string? AccountId);
}

/// <summary>
/// base64url — 받침(<c>=</c>)이 없고 <c>+/</c> 대신 <c>-_</c> 를 쓴다.
/// WebAuthn 의 이진값이 전부 이 모양으로 오간다.
/// </summary>
/// <remarks>
/// .NET 9 의 <c>Base64Url</c> 과 이름이 같지만 이쪽은 우리 것이다 —
/// 프레임워크 것으로 갈아 끼울 때 이 클래스만 지우면 된다.
/// </remarks>
public static class Base64Url
{
    /// <summary>바이트를 base64url 로.</summary>
    public static string Encode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>
    /// base64url 을 바이트로. <b>평범한 base64 도 받는다</b> — 브라우저마다
    /// 어느 쪽으로 보내는지가 갈려서, 가려 받으면 한 브라우저에서만 실패한다.
    /// </summary>
    public static byte[] Decode(string value)
    {
        var text = value.Replace('-', '+').Replace('_', '/');

        return Convert.FromBase64String(text.PadRight(
            text.Length + ((4 - (text.Length % 4)) % 4), '='));
    }

    /// <summary>
    /// 못 읽으면 예외 대신 거짓을 준다.
    ///
    /// <para>
    /// <b>이 값들은 전부 바깥에서 온다.</b> 로그인 경로는 익명이라 아무나
    /// <c>{"signature":"!!!"}</c> 를 보낼 수 있고, 그때 <see cref="Decode"/> 가
    /// 던지면 500 이 나간다 — 검증이 「거절」이 아니라 「고장」으로 끝나는 것이라
    /// 오류 로그가 남의 장난으로 가득 차고, 진짜 고장이 그 속에 묻힌다.
    /// <c>null</c> 도 여기서 함께 받는다(JSON 은 칸을 비워 보낼 수 있다).
    /// </para>
    /// </summary>
    public static bool TryDecode(string? value, out byte[] bytes)
    {
        if (string.IsNullOrEmpty(value))
        {
            bytes = [];
            return false;
        }

        try
        {
            bytes = Decode(value);
            return true;
        }
        catch (FormatException)
        {
            bytes = [];
            return false;
        }
    }
}
