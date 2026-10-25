using HelpDeskServer.Data;
using HelpDeskServer.Helpers;
using HelpDeskServer.Models;
using HelpDeskServer.Services;

using JSini.Shared.Internal;

using Microsoft.EntityFrameworkCore;

namespace HelpDeskServer.Endpoints;

/// <summary>
/// 업무 비서(AI)가 쓰는 <b>내부 전용</b> 조회. 게이트웨이의 사람 인증을 거치지 않고,
/// 서버 간 머리글(<see cref="InternalCallGuard"/>)만 본다 — 평소 권한으로는 못 보는
/// 남의 글까지 비서가 대신 찾아 주기 위한 길이라, <b>회사로 거르지 않는다.</b>
/// </summary>
public static class InternalQueryEndpoints
{
    /// <summary>내부 조회 엔드포인트를 매핑한다.</summary>
    public static void MapInternalQueryEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/internal");

        // 요청 목록 — 글쓴이 · 담당자 · 댓글 단 사람 세 갈래를 OR 로 묶어 찾는다.
        group.MapGet("/requests", async (
            HttpContext http,
            AppDbContext db,
            IConfiguration config,
            IPortalCompanyDirectory companies,
            string? userId,
            DateTime? from,
            DateTime? to,
            string? status,
            string? keyword,
            int? take) =>
        {
            if (!InternalCallGuard.IsAllowed(http.Request.Headers["X-Internal-Token"], config["Assistant:InternalToken"]))
                return Results.NotFound();

            var ct = http.RequestAborted;
            var takeCount = Math.Clamp(take ?? 30, 1, 100);

            // 사람 조건은 헬프데스크 내부 번호(Customer/Admin 의 Id)로 걸리므로,
            // 포털 사용자 아이디를 먼저 그 내부 번호들로 풀어 둔다.
            var customerIds = new List<int>();
            var adminIds = new List<int>();
            if (!string.IsNullOrWhiteSpace(userId))
            {
                customerIds = await db.AuthUserLinks
                    .Where(l => l.UserType == "customer" && l.AuthUserId == userId)
                    .Select(l => l.HelpdeskUserId)
                    .ToListAsync(ct);

                adminIds = await db.AuthUserLinks
                    .Where(l => l.UserType == "admin" && l.AuthUserId == userId)
                    .Select(l => l.HelpdeskUserId)
                    .ToListAsync(ct);
            }

            var query = db.Requests.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(userId))
            {
                query = query.Where(r =>
                    customerIds.Contains(r.CustomerId)
                    || (r.AdminId != null && adminIds.Contains(r.AdminId.Value))
                    || r.Comments.Any(c => !c.IsDel && (
                        (c.AuthorType.ToLower() == "admin" && adminIds.Contains(c.AuthorId))
                        || (c.AuthorType.ToLower() != "admin" && customerIds.Contains(c.AuthorId)))));
            }

            if (from.HasValue) query = query.Where(r => r.RequestedAt >= from.Value);
            if (to.HasValue) query = query.Where(r => r.RequestedAt <= to.Value);

            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ImprovementStatus>(status, true, out var statusValue))
                query = query.Where(r => r.Status == statusValue);

            if (!string.IsNullOrWhiteSpace(keyword))
                query = query.Where(r =>
                    EF.Functions.ILike(r.Title, $"%{keyword}%")
                    || EF.Functions.ILike(r.Description, $"%{keyword}%"));

            var total = await query.CountAsync(ct);

            var rows = await query
                .Include(r => r.Customer)
                .Include(r => r.Admin)
                .OrderByDescending(r => r.RequestedAt)
                .Take(takeCount)
                .Select(r => new
                {
                    r.Id,
                    r.Title,
                    r.Status,
                    r.RequestedAt,
                    r.AcceptedAt,
                    CompletedAt = r.CompletededAt ?? r.UserCompletededAt,
                    r.CustomerId,
                    r.AdminId,
                    AuthorName = r.Customer != null ? r.Customer.UserName : null,
                    AssigneeName = r.Admin != null ? r.Admin.UserName : null,
                    CompanyId = r.Customer != null ? r.Customer.CompanyId : null,
                    Commented = r.Comments.Any(c => !c.IsDel && (
                        (c.AuthorType.ToLower() == "admin" && adminIds.Contains(c.AuthorId))
                        || (c.AuthorType.ToLower() != "admin" && customerIds.Contains(c.AuthorId)))),
                })
                .ToListAsync(ct);

            // 회사 이름은 포털에서 한 번에 받아 둔다 — 줄마다 부르면 느려진다.
            var companyNames = await companies.GetNamesAsync(ct);

            var items = rows.Select(r => new
            {
                id = r.Id,
                title = r.Title,
                status = r.Status.ToString(),
                requestedAt = r.RequestedAt,
                acceptedAt = r.AcceptedAt,
                completedAt = r.CompletedAt,
                authorName = r.AuthorName,
                assigneeName = r.AssigneeName,
                companyName = r.CompanyId is null ? null : (companyNames.GetValueOrDefault(r.CompanyId) ?? r.CompanyId),
                match = PersonMatch.Of(
                    wrote: customerIds.Contains(r.CustomerId),
                    assigned: r.AdminId.HasValue && adminIds.Contains(r.AdminId.Value),
                    commented: r.Commented),
            });

