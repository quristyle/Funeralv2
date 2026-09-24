using CargoTrustServer.Audit;
using CargoTrustServer.Common;
using CargoTrustServer.Data;
using CargoTrustServer.Disputes;
using CargoTrustServer.Reports;
using CargoTrustServer.Reviews;
using CargoTrustServer.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CargoTrustServer.Admin;

/// <summary>관리자 — 후기 · 신고 · 이의제기 처리</summary>
public static class AdminModerationEndpoints
{
    public static void MapAdminModerationEndpoints(this RouteGroupBuilder admin)
    {
        admin.MapGet("/reviews", Reviews).WithSummary("후기 목록");
        admin.MapPut("/reviews/{id:long}", UpdateReview).WithSummary("후기 공개 · 숨김");
        admin.MapGet("/reports", ReportList).WithSummary("신고 목록");
        admin.MapPut("/reports/{id:long}", ResolveReport).WithSummary("신고 처리");
        admin.MapGet("/disputes", DisputeList).WithSummary("이의제기 목록");
        admin.MapPut("/disputes/{id:long}", ResolveDispute).WithSummary("이의제기 처리");
    }

    // ── 후기 ─────────────────────────────────────────────────

    private static async Task<IResult> Reviews(
        CargoTrustDbContext db, IOptions<CargoTrustOptions> options, string? status, CancellationToken ct)
    {
        if (!Code.TryParse<ReviewVisibility>(status, out var st))
            return ApiError.BadRequest($"status 는 {Code.Allowed<ReviewVisibility>()} 중 하나입니다.");

        var query = LoadReviews(db).AsNoTracking();
        if (st is { } s) query = query.Where(r => r.Status == s);
        var list = await query.OrderByDescending(r => r.CreatedAt).Take(options.Value.ListLimit).ToListAsync(ct);
        return Results.Ok(await ToReviewDtosAsync(db, list, ct));
    }

    private static async Task<IResult> UpdateReview(
        CargoTrustDbContext db, AuditService audit, long id, ReviewStatusRequest req, CancellationToken ct)
    {
        if (!Code.TryParse<ReviewVisibility>(req.Status, out var st) || st is null)
            return ApiError.BadRequest($"status 는 {Code.Allowed<ReviewVisibility>()} 중 하나입니다.");

        var review = await LoadReviews(db).FirstOrDefaultAsync(r => r.ReviewId == id, ct);
        if (review is null) return ApiError.NotFound("후기를 찾을 수 없습니다.");

        var before = audit.Snapshot(review);
        review.Status = st.Value;
        review.UpdatedAt = DateTimeOffset.UtcNow;
        audit.AddChange("ADMIN_REVIEW_UPDATE", AuditTarget.Review, id, before, review);
        await db.SaveChangesAsync(ct);

        return Results.Ok((await ToReviewDtosAsync(db, [review], ct))[0]);
    }

    private static IQueryable<TransactionReview> LoadReviews(CargoTrustDbContext db) =>
        db.Reviews.Include(r => r.Transaction).ThenInclude(t => t.Company).Include(r => r.User);

