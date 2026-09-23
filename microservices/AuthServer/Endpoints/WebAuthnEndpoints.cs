using AuthServer.Data;
using AuthServer.DTOs;
using AuthServer.Entities;
using AuthServer.Services;
using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Endpoints;

/// <summary>
/// 패스키(지문·얼굴) — 등록 · 목록 · 삭제 · 로그인.
/// </summary>
/// <remarks>
/// <para>
/// [경로가 둘로 갈린다]
/// </para>
///
/// <list type="table">
///   <item>
///     <term><c>/webauthn/login/*</c></term>
///     <description><b>익명이다.</b> 로그인하려는 사람에게 토큰이 있을 리 없다.
///     대신 게이트웨이가 <c>auth-attempts</c>(IP 당 분당 10회)로 조인다 —
///     비밀번호 로그인과 같은 통을 쓰지 않으면 이 경로가 그 제한의 우회로가 된다.</description>
///   </item>
///   <item>
///     <term>나머지</term>
///     <description>로그인한 사람만. <b>내 계정에만</b> 붙이고 뗄 수 있다 —
///     계정 아이디를 요청에서 받지 않고 신원 헤더에서만 읽는 이유가 그것이다.</description>
///   </item>
/// </list>
///
/// <para>
/// [로그인 성공 처리를 비밀번호 로그인과 한 벌로 쓴다]
/// </para>
///
/// <para>
/// 토큰 발급 · 갱신 쿠키 · 파일 쿠키 · 접속 기록 · 비밀번호 만료 판정까지,
/// 「로그인이 됐다」 뒤에 따라붙는 일이 여섯이다. 여기 한 벌을 더 적어 두면
/// <b>반드시 한쪽만 고치는 날이 온다</b> — 그때 증상은 「지문으로 들어가면
/// 사진이 안 보인다」처럼 원인과 멀다. 그래서 <see cref="LoginCompletion"/>
/// 한 곳에 모으고 <c>AuthEndpoints</c> 의 비밀번호 로그인도 그것을 부른다.
/// </para>
/// </remarks>
public static class WebAuthnEndpoints
{
    public static void MapWebAuthnEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/webauthn");

        // ── 등록 ─────────────────────────────────────────────────

        group.MapPost("/register/options", async (
            UserContext? user, AppDbContext db, WebAuthnService webAuthn, CancellationToken ct) =>
        {
            if (user is null)
            {
                return Results.Json(ApiResponse<object>.Fail("인증 정보가 없습니다.", "401"), statusCode: 401);
            }

            var account = await db.Accounts.FirstOrDefaultAsync(a => a.UserId == user.UserId, ct);
            if (account is null)
            {
                return Results.Json(ApiResponse<object>.Fail("사용자를 찾을 수 없습니다.", "404"), statusCode: 404);
            }

            var options = await webAuthn.CreateRegisterOptionsAsync(account, ct);
            return Results.Ok(ApiResponse<WebAuthnOptionsDto>.Ok(options));
        })
        .RequireAuthorization()
        .WithName("CreateWebAuthnRegisterOptions");

        group.MapPost("/register", async (
            UserContext? user, [FromBody] WebAuthnRegisterDto request,
            AppDbContext db, WebAuthnService webAuthn, CancellationToken ct) =>
        {
            if (user is null)
            {
                return Results.Json(ApiResponse<object>.Fail("인증 정보가 없습니다.", "401"), statusCode: 401);
            }

            var account = await db.Accounts.FirstOrDefaultAsync(a => a.UserId == user.UserId, ct);
            if (account is null)
            {
                return Results.Json(ApiResponse<object>.Fail("사용자를 찾을 수 없습니다.", "404"), statusCode: 404);
            }

            var result = await webAuthn.RegisterAsync(account, request, ct);
            if (!result.Ok)
            {
                return Results.Json(
                    ApiResponse<object>.Fail(result.Message ?? "등록하지 못했습니다.", "400"), statusCode: 400);
            }

            return Results.Ok(ApiResponse<WebAuthnCredentialDto>.Ok(ToDto(result.Credential!)));
        })
        .RequireAuthorization()
        .WithName("RegisterWebAuthnCredential");

        // ── 내 패스키 목록 ───────────────────────────────────────

        group.MapGet("/credentials", async (
            UserContext? user, AppDbContext db, CancellationToken ct) =>
        {
            if (user is null)
            {
                return Results.Json(ApiResponse<object>.Fail("인증 정보가 없습니다.", "401"), statusCode: 401);
            }

            var rows = await (
                from c in db.Set<AccountWebAuthnCredential>()
                join a in db.Accounts on c.AccountId equals a.Id
                where a.UserId == user.UserId && !c.IsDeleted
                orderby c.CreatedAt
                select c).ToListAsync(ct);

            return Results.Ok(ApiResponse<List<WebAuthnCredentialDto>>.Ok([.. rows.Select(ToDto)]));
        })
        .RequireAuthorization()
        .WithName("GetMyWebAuthnCredentials");