            return Results.Ok(new { total, items });
        });

        // 요청 상세 — 본문과 댓글 전부. 지운 댓글은 사람이 보는 화면과 같은 규칙으로 가린다(CommentVisibility).
        group.MapGet("/requests/{id:int}", async (
            HttpContext http,
            AppDbContext db,
            IConfiguration config,
            IPortalCompanyDirectory companies,
            IPortalAccountDirectory accounts,
            int id) =>
        {
            if (!InternalCallGuard.IsAllowed(http.Request.Headers["X-Internal-Token"], config["Assistant:InternalToken"]))
                return Results.NotFound();

            var ct = http.RequestAborted;

            var request = await db.Requests
                .AsNoTracking()
                .Include(r => r.Customer)
                .Include(r => r.Admin)
                .Include(r => r.Comments)
                .FirstOrDefaultAsync(r => r.Id == id, ct);

            if (request is null) return Results.NotFound();

            var comments = CommentVisibility.Mask(CommentVisibility.Visible(request.Comments));

            var adminIds = comments.Where(c => c.AuthorType.Equals("admin", StringComparison.OrdinalIgnoreCase))
                .Select(c => c.AuthorId).Distinct().ToList();
            var customerIds = comments.Where(c => !c.AuthorType.Equals("admin", StringComparison.OrdinalIgnoreCase))
                .Select(c => c.AuthorId).Distinct().ToList();

            var admins = await db.Admins.Where(a => adminIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
            var customers = await db.Customers.Where(c => customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);

            var companyName = await companies.GetNameAsync(request.Customer?.CompanyId, ct) ?? request.Customer?.CompanyId;

            return Results.Ok(new
            {
                id = request.Id,
                title = request.Title,
                description = request.Description,
                status = request.Status.ToString(),
                requestedAt = request.RequestedAt,
                acceptedAt = request.AcceptedAt,
                completedAt = request.CompletededAt ?? request.UserCompletededAt,
                authorName = request.Customer?.UserName,
                assigneeName = request.Admin?.UserName,
                companyName,
                comments = comments.Select(c => new
                {
                    id = c.Id,
                    authorType = c.AuthorType,
                    authorName = c.AuthorType.Equals("admin", StringComparison.OrdinalIgnoreCase)
                        ? admins.GetValueOrDefault(c.AuthorId)?.UserName
                        : customers.GetValueOrDefault(c.AuthorId)?.UserName,
                    commentText = c.CommentText,
                    isDeleted = c.IsDel,
                    createdAt = c.CreatedAt,
                    parentCommentId = c.ParentCommentId,
                }),
            });
        });

        // 일정(Schedules) — 작성자(CreatedBy)가 곧 포털 로그인 아이디라, 사람 조건은 바로 비교한다.
        group.MapGet("/schedules", async (
            HttpContext http,
            AppDbContext db,
            IConfiguration config,
            IPortalAccountDirectory accounts,
            string? userId,
            DateTime? from,
            DateTime? to,
            string? keyword,
            int? take) =>
        {
            if (!InternalCallGuard.IsAllowed(http.Request.Headers["X-Internal-Token"], config["Assistant:InternalToken"]))
                return Results.NotFound();

            var ct = http.RequestAborted;
            var takeCount = Math.Clamp(take ?? 30, 1, 100);

            var query = db.Schedules.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(userId))
                query = query.Where(s => s.CreatedBy == userId);

            // 기간이 겹치는 일정만 본다 — 일정은 하루짜리가 아니라 시작·끝을 모두 가진다.
            if (from.HasValue) query = query.Where(s => s.EndDate >= from.Value);
            if (to.HasValue) query = query.Where(s => s.StartDate <= to.Value);

            if (!string.IsNullOrWhiteSpace(keyword))
                query = query.Where(s =>
                    EF.Functions.ILike(s.Title, $"%{keyword}%")
                    || (s.Description != null && EF.Functions.ILike(s.Description, $"%{keyword}%")));

            var total = await query.CountAsync(ct);

            var rows = await query
                .OrderBy(s => s.StartDate)
                .Take(takeCount)
                .Select(s => new { s.Title, s.StartDate, s.EndDate, s.IsCompleted, s.CreatedBy })
                .ToListAsync(ct);

            // 이름 표시용 — 만든 사람 아이디를 한 번에 풀어 둔다.
            var ownerIds = rows.Select(r => r.CreatedBy).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct()!;
            var ownerNames = await accounts.GetNamesAsync(ownerIds!, ct);

            var items = rows.Select(r => new
            {
                source = "helpdesk",
                title = r.Title,
                startDate = r.StartDate,
                endDate = r.EndDate,
                done = r.IsCompleted,
                owner = r.CreatedBy is null ? null : (ownerNames.GetValueOrDefault(r.CreatedBy) ?? r.CreatedBy),
            });

            return Results.Ok(new { total, items });
        });
    }
}