    private static async Task<List<AdminReviewDto>> ToReviewDtosAsync(CargoTrustDbContext db, List<TransactionReview> list, CancellationToken ct)
    {
        var ids = list.Select(r => r.ReviewId).ToList();
        var reportCounts = await db.Reports.AsNoTracking()
            .Where(r => r.TargetType == ReportTarget.REVIEW && ids.Contains(r.TargetId))
            .GroupBy(r => r.TargetId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        return list.Select(r =>
        {
            var dto = ReviewMap.Review<AdminReviewDto>(r);
            dto.CompanyId = r.Transaction.CompanyId;
            dto.CompanyName = r.Transaction.Company.CompanyName;
            dto.UserName = r.User.DisplayName ?? r.User.ExternalUserId;
            dto.TransportDate = r.Transaction.TransportDate;
            dto.ReportCount = reportCounts.GetValueOrDefault(r.ReviewId);
            return dto;
        }).ToList();
    }

    // ── 신고 ─────────────────────────────────────────────────

    private static async Task<IResult> ReportList(
        CargoTrustDbContext db, IOptions<CargoTrustOptions> options, string? status, CancellationToken ct)
    {
        if (!Code.TryParse<ReportStatus>(status, out var st))
            return ApiError.BadRequest($"status 는 {Code.Allowed<ReportStatus>()} 중 하나입니다.");

        var query = db.Reports.AsNoTracking().Include(r => r.Reporter).AsQueryable();
        if (st is { } s) query = query.Where(r => r.Status == s);
        var list = await query.OrderByDescending(r => r.CreatedAt).Take(options.Value.ListLimit).ToListAsync(ct);
        return Results.Ok(await ToReportDtosAsync(db, list, ct));
    }

    private static async Task<IResult> ResolveReport(
        CargoTrustDbContext db, AuditService audit, CurrentUser me, long id, AdminResolve req, CancellationToken ct)
    {
        if (!Code.TryParse<ReportStatus>(req.Status, out var st) || st is null)
            return ApiError.BadRequest($"status 는 {Code.Allowed<ReportStatus>()} 중 하나입니다.");

        var report = await db.Reports.Include(r => r.Reporter).FirstOrDefaultAsync(r => r.ReportId == id, ct);
        if (report is null) return ApiError.NotFound("신고를 찾을 수 없습니다.");

        var before = audit.Snapshot(report);
        report.Status = st.Value;
        report.Resolution = Check.Clean(req.Resolution);
        StampResolved(report.Status is not (ReportStatus.RECEIVED or ReportStatus.REVIEWING), me.UserId,
            v => report.ResolvedAt = v, v => report.ResolvedBy = v);
        audit.AddChange("ADMIN_REPORT_RESOLVE", AuditTarget.Report, id, before, report);

        if (req.HideTarget)
        {
            var hideError = await HideReportTargetAsync(db, audit, report, ct);
            if (hideError is not null) return ApiError.BadRequest(hideError);
        }

        await db.SaveChangesAsync(ct);
        return Results.Ok((await ToReportDtosAsync(db, [report], ct))[0]);
    }

    /// <summary>신고 대상을 숨긴다 — 거래는 통계에서 빼고(HIDDEN), 후기·회사는 감춘다.</summary>
    private static async Task<string?> HideReportTargetAsync(CargoTrustDbContext db, AuditService audit, Report report, CancellationToken ct)
    {
        switch (report.TargetType)
        {
            case ReportTarget.TRANSACTION:
                var t = await db.Transactions.FirstOrDefaultAsync(x => x.TransactionId == report.TargetId, ct);
                if (t is null) return "신고 대상 거래를 찾을 수 없습니다.";
                HideTransaction(audit, t, "ADMIN_TRANSACTION_HIDE");
                return null;

            case ReportTarget.REVIEW:
                var r = await db.Reviews.FirstOrDefaultAsync(x => x.ReviewId == report.TargetId, ct);
                if (r is null) return "신고 대상 후기를 찾을 수 없습니다.";
                if (r.Status != ReviewVisibility.HIDDEN)
                {
                    var before = audit.Snapshot(r);
                    r.Status = ReviewVisibility.HIDDEN;
                    r.UpdatedAt = DateTimeOffset.UtcNow;
                    audit.AddChange("ADMIN_REVIEW_HIDE", AuditTarget.Review, r.ReviewId, before, r);
                }
                return null;

            case ReportTarget.COMPANY:
                var c = await db.Companies.FirstOrDefaultAsync(x => x.CompanyId == report.TargetId, ct);
                if (c is null) return "신고 대상 거래처를 찾을 수 없습니다.";
                if (c.Status != CompanyStatus.HIDDEN)
                {
                    var before = audit.Snapshot(c);
                    c.Status = CompanyStatus.HIDDEN;
                    c.UpdatedAt = DateTimeOffset.UtcNow;
                    audit.AddChange("ADMIN_COMPANY_HIDE", AuditTarget.Company, c.CompanyId, before, c);
                }
                return null;

            default:
                return "알 수 없는 신고 대상입니다.";
        }
    }

    private static async Task<List<AdminReportDto>> ToReportDtosAsync(CargoTrustDbContext db, List<Report> list, CancellationToken ct)
    {
        var summaries = await ReportTargets.SummarizeAsync(db, list, isAdmin: true, ct);
        return list.Select(r =>
        {
            var dto = ReportMap.Report<AdminReportDto>(r, summaries.Get(r));
            dto.ReporterName = r.Reporter.DisplayName;
            dto.ReporterExternalId = r.Reporter.ExternalUserId;
            return dto;
        }).ToList();
    }

    // ── 이의제기 ─────────────────────────────────────────────

    private static async Task<IResult> DisputeList(
        CargoTrustDbContext db, IOptions<CargoTrustOptions> options, string? status, CancellationToken ct)
    {
        if (!Code.TryParse<DisputeStatus>(status, out var st))
            return ApiError.BadRequest($"status 는 {Code.Allowed<DisputeStatus>()} 중 하나입니다.");

        var query = LoadDisputes(db).AsNoTracking();
        if (st is { } s) query = query.Where(d => d.Status == s);
        var list = await query.OrderByDescending(d => d.CreatedAt).Take(options.Value.ListLimit).ToListAsync(ct);
        return Results.Ok(list.Select(ToDisputeDto).ToList());
    }

    private static async Task<IResult> ResolveDispute(
        CargoTrustDbContext db, AuditService audit, CurrentUser me, long id, AdminResolve req, CancellationToken ct)
    {
        if (!Code.TryParse<DisputeStatus>(req.Status, out var st) || st is null)
            return ApiError.BadRequest($"status 는 {Code.Allowed<DisputeStatus>()} 중 하나입니다.");

        var d = await LoadDisputes(db).FirstOrDefaultAsync(x => x.DisputeId == id, ct);
        if (d is null) return ApiError.NotFound("이의제기를 찾을 수 없습니다.");

        var before = audit.Snapshot(d);
        d.Status = st.Value;
        d.Resolution = Check.Clean(req.Resolution);
        StampResolved(!DisputeMap.IsOpen(d.Status), me.UserId, v => d.ResolvedAt = v, v => d.ResolvedBy = v);
        audit.AddChange("ADMIN_DISPUTE_RESOLVE", AuditTarget.Dispute, id, before, d);

        // 이의가 받아들여졌을 때만 거래를 통계에서 뺀다. 기각된 이의로 거래를 숨기면
        // 회사가 이의만 걸어 불리한 기록을 지우는 길이 된다.
        if (req.HideTarget && d.Status == DisputeStatus.ACCEPTED)
            HideTransaction(audit, d.Transaction, "ADMIN_TRANSACTION_HIDE");

        await db.SaveChangesAsync(ct);
        return Results.Ok(ToDisputeDto(d));
    }

    private static IQueryable<TransactionDispute> LoadDisputes(CargoTrustDbContext db) =>
        db.Disputes
            .Include(d => d.Transaction).ThenInclude(t => t.User)
            .Include(d => d.Company)
            .Include(d => d.Requester);

    private static AdminDisputeDto ToDisputeDto(TransactionDispute d)
    {
        var dto = DisputeMap.Dispute<AdminDisputeDto>(d);
        dto.RequesterName = d.Requester.DisplayName ?? d.Requester.ExternalUserId;
        dto.TransactionOwnerName = d.Transaction.User.DisplayName ?? d.Transaction.User.ExternalUserId;
        return dto;
    }

    // ── 공통 ─────────────────────────────────────────────────

    private static void HideTransaction(AuditService audit, CargoTransaction t, string action)
    {
        if (t.ReviewStatus == ReviewStatus.HIDDEN) return;
        var before = audit.Snapshot(t);
        t.ReviewStatus = ReviewStatus.HIDDEN;
        t.UpdatedAt = DateTimeOffset.UtcNow;
        audit.AddChange(action, AuditTarget.Transaction, t.TransactionId, before, t);
    }

    /// <summary>
    /// 처리 완료로 가면 처리 시각·처리자를 채우고, 접수·검토중으로 되돌리면 비운다 —
    /// 다시 열린 건에 옛 처리 시각이 남아 있으면 「언제 끝났나」를 잘못 읽는다.
    /// </summary>
    private static void StampResolved(bool resolved, long adminUserId, Action<DateTimeOffset?> setAt, Action<long?> setBy)
    {
        setAt(resolved ? DateTimeOffset.UtcNow : null);
        setBy(resolved ? adminUserId : null);
    }
}
