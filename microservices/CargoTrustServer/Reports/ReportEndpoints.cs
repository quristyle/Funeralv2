using CargoTrustServer.Common;
using CargoTrustServer.Data;
using CargoTrustServer.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CargoTrustServer.Reports;

/// <summary>신고 — 거래처 · 거래 · 후기 (설계안 15)</summary>
public static class ReportEndpoints
{
    public static void MapReportEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/reports").WithTags("Report");

        group.MapPost("/", Create).WithSummary("신고하기");
        group.MapGet("/mine", Mine).WithSummary("내 신고");
        group.MapGet("/{id:long}", Get).WithSummary("신고 한 건 (신고자 · 관리자)");
    }

    private static async Task<IResult> Create(
        CargoTrustDbContext db, CurrentUser me, ReportRequest req, CancellationToken ct)
    {
        if (!Code.TryParse<ReportTarget>(req.TargetType, out var targetType) || targetType is null)
            return ApiError.BadRequest($"targetType 은 {Code.Allowed<ReportTarget>()} 중 하나입니다.");
        if (req.TargetId is not { } targetId)
            return ApiError.BadRequest("신고 대상(targetId)을 고르세요.");
        if (!Code.TryParse<ReportReason>(req.Reason, out var reason) || reason is null)
            return ApiError.BadRequest($"reason 은 {Code.Allowed<ReportReason>()} 중 하나입니다.");
        if (Check.MaxLength(req.Content, 2000, "신고 내용") is { } lengthError)
            return ApiError.BadRequest(lengthError);

        if (!await ReportTargets.ExistsAsync(db, targetType.Value, targetId, ct))
            return ApiError.BadRequest("신고 대상을 찾을 수 없습니다.");

        // 처리 안 된 신고가 있는데 또 넣으면 관리자 목록에 같은 것이 쌓일 뿐이다.
        var open = await db.Reports.AnyAsync(r =>
            r.ReporterUserId == me.UserId && r.TargetType == targetType && r.TargetId == targetId
            && (r.Status == ReportStatus.RECEIVED || r.Status == ReportStatus.REVIEWING), ct);
        if (open) return ApiError.Conflict("같은 대상에 처리 중인 신고가 이미 있습니다.");

        var report = new Report
        {
            TargetType = targetType.Value,
            TargetId = targetId,
            ReporterUserId = me.UserId,
            Reason = reason.Value,
            Content = Check.Clean(req.Content),
            Status = ReportStatus.RECEIVED,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Reports.Add(report);
        await db.SaveChangesAsync(ct);

        var summaries = await ReportTargets.SummarizeAsync(db, [report], me.IsAdmin, ct);
        return Results.Ok(ReportMap.Report<ReportDto>(report, summaries.Get(report)));
    }

    private static async Task<IResult> Mine(
        CargoTrustDbContext db, CurrentUser me, IOptions<CargoTrustOptions> options, CancellationToken ct)
    {
        var reports = await db.Reports.AsNoTracking()
            .Where(r => r.ReporterUserId == me.UserId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(options.Value.ListLimit)
            .ToListAsync(ct);
        var summaries = await ReportTargets.SummarizeAsync(db, reports, me.IsAdmin, ct);
        return Results.Ok(reports.Select(r => ReportMap.Report<ReportDto>(r, summaries.Get(r))).ToList());
    }

    private static async Task<IResult> Get(CargoTrustDbContext db, CurrentUser me, long id, CancellationToken ct)
    {
        var report = await db.Reports.AsNoTracking().FirstOrDefaultAsync(r => r.ReportId == id, ct);
        if (report is null) return ApiError.NotFound("신고를 찾을 수 없습니다.");
        if (report.ReporterUserId != me.UserId && !me.IsAdmin)
            return ApiError.Forbidden("본인이 한 신고만 볼 수 있습니다.");

        var summaries = await ReportTargets.SummarizeAsync(db, [report], me.IsAdmin, ct);
        return Results.Ok(ReportMap.Report<ReportDto>(report, summaries.Get(report)));
    }
}
