using CargoTrustServer.Audit;
using CargoTrustServer.Common;
using CargoTrustServer.Companies;
using CargoTrustServer.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CargoTrustServer.Admin;

/// <summary>관리자 — 사용자 (유형 · 운송사의 회사 연결 · 차단)</summary>
public static class AdminUserEndpoints
{
    public static void MapAdminUserEndpoints(this RouteGroupBuilder admin)
    {
        admin.MapGet("/users", List).WithSummary("사용자 목록");
        admin.MapPut("/users/{id:long}", Update).WithSummary("사용자 유형 · 회사 연결 · 차단");
    }

    private static async Task<IResult> List(
        CargoTrustDbContext db, IOptions<CargoTrustOptions> options, string? q, string? userType, CancellationToken ct)
    {
        if (!Code.TryParse<UserType>(userType, out var ut))
            return ApiError.BadRequest($"userType 은 {Code.Allowed<UserType>()} 중 하나입니다.");

        var query = db.Users.AsNoTracking().Include(u => u.Company).AsQueryable();
        if (ut is { } t) query = query.Where(u => u.UserType == t);
        if (Check.Clean(q) is { } text)
        {
            var pattern = "%" + CompanyEndpoints.EscapeLike(text) + "%";
            query = query.Where(u =>
                EF.Functions.ILike(u.ExternalUserId, pattern, "\\")
                || (u.DisplayName != null && EF.Functions.ILike(u.DisplayName, pattern, "\\"))
                || (u.Company != null && EF.Functions.ILike(u.Company.CompanyName, pattern, "\\")));
        }

        var users = await query
            .OrderByDescending(u => u.LastSeenAt).ThenByDescending(u => u.UserId)
            .Take(options.Value.ListLimit)
            .ToListAsync(ct);
        return Results.Ok(await ToDtosAsync(db, users, ct));
    }

    private static async Task<IResult> Update(
        CargoTrustDbContext db, AuditService audit, long id, AdminUserUpdate req, CancellationToken ct)
    {
        if (!Code.TryParse<UserType>(req.UserType, out var ut))
            return ApiError.BadRequest($"userType 은 {Code.Allowed<UserType>()} 중 하나입니다.");
        if (!Code.TryParse<UserStatus>(req.Status, out var st))
            return ApiError.BadRequest($"status 는 {Code.Allowed<UserStatus>()} 중 하나입니다.");

        var user = await db.Users.Include(u => u.Company).FirstOrDefaultAsync(u => u.UserId == id, ct);
        if (user is null) return ApiError.NotFound("사용자를 찾을 수 없습니다.");

        if (req.CompanyId is { } cid && !await db.Companies.AnyAsync(c => c.CompanyId == cid, ct))
            return ApiError.BadRequest("연결할 거래처를 찾을 수 없습니다.");

        var before = audit.Snapshot(user);
        if (ut is { } t) user.UserType = t;
        if (st is { } s) user.Status = s;
        user.CompanyId = req.CompanyId;
        user.AdminMemo = Check.Clean(req.AdminMemo);
        audit.AddChange("ADMIN_USER_UPDATE", AuditTarget.User, id, before, user);
        await db.SaveChangesAsync(ct);

        // 회사 연결을 바꿨으면 이름을 새로 읽는다
        await db.Entry(user).Reference(u => u.Company).LoadAsync(ct);
        return Results.Ok((await ToDtosAsync(db, [user], ct))[0]);
    }

    private static async Task<List<AdminUserDto>> ToDtosAsync(CargoTrustDbContext db, List<AppUser> users, CancellationToken ct)
    {
        var ids = users.Select(u => u.UserId).ToList();
        var txCounts = await db.Transactions.AsNoTracking()
            .Where(t => ids.Contains(t.UserId) && !t.IsDeleted)
            .GroupBy(t => t.UserId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var reportCounts = await db.Reports.AsNoTracking()
            .Where(r => ids.Contains(r.ReporterUserId))
            .GroupBy(r => r.ReporterUserId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        return users.Select(u => new AdminUserDto(
            u.UserId,
            u.ExternalUserId,
            u.DisplayName,
            u.UserType,
            u.CompanyId,
            u.Company?.CompanyName,
            u.Status,
            u.AdminMemo,
            txCounts.GetValueOrDefault(u.UserId),
            reportCounts.GetValueOrDefault(u.UserId),
            u.CreatedAt,
            u.LastSeenAt)).ToList();
    }
}
