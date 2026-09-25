using CargoTrustServer.Audit;
using CargoTrustServer.Common;
using CargoTrustServer.Data;
using CargoTrustServer.Statistics;
using CargoTrustServer.Transactions;
using CargoTrustServer.Users;
using Microsoft.EntityFrameworkCore;

namespace CargoTrustServer.Companies;

/// <summary>거래처 — 검색 · 상세 · 후기 · 등록</summary>
public static class CompanyEndpoints
{
    private const int SearchLimit = 50;

    public static void MapCompanyEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/companies").WithTags("Company");

        group.MapGet("/search", Search).WithSummary("거래처 검색");
        group.MapGet("/by-number/{bizno}", ByNumber).WithSummary("사업자번호로 한 곳 (등록 전 중복 확인)");
        group.MapGet("/{id:long}", Detail).WithSummary("거래처 상세 — 통계 · 최근 거래");
        group.MapGet("/{id:long}/reviews", Reviews).WithSummary("거래처 후기 (공개분)");
        group.MapPost("/", Create).WithSummary("거래처 등록");
    }

    // field: all(기본) · name · bizno · ceo · phone · address
    private static async Task<IResult> Search(
        CargoTrustDbContext db, CurrentUser me, CompanyStatsService stats,
        string? q, string? field, CancellationToken ct)
    {
        var text = Check.Clean(q);
        field = string.IsNullOrWhiteSpace(field) ? "all" : field.Trim().ToLowerInvariant();

        var query = db.Companies.AsNoTracking();
        if (!me.IsAdmin) query = query.Where(c => c.Status != CompanyStatus.HIDDEN);

        string? bizKey = null;
        string? startsWith = null;

        if (text != null)
        {
            bizKey = BusinessNumber.AsSearchKey(text);
            var pattern = "%" + EscapeLike(text) + "%";
            // 전화번호는 하이픈을 섞어 적기도 안 적기도 한다 — 숫자만 맞대어 본다.
            var phoneDigits = new string(text.Where(char.IsAsciiDigit).ToArray());
            var phonePattern = phoneDigits.Length >= 3 ? "%" + phoneDigits + "%" : null;

            switch (field)
            {
                case "name":
                    query = query.Where(c => EF.Functions.ILike(c.CompanyName, pattern, "\\"));
                    break;
                case "bizno":
                    // 번호는 **전체로만** 찾는다. 앞자리 몇 개로 찾게 두면 가린 뒤 5자리를
                    // 검색을 되풀이해 알아낼 수 있다(설계안 29).
                    if (bizKey is null) return ApiError.BadRequest("사업자번호 검색은 10자리 전체로 합니다.");
                    query = query.Where(c => c.BusinessNumber == bizKey);
                    break;
                case "ceo":
                    query = query.Where(c => c.CeoName != null && EF.Functions.ILike(c.CeoName, pattern, "\\"));
                    break;
                case "phone":
                    query = phonePattern is null
                        ? query.Where(c => c.Phone != null && EF.Functions.ILike(c.Phone, pattern, "\\"))
                        : query.Where(c => c.Phone != null && EF.Functions.ILike(c.Phone.Replace("-", "").Replace(" ", ""), phonePattern));
                    break;
                case "address":
                    query = query.Where(c => c.Address != null && EF.Functions.ILike(c.Address, pattern, "\\"));
                    break;
                case "all":
                    query = query.Where(c =>
                        (bizKey != null && c.BusinessNumber == bizKey)
                        || EF.Functions.ILike(c.CompanyName, pattern, "\\")
                        || (c.CeoName != null && EF.Functions.ILike(c.CeoName, pattern, "\\"))
                        || (c.Address != null && EF.Functions.ILike(c.Address, pattern, "\\"))
                        || (c.Phone != null && EF.Functions.ILike(c.Phone, pattern, "\\"))
                        || (phonePattern != null && c.Phone != null
                            && EF.Functions.ILike(c.Phone.Replace("-", "").Replace(" ", ""), phonePattern)));
                    break;
                default:
                    return ApiError.BadRequest("field 는 all · name · bizno · ceo · phone · address 중 하나입니다.");
            }
            
            startsWith = EscapeLike(text) + "%";
        }
        else
        {
            if (field == "bizno")
                return ApiError.BadRequest("사업자번호 검색은 10자리 전체로 합니다.");
            if (field != "all" && field != "name" && field != "ceo" && field != "phone" && field != "address")
                return ApiError.BadRequest("field 는 all · name · bizno · ceo · phone · address 중 하나입니다.");
        }

        // 번호가 정확히 맞은 곳을 맨 앞에, 그다음 이름이 검색어로 시작하는 곳.
        var companies = await query
            .OrderByDescending(c => bizKey != null && c.BusinessNumber == bizKey)
            .ThenByDescending(c => startsWith != null && EF.Functions.ILike(c.CompanyName, startsWith, "\\"))
            .ThenBy(c => c.CompanyName)
            .ThenBy(c => c.CompanyId)
            .Take(SearchLimit)
            .ToListAsync(ct);

        var statMap = await stats.ForCompaniesAsync(companies.Select(c => c.CompanyId).ToList(), null, ct);
        return Results.Ok(companies.Select(c => CompanyMap.Summary(c, me.IsAdmin, statMap[c.CompanyId])).ToList());
    }

    private static async Task<IResult> ByNumber(CargoTrustDbContext db, CurrentUser me, string bizno, CancellationToken ct)
    {
        if (!BusinessNumber.TryNormalize(bizno, out var digits, out var error))
            return ApiError.BadRequest(error);

        // HIDDEN 회사도 돌려준다. 이 조회는 「등록해도 되나」를 묻는 것이라, 숨김 회사를
        // 「없다」고 답하면 곧이어 등록이 409 로 튕겨 앞뒤가 안 맞는다. status 칸이 HIDDEN 을 알린다.
        var company = await db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.BusinessNumber == digits, ct);
        return company is null ? ApiError.Empty() : Results.Ok(CompanyMap.Info(company, me.IsAdmin));
    }

    private static async Task<IResult> Detail(
        CargoTrustDbContext db, CurrentUser me, CompanyStatsService stats,
        long id, string? period, CancellationToken ct)
    {
        if (!CompanyStatsService.TryParsePeriod(period, out var periodDays))
            return ApiError.BadRequest("period 는 30 · 90 · 180 · 365 · all 중 하나입니다.");

        var company = await db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.CompanyId == id, ct);
        if (company is null || (company.Status == CompanyStatus.HIDDEN && !me.IsAdmin))
            return ApiError.NotFound("거래처를 찾을 수 없습니다.");

        // 홈의 「최근 본 거래처」. 한 사람·한 회사는 한 줄 — 다시 보면 시각만 올린다.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO cargotrust.company_view (user_id, company_id, viewed_at)
            VALUES ({me.UserId}, {id}, now())
            ON CONFLICT (user_id, company_id) DO UPDATE SET viewed_at = EXCLUDED.viewed_at
            """, ct);

        var rows = await stats.LoadRowsAsync([id], ct);
        var today = KstDate.Today;

        var counted = stats.CountedTransactions().AsNoTracking().Where(t => t.CompanyId == id);
        var recent = await counted
            .OrderByDescending(t => t.TransportDate).ThenByDescending(t => t.TransactionId)
            .Take(20).ToListAsync(ct);
        var recentUnpaid = await counted
            .Where(t => t.PaymentStatus == PaymentStatus.UNPAID)
            .OrderByDescending(t => t.TransportDate).ThenByDescending(t => t.TransactionId)
            .Take(5).ToListAsync(ct);

        var mine = await db.Transactions.CountAsync(t => t.CompanyId == id && t.UserId == me.UserId && !t.IsDeleted, ct);

        return Results.Ok(new CompanyDetailDto(
            CompanyMap.Info(company, me.IsAdmin),
            CompanyStatsService.Compute(rows, periodDays, today),
            CompanyStatsService.StandardPeriods.Select(p => CompanyStatsService.Compute(rows, p, today)).ToList(),
            recent.Select(TransactionMap.Public).ToList(),
            recentUnpaid.Select(TransactionMap.Public).ToList(),
            mine));
    }

    private static async Task<IResult> Reviews(CargoTrustDbContext db, CurrentUser me, long id, CancellationToken ct)
    {
        var company = await db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.CompanyId == id, ct);
        if (company is null || (company.Status == CompanyStatus.HIDDEN && !me.IsAdmin))
            return ApiError.NotFound("거래처를 찾을 수 없습니다.");

        // 통계에서 빠진 거래(삭제 · HIDDEN)의 후기도 함께 뺀다 — 숨긴 거래의 후기가 남아
        // 보이면 숨긴 뜻이 없다.
        var reviews = await db.Reviews.AsNoTracking()
            .Where(r => r.Status == ReviewVisibility.VISIBLE
                        && r.Transaction.CompanyId == id
                        && !r.Transaction.IsDeleted
                        && r.Transaction.ReviewStatus != ReviewStatus.HIDDEN)
            .OrderByDescending(r => r.CreatedAt)
            .Take(500)
            .Select(r => new PublicReviewDto(r.ReviewId, r.Transaction.TransportDate, r.Transaction.Amount,
                r.Transaction.PaymentStatus, r.Content, r.CreatedAt))
            .ToListAsync(ct);
        return Results.Ok(reviews);
    }

    private static async Task<IResult> Create(
        CargoTrustDbContext db, CurrentUser me, AuditService audit,
        CompanyCreateRequest req, CancellationToken ct)
    {
        if (CompanyInput.Validate(req, out var digits) is { } error)
            return ApiError.BadRequest(error);

        if (await db.Companies.AnyAsync(c => c.BusinessNumber == digits, ct))
            return ApiError.Conflict("이미 등록된 사업자번호입니다");

        var company = new Company
        {
            BusinessNumber = digits,
            Status = CompanyStatus.ACTIVE,
            CreatedBy = me.UserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        CompanyInput.Apply(company, req);
        db.Companies.Add(company);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // 확인과 저장 사이에 다른 사람이 같은 번호를 넣었다 (uq_company_business_number).
            return ApiError.Conflict("이미 등록된 사업자번호입니다");
        }

        audit.Add("COMPANY_CREATE", AuditTarget.Company, company.CompanyId, null, audit.Snapshot(company));
        await db.SaveChangesAsync(ct);

        return Results.Ok(CompanyMap.Info(company, me.IsAdmin));
    }

    /// <summary>LIKE 의 와일드카드(% _)와 탈출 문자를 글자 그대로로.</summary>
    public static string EscapeLike(string text) =>
        text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