        group.MapPut("/credentials/{id}", async (
            string id, UserContext? user, [FromBody] WebAuthnRenameDto request,
            AppDbContext db, CancellationToken ct) =>
        {
            var credential = await FindMineAsync(db, user, id, ct);
            if (credential is null)
            {
                return Results.Json(ApiResponse<object>.Fail("등록된 기기가 아닙니다.", "404"), statusCode: 404);
            }

            var label = request.Label?.Trim();
            if (string.IsNullOrWhiteSpace(label))
            {
                return Results.Json(ApiResponse<object>.Fail("이름을 입력하세요.", "400"), statusCode: 400);
            }

            credential.Label = label[..Math.Min(label.Length, 60)];
            await db.SaveChangesAsync(ct);

            return Results.Ok(ApiResponse<WebAuthnCredentialDto>.Ok(ToDto(credential)));
        })
        .RequireAuthorization()
        .WithName("RenameWebAuthnCredential");

        group.MapDelete("/credentials/{id}", async (
            string id, UserContext? user, AppDbContext db,
            ILogger<AccountWebAuthnCredential> logger, CancellationToken ct) =>
        {
            var credential = await FindMineAsync(db, user, id, ct);
            if (credential is null)
            {
                return Results.Json(ApiResponse<object>.Fail("등록된 기기가 아닙니다.", "404"), statusCode: 404);
            }

            // **정말로 지운다.** 표시만 해 두면 같은 기기로 다시 등록할 때
            // 자격 증명 아이디 고유 색인에 걸린다. 잃어버린 기기를 끊는 것이
            // 이 단추의 일이라, 흔적을 남기는 것보다 확실히 없애는 편이 맞다.
            db.Remove(credential);
            await db.SaveChangesAsync(ct);

            logger.LogInformation("패스키를 지웠다: {UserId} ({Label})", user!.UserId, credential.Label);
            return Results.Ok(ApiResponse<bool>.Ok(true, "기기를 지웠습니다."));
        })
        .RequireAuthorization()
        .WithName("DeleteWebAuthnCredential");

        // ── 로그인 ───────────────────────────────────────────────

        group.MapPost("/login/options", async (
            [FromBody] WebAuthnLoginOptionsRequest? request,
            WebAuthnService webAuthn, CancellationToken ct) =>
        {
            var options = await webAuthn.CreateLoginOptionsAsync(request?.Username, ct);
            return Results.Ok(ApiResponse<WebAuthnOptionsDto>.Ok(options));
        })
        .AllowAnonymous()
        .WithName("CreateWebAuthnLoginOptions");

        group.MapPost("/login", async (
            [FromBody] WebAuthnLoginDto request, HttpContext http, AppDbContext db,
            WebAuthnService webAuthn, LoginCompletion completion,
            ILogger<Account> logger, CancellationToken ct) =>
        {
            var verified = await webAuthn.VerifyLoginAsync(request, ct);
            if (!verified.Ok)
            {
                return Results.Json(
                    ApiResponse<object>.Fail(verified.Message ?? "기기 인증에 실패했습니다.", "401"), statusCode: 401);
            }

            var account = await db.Accounts
                .FirstOrDefaultAsync(a => a.Id == verified.Credential!.AccountId, ct);

            if (account is null)
            {
                // 계정이 사라졌는데 패스키만 남은 경우다. 외래 키가 막고 있어
                // 정상 경로로는 생기지 않지만, 생기면 조용히 통과시키면 안 된다.
                logger.LogWarning("패스키의 주인 계정이 없다: {AccountId}", verified.Credential!.AccountId);
                return Results.Json(
                    ApiResponse<object>.Fail("기기 인증에 실패했습니다.", "401"), statusCode: 401);
            }

            // 승인 대기·정지 계정을 막는다. **비밀번호 로그인과 같은 판정이다** —
            // 여기를 빠뜨리면 정지된 계정이 등록해 둔 지문으로는 그대로 들어온다.
            if (await completion.RejectIfNotActiveAsync(http, account, ct) is { } rejected)
            {
                return rejected;
            }

            logger.LogInformation("패스키 로그인: {Username}", account.UserId);
            return await completion.CompleteAsync(http, account, ct);
        })
        .AllowAnonymous()
        .WithName("LoginWithWebAuthn");
    }

    /// <summary>
    /// 내 것인 패스키만 꺼낸다. <b>아이디만으로 찾지 않는다</b> — 그러면 남의
    /// 줄 번호를 넣어 지울 수 있다.
    /// </summary>
    private static async Task<AccountWebAuthnCredential?> FindMineAsync(
        AppDbContext db, UserContext? user, string id, CancellationToken ct)
    {
        if (user is null)
        {
            return null;
        }

        return await (
            from c in db.Set<AccountWebAuthnCredential>()
            join a in db.Accounts on c.AccountId equals a.Id
            where c.Id == id && a.UserId == user.UserId && !c.IsDeleted
            select c).FirstOrDefaultAsync(ct);
    }

    private static WebAuthnCredentialDto ToDto(AccountWebAuthnCredential c) => new()
    {
        Id = c.Id,
        Label = c.Label,
        Attachment = c.Attachment,
        CreatedAt = c.CreatedAt,
        LastUsedAt = c.LastUsedAt,
    };

    /// <summary>
    /// 로그인 설정 요청. 아이디는 <b>있어도 되고 없어도 된다</b> —
    /// 없으면 기기가 스스로 열쇠를 고른다.
    /// </summary>
    public sealed class WebAuthnLoginOptionsRequest
    {
        /// <summary>로그인 아이디. 비워도 된다.</summary>
        public string? Username { get; set; }
    }
}
