using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using SiteServer.DTOs;
using SiteServer.Services;

namespace SiteServer.Endpoints;

/// <summary>
/// 소개 사이트의 엔드포인트.
/// </summary>
/// <remarks>
/// 게이트웨이가 세 갈래로 나눠 보낸다 (ApiGateway/appsettings.json).
///
/// <list type="table">
///   <item><term><c>GET /api/site/{**}</c></term><description>익명. 여기의 공개 조회들</description></item>
///   <item><term><c>POST /api/site/inquiries</c></term><description>익명 + IP 레이트리밋(<c>public-write</c>)</description></item>
///   <item><term><c>/api/site/admin/{**}</c></term><description>인증 필요. 포털의 관리 화면이 쓴다</description></item>
/// </list>
///
/// 익명으로 열린 것은 <b>조회와 문의 접수뿐</b>이다. 나머지는 전부 <c>admin</c> 아래로 둔다.
/// </remarks>
public static class SiteEndpoints
{
    public static void MapSiteEndpoints(this IEndpointRouteBuilder app)
    {
        MapPublic(app);
        MapAdmin(app);
    }

    // ── 공개 조회 ────────────────────────────────────────────
    private static void MapPublic(IEndpointRouteBuilder app)
    {
        var pub = app.MapGroup("/");

        pub.MapGet("/sections", async (
            [FromQuery] string? locale, [FromQuery] string? keyPrefix,
            [FromServices] ISiteService svc) =>
        {
            var rows = await svc.GetSectionsAsync(locale ?? "ko", keyPrefix);
            return Results.Ok(ApiResponse<List<SectionDto>>.Ok(rows));
        })
        .WithName("GetSections").WithOpenApi();

        pub.MapGet("/posts", async (
            [FromQuery] string? locale, [FromQuery] int? take,
            [FromServices] ISiteService svc) =>
        {
            var rows = await svc.GetPostsAsync(locale ?? "ko", take ?? 20);
            return Results.Ok(ApiResponse<List<PostListItemDto>>.Ok(rows));
        })
        .WithName("GetPosts").WithOpenApi();

        pub.MapGet("/posts/{slug}", async (
            string slug, [FromQuery] string? locale,
            [FromServices] ISiteService svc) =>
        {
            var row = await svc.GetPostAsync(locale ?? "ko", slug);
            return row is null
                ? Results.NotFound(ApiResponse<PostDetailDto>.Fail("글을 찾을 수 없습니다.", "404"))
                : Results.Ok(ApiResponse<PostDetailDto>.Ok(row));
        })
        .WithName("GetPost").WithOpenApi();

        pub.MapGet("/downloads", async (
            [FromQuery] string? locale, [FromQuery] string? category,
            [FromServices] ISiteService svc) =>
        {
            var rows = await svc.GetDownloadsAsync(locale ?? "ko", category);
            return Results.Ok(ApiResponse<List<DownloadDto>>.Ok(rows));
        })
        .WithName("GetDownloads").WithOpenApi();

        // 횟수를 세고 FileServer 로 넘긴다. 브라우저가 FileServer 를 직접 열면 셀 수가 없다.
        //
        // 넘겨받는 파일은 FileServer 의 `is_public` 이 켜져 있어야 한다. 꺼져 있으면
        // 넘어간 자리에서 404 가 난다 — 자료를 등록할 때 함께 켜야 한다.
        // 응답 봉투(ApiResponse)를 쓰지 않는다. 브라우저가 그대로 따라가야 하는 리다이렉트다.
        pub.MapGet("/downloads/{id:guid}/file", async (
            Guid id, [FromServices] ISiteService svc) =>
        {
            var url = await svc.ResolveDownloadAsync(id);
            return url is null
                ? Results.NotFound(ApiResponse<bool>.Fail("자료를 찾을 수 없습니다.", "404"))
                : Results.Redirect(url);
        })
        .WithName("DownloadSiteFile").WithOpenApi();

        // 문의 접수. 익명 쓰기라 방어가 세 겹이다 —
        // 게이트웨이의 IP 레이트리밋 · 허니팟 · 동의 확인.
        //
        // **화면은 D-S7(동의 문구)이 확정되기 전까지 이 폼을 열지 않는다.**
        // API 를 먼저 두는 것은 스키마와 방어를 확정해 두려는 것이다.
        //
        // 실패해도 왜 실패했는지 자세히 알려 주지 않는다. 허니팟에 걸린 경우는
        // 성공과 같은 응답을 준다 — 봇에게 무엇에 걸렸는지 알려 주지 않는다.
        pub.MapPost("/inquiries", async (
            [FromBody] InquiryRequestDto request,
            HttpContext http,
            [FromServices] ISiteService svc) =>
        {
            if (!request.Consent)
            {
                return Results.BadRequest(
                    ApiResponse<bool>.Fail("개인정보 수집·이용에 동의해야 접수됩니다.", "400"));
            }

            var ip = http.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                     ?? http.Connection.RemoteIpAddress?.ToString();

            var id = await svc.CreateInquiryAsync(
                request, ip, http.Request.Headers.UserAgent.ToString());

            // id 가 null 이면 허니팟이거나 필수값이 빈 것이다. 둘을 구별해 주지 않는다.
            return Results.Ok(ApiResponse<bool>.Ok(true, "문의가 접수되었습니다."));
        })
        .WithName("CreateInquiry").WithOpenApi();

        // 조회 집계. 개인을 특정할 값을 쌓지 않는다 — 날짜·경로·언어별 횟수만 올린다.
        // `path` 를 nullable 로 받는다. 필수(non-nullable)로 두면 값이 없을 때
        // 미니멀 API 가 본문 없는 400 을 먼저 돌려주고 아래 안내 문구는 쓰이지 않는다.
        pub.MapPost("/visits", async (
            [FromQuery] string? path, [FromQuery] string? locale,
            [FromServices] ISiteService svc) =>
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return Results.BadRequest(ApiResponse<bool>.Fail("경로가 필요합니다.", "400"));
            }

            await svc.RecordVisitAsync(path, locale ?? "ko");
            return Results.Ok(ApiResponse<bool>.Ok(true));
        })
        .WithName("RecordVisit").WithOpenApi();
    }

    // ── 관리 (인증 필요) ─────────────────────────────────────
    private static void MapAdmin(IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/admin");

        // 게이트웨이가 이미 막지만 여기서도 본다. 서비스를 직접 부르는 경로가 생겼을 때를 위한 것이다.
        // 다른 서비스와 같은 모양이다.
        static IResult Unauthorized() => Results.Json(
            ApiResponse<bool>.Fail("로그인이 필요합니다.", "401"),
            statusCode: StatusCodes.Status401Unauthorized);

        admin.MapGet("/inquiries", async (
            UserContext? user, [FromQuery] string? status,
            [FromServices] Data.SiteDbContext db) =>
        {
            if (user is null) return Unauthorized();

            var q = db.Inquiries.Where(x => !x.IsDeleted);
            if (!string.IsNullOrWhiteSpace(status))
            {
                q = q.Where(x => x.Status == status);
            }

            var rows = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .ToListAsync(q.OrderByDescending(x => x.CreatedAt)
                    .Take(500)
                    .Select(x => new InquiryAdminDto
                    {
                        Id = x.Id,
                        Name = x.Name,
                        Company = x.Company,
                        Email = x.Email,
                        Phone = x.Phone,
                        Category = x.Category,
                        Subject = x.Subject,
                        Message = x.Message,
                        Locale = x.Locale,
                        Status = x.Status,
                        InternalNote = x.InternalNote,
                        ClientIp = x.ClientIp,
                        ConsentedAt = x.ConsentedAt,
                        CreatedAt = x.CreatedAt,
                    }));

            return Results.Ok(ApiResponse<List<InquiryAdminDto>>.Ok(rows));
        })
        .WithName("AdminListInquiries").WithOpenApi();

        admin.MapPut("/inquiries/{id:guid}/status", async (
            Guid id, [FromQuery] string value, UserContext? user,
            [FromServices] Data.SiteDbContext db) =>
        {
            if (user is null) return Unauthorized();

            string[] allowed = ["new", "reading", "answered", "spam"];
            if (!allowed.Contains(value))
            {
                return Results.BadRequest(ApiResponse<bool>.Fail(
                    $"상태는 {string.Join(" · ", allowed)} 중 하나여야 합니다.", "400"));
            }

            var row = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .FirstOrDefaultAsync(db.Inquiries, x => x.Id == id && !x.IsDeleted);
            if (row is null)
            {
                return Results.NotFound(ApiResponse<bool>.Fail("문의를 찾을 수 없습니다.", "404"));
            }

            row.Status = value;
            row.UpdatedAt = DateTime.UtcNow;
            row.UpdatedBy = user.UserId;
            await db.SaveChangesAsync();

            return Results.Ok(ApiResponse<bool>.Ok(true));
        })
        .WithName("AdminSetInquiryStatus").WithOpenApi();
    }
}
