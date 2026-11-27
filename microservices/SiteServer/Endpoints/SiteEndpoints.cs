using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using SiteServer.DTOs;
using SiteServer.Services;

namespace SiteServer.Endpoints;

/// <summary>소개 사이트의 엔드포인트. 게이트웨이가 익명 조회, 익명 문의 접수(IP 레이트리밋), admin 아래 관리로 나눠 보내며, 익명으로 열린 것은 조회와 문의 접수뿐이다.</summary>
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
        .WithName("GetSections");

        pub.MapGet("/posts", async (
            [FromQuery] string? locale, [FromQuery] int? take,
            [FromServices] ISiteService svc) =>
        {
            var rows = await svc.GetPostsAsync(locale ?? "ko", take ?? 20);
            return Results.Ok(ApiResponse<List<PostListItemDto>>.Ok(rows));
        })
        .WithName("GetPosts");

        pub.MapGet("/posts/{slug}", async (
            string slug, [FromQuery] string? locale,
            [FromServices] ISiteService svc) =>
        {
            var row = await svc.GetPostAsync(locale ?? "ko", slug);
            return row is null
                ? Results.NotFound(ApiResponse<PostDetailDto>.Fail("글을 찾을 수 없습니다.", "404"))
                : Results.Ok(ApiResponse<PostDetailDto>.Ok(row));
        })
        .WithName("GetPost");

        pub.MapGet("/downloads", async (
            [FromQuery] string? locale, [FromQuery] string? category,
            [FromServices] ISiteService svc) =>
        {
            var rows = await svc.GetDownloadsAsync(locale ?? "ko", category);
            return Results.Ok(ApiResponse<List<DownloadDto>>.Ok(rows));
        })
        .WithName("GetDownloads");

        // 횟수를 세고 FileServer 로 넘긴다 — 브라우저가 FileServer 를 직접 열면 셀 수가 없다. 넘어가는 파일은 is_public 이 켜져 있어야 하고(꺼져 있으면 넘어간 자리에서 404), 응답 봉투를 쓰지 않는 리다이렉트다.
        pub.MapGet("/downloads/{id:guid}/file", async (
            Guid id, [FromServices] ISiteService svc) =>
        {
            var url = await svc.ResolveDownloadAsync(id);
            return url is null
                ? Results.NotFound(ApiResponse<bool>.Fail("자료를 찾을 수 없습니다.", "404"))
                : Results.Redirect(url);
        })
        .WithName("DownloadSiteFile");

        // 문의 접수. 익명 쓰기라 방어가 여러 겹이다 — 게이트웨이 IP 레이트리밋 · 본문 크기 상한 · 허니팟 · 폼 스팸 판별 · 동의 확인.
        // 봇으로 본 것은 사유를 알려 주지 않고 성공과 같은 응답을 준다. 길이·메일 형식처럼 사람이 고칠 잘못만 400 으로 말한다.
        pub.MapPost("/inquiries", async (
            [FromBody] InquiryRequestDto request,
            HttpContext http,
            [FromServices] ISiteService svc,
            [FromServices] IInquiryMailNotifier mailNotifier,
            [FromServices] IInquiryPushNotifier pushNotifier) =>
        {
            if (!request.Consent)
            {
                return Results.BadRequest(
                    ApiResponse<bool>.Fail("개인정보 수집·이용에 동의해야 접수됩니다.", "400"));
            }

            // 게이트웨이가 속도 제한에 쓴 방문자 주소(X-Client-Ip)가 정본이다. X-Forwarded-For 는 YARP 가
            // 게이트웨이에 붙은 쪽(소개 사이트 컨테이너) 주소로 덮어써서 모든 문의가 같은 주소로 남는다.
            var ip = FirstValue(http.Request.Headers["X-Client-Ip"])
                     ?? http.Connection.RemoteIpAddress?.ToString();

            // 소개 사이트는 서버가 대신 부르므로 User-Agent 가 그 서버 것이다. 방문자 것을 따로 실어 온다.
            var visitorUa = http.Request.Headers["X-Visitor-User-Agent"].ToString();
            var ua = string.IsNullOrWhiteSpace(visitorUa) ? http.Request.Headers.UserAgent.ToString() : visitorUa;

            var intake = await svc.CreateInquiryAsync(request, ip, ua);

            if (intake.Rejection is not null)
            {
                return Results.BadRequest(ApiResponse<bool>.Fail(intake.Rejection, "400"));
            }

            // 저장된 문의만, 저장된 값(거른 본문)으로 알린다. 메일과 푸시는 서로를 막지 않도록 둘 다 예외를 삼키고,
            // 누가 어느 길로 받는지는 포털관리 알림관리의 SITE_INQUIRY 가 정한다.
            if (intake.Saved is { } saved)
            {
                var notice = new InquiryRequestDto
                {
                    Name = saved.Name,
                    Company = saved.Company,
                    Email = saved.Email,
                    Phone = saved.Phone,
                    Category = saved.Category,
                    Subject = saved.Subject,
                    Message = saved.Message,
                    Locale = saved.Locale,
                    Consent = true,
                };
                await mailNotifier.NotifyAsync(saved.Id, notice, http.RequestAborted);
                await pushNotifier.NotifyAsync(saved.Id, notice, http.RequestAborted);
            }

            return Results.Ok(ApiResponse<bool>.Ok(true, "문의가 접수되었습니다."));
        })
        // 본문 상한(MessageMax 5000자)에 여유를 둔 크기. 기본 30MB 를 그대로 받으면 역직렬화까지 다 한 뒤에야 거절한다.
        .WithMetadata(new RequestSizeLimitAttribute(64 * 1024))
        .WithName("CreateInquiry");

        static string? FirstValue(Microsoft.Extensions.Primitives.StringValues values)
        {
            var v = values.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();
            return string.IsNullOrWhiteSpace(v) ? null : v;
        }

        // 조회 집계. 개인을 특정할 값은 쌓지 않고 날짜·경로·언어별 횟수만 올린다. path 는 nullable 로 받아야 빈 값에서 안내 문구가 쓰인다.
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
        .WithName("RecordVisit");
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

        // 문의에는 바깥 사람의 연락처가 담긴다. 메뉴(/company/site-inquiries)를 쥔 역할과 맞춘다.
        static bool IsAdmin(UserContext user)
        {
            string[] adminRoles = ["SYSTEM_ADMINISTRATOR", "ADMINISTRATOR"];
            return user.Roles.Append(user.Role)
                .Any(r => adminRoles.Contains(r.Trim(), StringComparer.OrdinalIgnoreCase));
        }

        static IResult Forbidden() => Results.Json(
            ApiResponse<bool>.Fail("문의를 볼 권한이 없습니다.", "403"),
            statusCode: StatusCodes.Status403Forbidden);

        admin.MapGet("/inquiries", async (
            UserContext? user, [FromQuery] string? status,
            [FromServices] Data.SiteDbContext db) =>
        {
            if (user is null) return Unauthorized();
            if (!IsAdmin(user)) return Forbidden();

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
        .WithName("AdminListInquiries");

        admin.MapPut("/inquiries/{id:guid}/status", async (
            Guid id, [FromBody] InquiryStatusUpdateDto request, UserContext? user,
            [FromServices] Data.SiteDbContext db) =>
        {
            if (user is null) return Unauthorized();
            if (!IsAdmin(user)) return Forbidden();

            string[] allowed = ["new", "reading", "replied", "closed"];
            if (!allowed.Contains(request.Status))
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

            row.Status = request.Status;
            row.UpdatedAt = DateTime.UtcNow;
            row.UpdatedBy = user.UserId;
            await db.SaveChangesAsync();

            return Results.Ok(ApiResponse<bool>.Ok(true));
        })
        .WithName("AdminSetInquiryStatus");

        admin.MapDelete("/inquiries/{id:guid}", async (
            Guid id, UserContext? user,
            [FromServices] Data.SiteDbContext db) =>
        {
            if (user is null) return Unauthorized();
            if (!IsAdmin(user)) return Forbidden();

            var row = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .FirstOrDefaultAsync(db.Inquiries, x => x.Id == id && !x.IsDeleted);
            if (row is null)
            {
                return Results.NotFound(ApiResponse<bool>.Fail("문의를 찾을 수 없습니다.", "404"));
            }

            row.IsDeleted = true;
            row.UpdatedAt = DateTime.UtcNow;
            row.UpdatedBy = user.UserId;
            await db.SaveChangesAsync();

            return Results.Ok(ApiResponse<bool>.Ok(true));
        })
        .WithName("AdminDeleteInquiry");

        // 목록에서 체크한 것을 한 왕복·한 저장으로 처리한다 — 한 건씩 부르면 중간에 끊겼을 때 어디까지 됐는지 알 수 없다.
        const int BulkMax = 500;

        admin.MapPost("/inquiries/bulk-status", async (
            [FromBody] InquiryBulkDto request, UserContext? user,
            [FromServices] Data.SiteDbContext db) =>
        {
            if (user is null) return Unauthorized();
            if (!IsAdmin(user)) return Forbidden();

            string[] allowed = ["new", "reading", "replied", "closed"];
            if (request.Status is null || !allowed.Contains(request.Status))
            {
                return Results.BadRequest(ApiResponse<bool>.Fail(
                    $"상태는 {string.Join(" · ", allowed)} 중 하나여야 합니다.", "400"));
            }

            var ids = request.Ids.Distinct().ToList();
            if (ids.Count == 0 || ids.Count > BulkMax)
            {
                return Results.BadRequest(ApiResponse<bool>.Fail($"문의를 1~{BulkMax}건 골라야 합니다.", "400"));
            }

            var rows = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .ToListAsync(db.Inquiries.Where(x => ids.Contains(x.Id) && !x.IsDeleted));
            var now = DateTime.UtcNow;
            foreach (var row in rows)
            {
                row.Status = request.Status;
                row.UpdatedAt = now;
                row.UpdatedBy = user.UserId;
            }
            await db.SaveChangesAsync();

            return Results.Ok(ApiResponse<int>.Ok(rows.Count));
        })
        .WithName("AdminBulkSetInquiryStatus");

        admin.MapPost("/inquiries/bulk-delete", async (
            [FromBody] InquiryBulkDto request, UserContext? user,
            [FromServices] Data.SiteDbContext db) =>
        {
            if (user is null) return Unauthorized();
            if (!IsAdmin(user)) return Forbidden();

            var ids = request.Ids.Distinct().ToList();
            if (ids.Count == 0 || ids.Count > BulkMax)
            {
                return Results.BadRequest(ApiResponse<bool>.Fail($"문의를 1~{BulkMax}건 골라야 합니다.", "400"));
            }

            var rows = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .ToListAsync(db.Inquiries.Where(x => ids.Contains(x.Id) && !x.IsDeleted));
            var now = DateTime.UtcNow;
            foreach (var row in rows)
            {
                row.IsDeleted = true;
                row.UpdatedAt = now;
                row.UpdatedBy = user.UserId;
            }
            await db.SaveChangesAsync();

            return Results.Ok(ApiResponse<int>.Ok(rows.Count));
        })
        .WithName("AdminBulkDeleteInquiries");

        // ── 답장 ────────────────────────────────────────────
        // 문의 답장. 주소는 화면이 채우거나 관리자가 적어 보내므로 to 를 요청으로 받는다. 본문은 에디터가 쓴 HTML 이고 메일 틀은 InquiryEmailTemplates 가 입힌다.
        admin.MapPost("/inquiries/{id:guid}/reply", async (
            Guid id, [FromBody] InquiryReplyDto request, UserContext? user,
            [FromServices] Data.SiteDbContext db,
            [FromServices] IInquiryMailNotifier mailer) =>
        {
            if (user is null) return Unauthorized();
            if (!IsAdmin(user)) return Forbidden();

            if (string.IsNullOrWhiteSpace(request.To) ||
                !System.Net.Mail.MailAddress.TryCreate(request.To.Trim(), out _))
            {
                return Results.BadRequest(ApiResponse<bool>.Fail("받는 사람 이메일이 올바르지 않습니다.", "400"));
            }
            if (string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.Body))
            {
                return Results.BadRequest(ApiResponse<bool>.Fail("제목과 본문이 필요합니다.", "400"));
            }

            var row = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .FirstOrDefaultAsync(db.Inquiries, x => x.Id == id && !x.IsDeleted);
            if (row is null)
            {
                return Results.NotFound(ApiResponse<bool>.Fail("문의를 찾을 수 없습니다.", "404"));
            }

            var to = request.To.Trim();
            var html = InquiryEmailTemplates.Reply(request.Body, row);
            var sent = await mailer.SendHtmlAsync(to, request.Subject.Trim(), html);

            if (!sent)
            {
                // 실패를 성공으로 말하지 않는다 — 관리자가 다시 시도해야 한다.
                return Results.Json(
                    ApiResponse<bool>.Fail("메일 발송에 실패했습니다. 잠시 후 다시 시도해 주세요.", "EMAIL_SEND_FAILED"),
                    statusCode: StatusCodes.Status502BadGateway);
            }

            // 보낸 기록을 남기고 상태를 '답변 완료' 로 올린다.
            row.Status = "replied";
            row.InternalNote = string.IsNullOrWhiteSpace(row.InternalNote)
                ? $"[답장 {DateTime.UtcNow:yyyy-MM-dd HH:mm}Z] {user.UserId} → {to}"
                : $"{row.InternalNote}\n[답장 {DateTime.UtcNow:yyyy-MM-dd HH:mm}Z] {user.UserId} → {to}";
            row.UpdatedAt = DateTime.UtcNow;
            row.UpdatedBy = user.UserId;
            await db.SaveChangesAsync();

            return Results.Ok(ApiResponse<bool>.Ok(true, "답장을 보냈습니다."));
        })
        .WithName("AdminReplyInquiry");
    }
}
