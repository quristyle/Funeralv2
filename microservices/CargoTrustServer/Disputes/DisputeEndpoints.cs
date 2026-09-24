using CargoTrustServer.Common;
using CargoTrustServer.Data;
using CargoTrustServer.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CargoTrustServer.Disputes;

/// <summary>
/// 업체 이의제기(설계안 14). 차주가 적은 거래에 그 회사가 「사실과 다르다」를 걸 수 있는 길이다 —
/// 이 길이 없으면 한쪽 말만 쌓이는 서비스가 된다.
/// </summary>
public static class DisputeEndpoints
{
    public static void MapDisputeEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/disputes").WithTags("Dispute");

        group.MapPost("/", Create).WithSummary("이의제기 (거래처에 연결된 운송사 · 관리자)");
        group.MapGet("/mine", Mine).WithSummary("내 이의제기");
        group.MapGet("/{id:long}", Get).WithSummary("이의제기 한 건 (신청자 · 관리자 · 그 거래의 등록자)");
    }

    private static async Task<IResult> Create(
        CargoTrustDbContext db, CurrentUser me, DisputeRequest req, CancellationToken ct)
    {
        if (req.TransactionId is not { } transactionId)
            return ApiError.BadRequest("이의를 걸 거래(transactionId)를 고르세요.");
        if (!Code.TryParse<DisputeReason>(req.Reason, out var reason) || reason is null)
            return ApiError.BadRequest($"reason 은 {Code.Allowed<DisputeReason>()} 중 하나입니다.");
        if (Check.MaxLength(req.Content, 2000, "이의 내용") is { } lengthError)
            return ApiError.BadRequest(lengthError);

        var t = await db.Transactions.Include(x => x.Company)
            .FirstOrDefaultAsync(x => x.TransactionId == transactionId && !x.IsDeleted, ct);
        if (t is null) return ApiError.NotFound("거래를 찾을 수 없습니다.");

        // 자기 회사의 거래에만 건다. 아무 운송사나 남의 거래에 이의를 걸 수 있으면
        // 경쟁사 흠집 내기에 쓰인다.
        var isCompanyCarrier = me.UserType == UserType.CARRIER && me.Entity.CompanyId == t.CompanyId;
        if (!isCompanyCarrier && !me.IsAdmin)
            return ApiError.Forbidden("이 거래처에 연결된 운송사 사용자만 이의를 제기할 수 있습니다.");

        var open = await db.Disputes.AnyAsync(d =>
            d.TransactionId == transactionId
            && (d.Status == DisputeStatus.RECEIVED || d.Status == DisputeStatus.REVIEWING), ct);
        if (open) return ApiError.Conflict("이 거래에 처리 중인 이의제기가 이미 있습니다.");

        var dispute = new TransactionDispute
        {
            TransactionId = t.TransactionId,
            CompanyId = t.CompanyId,
            RequesterUserId = me.UserId,
            Reason = reason.Value,
            Content = Check.Clean(req.Content),
            Status = DisputeStatus.RECEIVED,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Disputes.Add(dispute);
        await db.SaveChangesAsync(ct);

        dispute.Transaction = t;
        dispute.Company = t.Company;
        return Results.Ok(DisputeMap.Dispute(dispute));
    }

    private static async Task<IResult> Mine(
        CargoTrustDbContext db, CurrentUser me, IOptions<CargoTrustOptions> options, CancellationToken ct)
    {
        var list = await db.Disputes.AsNoTracking()
            .Include(d => d.Transaction).Include(d => d.Company)
            .Where(d => d.RequesterUserId == me.UserId)
            .OrderByDescending(d => d.CreatedAt)
            .Take(options.Value.ListLimit)
            .ToListAsync(ct);
        return Results.Ok(list.Select(DisputeMap.Dispute).ToList());
    }

    private static async Task<IResult> Get(CargoTrustDbContext db, CurrentUser me, long id, CancellationToken ct)
    {
        var d = await db.Disputes.AsNoTracking()
            .Include(x => x.Transaction).Include(x => x.Company)
            .FirstOrDefaultAsync(x => x.DisputeId == id, ct);
        if (d is null) return ApiError.NotFound("이의제기를 찾을 수 없습니다.");

        // 거래를 등록한 차주도 본다 — 자기 기록에 무엇이 걸렸는지는 알아야 한다.
        var allowed = d.RequesterUserId == me.UserId || me.IsAdmin || d.Transaction.UserId == me.UserId;
        if (!allowed) return ApiError.Forbidden("이 이의제기를 볼 수 없습니다.");

        return Results.Ok(DisputeMap.Dispute(d));
    }
}
