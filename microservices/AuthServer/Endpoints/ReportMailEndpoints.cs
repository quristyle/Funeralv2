using AuthServer.DTOs;
using AuthServer.Services;
using JSini.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AuthServer.Endpoints;

/// <summary>보고서 메일 배치 — 고를 수 있는 보고서, 배치의 등록·수정·삭제, 받는 사람 미리보기, 지금 한 번 보내기. 게이트웨이가 <c>/api/auth/**</c> 를 익명으로 열어 두므로 로그인은 서버가 막는다.</summary>
public static class ReportMailEndpoints
{
    /// <summary>이 화면의 열쇠. 화면이 <c>RouteKey</c> 로 선언한 글자와 같아야 한다.</summary>
    private const string ReportMailRouteKey = "admin.system.report-mail";

    /// <summary>열쇠가 아직 안 채워진 DB 를 위한 경로 대비책.</summary>
    private static readonly string[] ReportMailPaths = ["/admin/system/report-mail"];

    public static void MapReportMailEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/system/report-mail").WithTags("ReportMail");

        // ── 고를 수 있는 보고서 ─────────────────────────────
        group.MapGet("/reports", async (
            UserContext? user,
            HttpContext http,
            [FromServices] IReportMailService service,
            [FromServices] MenuViewAccess access,
            CancellationToken ct) =>
        {
            if (await ForbidAsync(user, http, access, ct) is { } denied) return denied;

            var rows = await service.GetCatalogAsync(ct);
            return Results.Ok(ApiResponse<List<ReportCatalogItemDto>>.Ok(rows));
        })
        .WithName("GetReportMailCatalog");

        // ── 배치 목록 ───────────────────────────────────────
        group.MapGet("", async (
            UserContext? user,
            HttpContext http,
            [FromQuery] string? keyword,
            [FromQuery] bool? activeOnly,
            [FromServices] IReportMailService service,
            [FromServices] MenuViewAccess access,
            CancellationToken ct) =>
        {
            if (await ForbidAsync(user, http, access, ct) is { } denied) return denied;

            var rows = await service.GetSchedulesAsync(keyword, activeOnly ?? false, ct);
            return Results.Ok(ApiResponse<List<ReportMailScheduleDto>>.Ok(rows));
        })
        .WithName("GetReportMailSchedules");

        // ── 배치 하나 ───────────────────────────────────────
        group.MapGet("/{id}", async (
            string id,
            UserContext? user,
            HttpContext http,
            [FromServices] IReportMailService service,
            [FromServices] MenuViewAccess access,
            CancellationToken ct) =>
        {
            if (await ForbidAsync(user, http, access, ct) is { } denied) return denied;

            var row = await service.GetScheduleAsync(id, ct);

            return row is null
                ? Results.NotFound(ApiResponse<ReportMailScheduleDto>.Fail("배치를 찾을 수 없습니다.", "NOT_FOUND"))
                : Results.Ok(ApiResponse<ReportMailScheduleDto>.Ok(row));
        })
        .WithName("GetReportMailSchedule");

        // ── 등록 ────────────────────────────────────────────
        group.MapPost("", async (
            UserContext? user,
            HttpContext http,
            [FromBody] SaveReportMailScheduleDto request,
            [FromServices] IReportMailService service,
            [FromServices] MenuViewAccess access,
            CancellationToken ct) =>
        {
            if (await ForbidAsync(user, http, access, ct) is { } denied) return denied;

            try
            {
                var row = await service.CreateAsync(request, user!.UserId, ct);
                return Results.Ok(ApiResponse<ReportMailScheduleDto>.Ok(row, "배치를 등록했습니다."));
            }
            catch (InvalidOperationException ex)
            {
                // 「보고서를 안 골랐다」는 서버 오류가 아니라 사람에게 할 말이다 — 500 으로 던지면 화면이 「저장하지 못했습니다」만 띄운다.
                return Results.BadRequest(ApiResponse<ReportMailScheduleDto>.Fail(ex.Message, "INVALID"));
            }
        })
        .WithName("CreateReportMailSchedule");

