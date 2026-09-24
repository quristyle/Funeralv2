using CargoTrustServer.Audit;
using CargoTrustServer.Common;
using CargoTrustServer.Companies;
using CargoTrustServer.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CargoTrustServer.Admin;

/// <summary>관리자 — 거래처</summary>
public static class AdminCompanyEndpoints
{
    public static void MapAdminCompanyEndpoints(this RouteGroupBuilder admin)
    {
        admin.MapGet("/companies", List).WithSummary("거래처 목록 (숨김 포함)");
        admin.MapPost("/companies", Create).WithSummary("거래처 등록");
        admin.MapPut("/companies/{id:long}", Update).WithSummary("거래처 수정 · 숨김 · 폐업");
    }

    private static async Task<IResult> List(
        CargoTrustDbContext db, IOptions<CargoTrustOptions> options, string? q, string? status, CancellationToken ct)
    {
        if (!Code.TryParse<CompanyStatus>(status, out var st))
            return ApiError.BadRequest($"status 는 {Code.Allowed<CompanyStatus>()} 중 하나입니다.");

        var query = db.Companies.AsNoTracking().AsQueryable();
        if (st is { } s) query = query.Where(c => c.Status == s);
        if (Check.Clean(q) is { } text)
        {
            var pattern = "%" + CompanyEndpoints.EscapeLike(text) + "%";
            // 관리자는 번호 일부로도 찾는다 — 가리지 않고 보는 사람이라 뒤 5자리를 알아낼 걱정이 없다.
            var digits = new string(text.Where(char.IsAsciiDigit).ToArray());
            var digitPattern = digits.Length >= 3 ? "%" + digits + "%" : null;
            query = query.Where(c =>
                EF.Functions.ILike(c.CompanyName, pattern, "\\")
                || (c.CeoName != null && EF.Functions.ILike(c.CeoName, pattern, "\\"))
                || (c.Phone != null && EF.Functions.ILike(c.Phone, pattern, "\\"))
                || (c.Address != null && EF.Functions.ILike(c.Address, pattern, "\\"))
                || (digitPattern != null && EF.Functions.Like(c.BusinessNumber, digitPattern)));
        }

        var companies = await query
            .OrderByDescending(c => c.UpdatedAt).ThenByDescending(c => c.CompanyId)
            .Take(options.Value.ListLimit)
            .ToListAsync(ct);
        var counts = await CountsAsync(db, companies.Select(c => c.CompanyId).ToList(), ct);
        return Results.Ok(companies.Select(c => ToDto(c, counts.GetValueOrDefault(c.CompanyId))).ToList());
    }

    private static async Task<IResult> Create(
        CargoTrustDbContext db, AuditService audit, Users.CurrentUser me, AdminCompanySave req, CancellationToken ct)
    {
        if (CompanyInput.Validate(req, out var digits) is { } error) return ApiError.BadRequest(error);
        if (!Code.TryParse<CompanyStatus>(req.Status, out var status))
            return ApiError.BadRequest($"status 는 {Code.Allowed<CompanyStatus>()} 중 하나입니다.");
        if (await db.Companies.AnyAsync(c => c.BusinessNumber == digits, ct))
            return ApiError.Conflict("이미 등록된 사업자번호입니다");

        var now = DateTimeOffset.UtcNow;
        var company = new Company
        {
            BusinessNumber = digits,
            Status = status ?? CompanyStatus.ACTIVE,
            AdminMemo = Check.Clean(req.AdminMemo),
            CreatedBy = me.UserId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        CompanyInput.Apply(company, req);
        db.Companies.Add(company);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return ApiError.Conflict("이미 등록된 사업자번호입니다");
        }

        audit.Add("ADMIN_COMPANY_CREATE", AuditTarget.Company, company.CompanyId, null, audit.Snapshot(company));
        await db.SaveChangesAsync(ct);
        return Results.Ok(ToDto(company, 0));
    }

    private static async Task<IResult> Update(
        CargoTrustDbContext db, AuditService audit, long id, AdminCompanySave req, CancellationToken ct)
    {
        var company = await db.Companies.FirstOrDefaultAsync(c => c.CompanyId == id, ct);
        if (company is null) return ApiError.NotFound("거래처를 찾을 수 없습니다.");

        if (CompanyInput.Validate(req, out var digits) is { } error) return ApiError.BadRequest(error);
        if (!Code.TryParse<CompanyStatus>(req.Status, out var status))
            return ApiError.BadRequest($"status 는 {Code.Allowed<CompanyStatus>()} 중 하나입니다.");
        if (digits != company.BusinessNumber && await db.Companies.AnyAsync(c => c.BusinessNumber == digits && c.CompanyId != id, ct))
            return ApiError.Conflict("이미 등록된 사업자번호입니다");

        var before = audit.Snapshot(company);
        company.BusinessNumber = digits;
        CompanyInput.Apply(company, req);
        if (status is { } s) company.Status = s;
        company.AdminMemo = Check.Clean(req.AdminMemo);
        company.UpdatedAt = DateTimeOffset.UtcNow;
        audit.AddChange("ADMIN_COMPANY_UPDATE", AuditTarget.Company, id, before, company);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return ApiError.Conflict("이미 등록된 사업자번호입니다");
        }

        var counts = await CountsAsync(db, [id], ct);
        return Results.Ok(ToDto(company, counts.GetValueOrDefault(id)));
    }

    private static AdminCompanyDto ToDto(Company c, int transactionCount)
    {
        var dto = CompanyMap.Info<AdminCompanyDto>(c, isAdmin: true);
        dto.AdminMemo = c.AdminMemo;
        dto.TransactionCount = transactionCount;
        dto.UpdatedAt = c.UpdatedAt;
        return dto;
    }

    private static async Task<Dictionary<long, int>> CountsAsync(CargoTrustDbContext db, List<long> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];
        return await db.Transactions.AsNoTracking()
            .Where(t => ids.Contains(t.CompanyId) && !t.IsDeleted)
            .GroupBy(t => t.CompanyId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
    }
}
