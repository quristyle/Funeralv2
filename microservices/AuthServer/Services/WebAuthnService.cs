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

/// <summary>패스키(WebAuthn) 도전값을 내고 돌아온 서명을 검증한다. 증명서는 보지 않는다(<c>attestation: "none"</c>).
/// CBOR 은 풀지 않는다 — 브라우저가 getPublicKey() 로 이미 풀어 주므로, 그 메서드가 없는 옛 브라우저는 등록을 막는다.</summary>
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

    /// <summary>이 서버가 자기를 부르는 이름(RP ID). 브라우저의 호스트와 같거나 그 상위 도메인이어야 하며, 어긋나면 등록 자체가 거절된다.
    /// 기본값은 Portal:BaseUrl 에서 꺼낸다 — 도메인을 옮길 때 두 곳을 따로 고치면 등록한 지문이 갑자기 안 먹으므로 한 곳만 본다. 다른 도메인이면 WebAuthn:RelyingPartyId 로 덮는다.</summary>
    public string RelyingPartyId =>
        Nonempty(config["WebAuthn:RelyingPartyId"])
        ?? PortalUri?.Host
        ?? "localhost";

    /// <summary>기기의 열쇠 목록에 보일 이름.</summary>
    public string RelyingPartyName => config["WebAuthn:RelyingPartyName"] ?? "JSini 업무 포털";

    /// <summary>받아 주는 오리진. clientDataJSON.origin 이 이 중 하나여야 하며, 비우면 포털 주소 하나로 본다.</summary>
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

    /// <summary>본인 확인(UV)을 반드시 받아야 하는가. 기본은 true — 끄면 기기를 집어 든 사람이 그대로 들어온다.</summary>
    private bool RequireUserVerification => config.GetValue("WebAuthn:RequireUserVerification", true);


    /// <summary>새 패스키를 만들 때 브라우저에 넘길 설정.</summary>
    /// <param name="account">등록하는 사람</param>
    /// <param name="cancellationToken">취소 토큰</param>
    public async Task<WebAuthnOptionsDto> CreateRegisterOptionsAsync(
        Account account, CancellationToken cancellationToken = default)
    {
        var challenge = RandomNumberGenerator.GetBytes(32);
        var sessionId = Remember(challenge, account.Id);

        // 이미 등록한 기기는 또 만들지 않는다 — 같은 기기에 패스키가 두 벌 생기면 목록에서 어느 것을 지울지 알 수 없다.
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
                    // 계정 아이디를 손잡이로 쓴다 — 아이디를 치지 않고 들어올 때 인증기가 이 값을 돌려준다.
                    ["id"] = Base64Url.Encode(Encoding.UTF8.GetBytes(account.Id)),
                    ["name"] = account.UserId,
                    ["displayName"] = account.RealName ?? account.UserName ?? account.UserId,
                },
                // EdDSA(-8) 는 .NET 이 못 읽으므로 뺀다 — 넣으면 등록은 되고 로그인에서만 실패한다.
                ["pubKeyCredParams"] = new object[]
                {
                    new Dictionary<string, object?> { ["type"] = "public-key", ["alg"] = AlgEs256 },
                    new Dictionary<string, object?> { ["type"] = "public-key", ["alg"] = AlgRs256 },
                },
                ["timeout"] = (int)ChallengeLifetime.TotalMilliseconds,
                ["attestation"] = "none",
                ["authenticatorSelection"] = new Dictionary<string, object?>
                {
                    // 기기 내장 인증기를 권한다. 보안 열쇠는 막지 않는다 — 막으면 그것만 쓰는 사람이 등록할 길이 없다.
                    ["authenticatorAttachment"] = "platform",
                    // required 로 두지 않는다 — 저장 공간이 모자란 보안 열쇠에서 등록이 통째로 거절된다. 아이디 없이 들어오려면 기기가 열쇠를 들고 있어야 한다.
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

    /// <summary>패스키로 들어올 때 브라우저에 넘길 설정.</summary>
    /// <param name="username">아이디를 적으면 그 사람의 패스키만 후보로 준다. 비우면 기기가 스스로 고른다(아이디 없이 지문으로 들어오는 길).</param>
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

        // 아이디가 없는 계정인지 알려 주지 않는다 — 후보가 비었다고 거절하면 아이디로 계정의 존재를 캐낼 수 있다. 빈 목록도 그대로 낸다.
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


    /// <summary>등록 응답을 검증하고 패스키를 저장한다.</summary>
    public async Task<WebAuthnResult> RegisterAsync(
        Account account, WebAuthnRegisterDto request, CancellationToken cancellationToken = default)
    {
        if (Recall(request.SessionId) is not { } pending)
        {
            return new WebAuthnResult(false, "등록 시간이 지났습니다. 다시 시도해 주세요.");
        }

        // 도전값을 낸 사람과 저장하려는 사람이 같아야 한다 — 안 보면 남의 도전값으로 내 계정에 자기 기기를 붙일 수 있다.
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

        // 읽히지 않는 공개 키를 저장하면 등록은 성공하고 로그인만 실패하므로, 여기서 미리 열어 보고 거절한다.
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

        // 같은 자격 증명이 있으면 덮지 않고 되살린다 — 지웠다가 같은 기기로 다시 등록하면 고유 색인에 걸리기 때문이다.
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

    /// <summary>이미 로그인한 사람이 자기 기기를 다시 대는 잠금화면용 설정. 후보를 숨기지 않는다 — 누구인지 이미 아는 자리라 얻을 것이 없고, 숨기면 Windows Hello 처럼 열쇠를 스스로 들고 있지 않는 기기에서 열리지 않는다.
    /// 도전값에는 주인을 적어 둔다 — 받아 둔 도전값을 남에게 건네 남의 지문으로 내 잠금을 푸는 길을 막는다(<see cref="VerifyAssertionAsync"/> 가 대조).</summary>
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

    /// <summary>로그인 응답을 검증한다. 통과하면 그 패스키를 돌려준다.</summary>
    public Task<WebAuthnResult> VerifyLoginAsync(
        WebAuthnLoginDto request, CancellationToken cancellationToken = default)
        => VerifyAssertionAsync(request, expectedAccountId: null, cancellationToken);

    /// <summary>기기 서명을 검증한다. 로그인과 잠금 해제가 같은 코드를 쓴다.</summary>
    /// <param name="request">브라우저의 <c>navigator.credentials.get()</c> 결과</param>
    /// <param name="expectedAccountId">누구의 기기여야 하는가. 로그인은 null — 누구인지를 이 검증이 알아낸다. 잠금 해제는 지금 로그인한 사람을 넣어야 한다, 안 넣으면 옆 사람이 자기 지문으로 내 잠금을 푼다(서명은 멀쩡히 통과한다).</param>
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

        // 도전값에 주인이 적혀 있으면(잠금 해제) 그 사람에게 낸 것이어야 한다 — 등록(RegisterAsync)도 같은 자리를 같은 이유로 본다.
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
            // 없는 기기와 서명이 틀린 경우를 구분해 알려 주지 않는다 — 구분하면 등록된 기기를 골라내는 데 쓰인다.
            logger.LogInformation("등록되지 않은 패스키로 로그인 시도가 있었다.");
            return new WebAuthnResult(false, "등록된 기기가 아닙니다.");
        }

        // 잠금 해제의 핵심 — 서명이 맞아도 주인이 다른 열쇠를 여기서 거른다. 없으면 옆 사람의 지문으로 내 화면이 열린다.
        if (expectedAccountId is not null && credential.AccountId != expectedAccountId)
        {
            logger.LogWarning("남의 패스키로 잠금을 풀려는 시도가 있었다: credential={Id}", credential.Id);
            return new WebAuthnResult(false, "이 계정에 등록된 기기가 아닙니다.");
        }

        // 인증기가 계정 손잡이를 줬으면 맞춰 본다 — 어긋나는 응답은 정상이 아니라 거절한다.
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

        // 서명 대상은 인증기 자료 뒤에 문맥 해시를 이은 것이다 — 순서를 바꾸면 늘 실패하고 이유가 남지 않는다.
        var clientDataHash = SHA256.HashData(rawClientData);
        var signed = new byte[authData.Length + clientDataHash.Length];
        authData.CopyTo(signed, 0);
        clientDataHash.CopyTo(signed, authData.Length);

        if (!VerifySignature(credential, signed, signature))
        {
            logger.LogWarning("패스키 서명이 맞지 않는다: credential={Id}", credential.Id);
            return new WebAuthnResult(false, "기기 인증에 실패했습니다.");
        }

        // 복제 감지 카운터가 둘 다 0 이면 횟수를 안 세는 인증기(애플 패스키)다 — 그때 막으면 두 번째 로그인부터 안 된다.
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


    /// <summary>브라우저가 서명 대상에 넣은 문맥(동작·도전값·오리진)을 본다.</summary>
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

        // 도전값 대조는 고정 시간 비교로 한다 — 길이별로 빨리 끝나는 비교는 한 바이트씩 맞혀 갈 여지를 준다.
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

    /// <summary>인증기 자료의 앞머리를 본다 — RP ID 해시(32) · 깃발(1) · 서명 횟수(4).</summary>
    private WebAuthnResult VerifyAuthenticatorData(byte[] authData)
    {
        if (authData.Length < 37)
        {
            return new WebAuthnResult(false, "기기가 보낸 값이 온전하지 않습니다.");
        }

        var expected = SHA256.HashData(Encoding.UTF8.GetBytes(RelyingPartyId));
        if (!CryptographicOperations.FixedTimeEquals(authData.AsSpan(0, 32), expected))
        {
            // RP ID 가 설정과 다르면 이 로그가 곧 「WebAuthn:RelyingPartyId 를 고쳐라」다.
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

                // WebAuthn 의 ECDSA 서명은 DER 묶음이다 — 형식을 안 주면 .NET 이 고정 길이로 읽어 늘 실패한다.
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

    /// <summary>적어 둔 도전값을 꺼내고 그 자리에서 지운다 — 남겨 두면 같은 도전값으로 두 번 로그인할 수 있다(재생 공격).</summary>
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

/// <summary>base64url — 받침(<c>=</c>) 없이 <c>-_</c> 를 쓴다. WebAuthn 의 이진값이 전부 이 모양이다. 프레임워크 Base64Url 과 이름만 같다.</summary>
public static class Base64Url
{
    /// <summary>바이트를 base64url 로.</summary>
    public static string Encode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>base64url 을 바이트로. 평범한 base64 도 받는다 — 브라우저마다 보내는 쪽이 달라 가려 받으면 한 브라우저에서만 실패한다.</summary>
    public static byte[] Decode(string value)
    {
        var text = value.Replace('-', '+').Replace('_', '/');

        return Convert.FromBase64String(text.PadRight(
            text.Length + ((4 - (text.Length % 4)) % 4), '='));
    }

    /// <summary>못 읽으면 예외 대신 거짓을 준다. 이 값들은 익명 로그인 경로에서 바깥이 보낸 것이라 던지면 500 이 나고 오류 로그가 남의 장난으로 찬다. null 도 여기서 받는다.</summary>
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