        // ── 수정 ────────────────────────────────────────────
        group.MapPut("/{id}", async (
            string id,
            UserContext? user,
            HttpContext http,
            [FromBody] SaveReportMailScheduleDto request,
            [FromServices] IReportMailService service,
            [FromServices] MenuViewAccess access,
            CancellationToken ct) =>
        {
            if (await ForbidAsync(user, http, access, ct) is { } denied) return denied;

            try
            {
                var row = await service.UpdateAsync(id, request, user!.UserId, ct);

                return row is null
                    ? Results.NotFound(ApiResponse<ReportMailScheduleDto>.Fail("배치를 찾을 수 없습니다.", "NOT_FOUND"))
                    : Results.Ok(ApiResponse<ReportMailScheduleDto>.Ok(row, "배치를 고쳤습니다."));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ApiResponse<ReportMailScheduleDto>.Fail(ex.Message, "INVALID"));
            }
        })
        .WithName("UpdateReportMailSchedule");

        // ── 삭제 ────────────────────────────────────────────
        group.MapDelete("/{id}", async (
            string id,
            UserContext? user,
            HttpContext http,
            [FromServices] IReportMailService service,
            [FromServices] MenuViewAccess access,
            CancellationToken ct) =>
        {
            if (await ForbidAsync(user, http, access, ct) is { } denied) return denied;

            var removed = await service.DeleteAsync(id, user!.UserId, ct);

            return removed
                ? Results.Ok(ApiResponse<bool>.Ok(true, "배치를 지웠습니다."))
                : Results.NotFound(ApiResponse<bool>.Fail("배치를 찾을 수 없습니다.", "NOT_FOUND"));
        })
        .WithName("DeleteReportMailSchedule");

        // 받게 되는 사람은 보내기 전에 보여 준다 — 역할을 골랐는데 아무에게도 안 가는 일이 조용히 일어나는 자리가 둘이다(걸린 사람이 없음 · 이메일이 없음).
        group.MapGet("/{id}/recipients", async (
            string id,
            UserContext? user,
            HttpContext http,
            [FromServices] IReportMailService service,
            [FromServices] MenuViewAccess access,
            CancellationToken ct) =>
        {
            if (await ForbidAsync(user, http, access, ct) is { } denied) return denied;

            var rows = await service.GetRecipientsAsync(id, ct);

            return rows is null
                ? Results.NotFound(ApiResponse<ReportMailRecipientsDto>.Fail("배치를 찾을 수 없습니다.", "NOT_FOUND"))
                : Results.Ok(ApiResponse<ReportMailRecipientsDto>.Ok(rows));
        })
        .WithName("GetReportMailRecipients");

        // 지금 한 번 보내기는 보낸 때(last_sent_at)를 찍지 않는다 — 눌러 본 한 통 때문에 정기 발송이 건너뛰어지면 안 된다.
        group.MapPost("/{id}/send", async (
            string id,
            UserContext? user,
            HttpContext http,
            [FromServices] IReportMailService service,
            [FromServices] MenuViewAccess access,
            CancellationToken ct) =>
        {
            if (await ForbidAsync(user, http, access, ct) is { } denied) return denied;

            var (ok, message) = await service.SendNowAsync(id, user!.UserId, ct);

            // 실패를 성공으로 말하지 않는다. 화면이 띄우는 글자가 곧 사유다.
            return ok
                ? Results.Ok(ApiResponse<bool>.Ok(true, message))
                : Results.BadRequest(ApiResponse<bool>.Fail(message, "SEND_FAILED"));
        })
        .WithName("SendReportMailNow");

        // 미리받아보기는 본인에게만 보낸다 — 역할을 거치지 않고 배치를 저장하지 않으며 DB 에 자국도 남기지 않는다. 길에 {id} 가 없는 것은 저장 전에도 누를 수 있기 때문이다.
        group.MapPost("/preview", async (
            UserContext? user,
            HttpContext http,
            [FromBody] ReportMailPreviewDto request,
            [FromServices] IReportMailService service,
            [FromServices] MenuViewAccess access,
            CancellationToken ct) =>
        {
            if (await ForbidAsync(user, http, access, ct) is { } denied) return denied;

            var (ok, message, result) = await service.SendPreviewAsync(request, user!.UserId, ct);

            // 실패를 성공으로 말하지 않는다 — 「고른 보고서가 없다」와 「내 계정에 이메일이 없다」는 할 일이 달라서, 화면이 띄우는 글자가 곧 안내다.
            return ok
                ? Results.Ok(ApiResponse<ReportMailPreviewResultDto>.Ok(result!, message))
                : Results.BadRequest(ApiResponse<ReportMailPreviewResultDto>.Fail(message, "PREVIEW_FAILED"));
        })
        .WithName("SendReportMailPreview");
    }

    /// <summary>볼 수 있는 사람인가. 막아야 하면 그 응답을, 통과면 null 을 준다.</summary>
    private static async Task<IResult?> ForbidAsync(
        UserContext? user, HttpContext http, MenuViewAccess access, CancellationToken ct)
    {
        if (user is null) return Results.Unauthorized();

        // `X-User-Role` 은 첫 역할 하나뿐이라 전체 목록(`X-User-Roles`)으로 함께 본다.
        var roles = http.Request.Headers["X-User-Roles"].ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Concat(string.IsNullOrWhiteSpace(user.Role) ? [] : new[] { user.Role })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (await access.CanViewAsync(roles, ReportMailRouteKey, ReportMailPaths, ct))
        {
            return null;
        }

        return Results.Json(
            ApiResponse<object>.Fail(
                "이 화면을 볼 권한이 없습니다. 메뉴 권한에서 역할을 확인하세요.", "403"),
            statusCode: StatusCodes.Status403Forbidden);
    }
}
