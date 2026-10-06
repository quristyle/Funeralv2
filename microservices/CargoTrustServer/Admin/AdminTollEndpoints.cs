using CargoTrustServer.Common;
using CargoTrustServer.Data;
using CargoTrustServer.Toll;
using Microsoft.EntityFrameworkCore;

namespace CargoTrustServer.Admin;

/// <summary>영업소 보관 현황 — 「동기화가 돌긴 돌았나」를 한눈에.</summary>
public record TollPlazaStatusDto(
    int Total,
    int ClosedCount,
    int OpenCount,
    int PrivateCount,
    int InactiveCount,
    DateTimeOffset? LastSyncedAt,
    bool HasServiceKey,
    string RuleSetCode);

/// <summary>
/// 관리자 — 톨게이트 (<c>/admin/toll/*</c>).
///
/// <para>
/// 영업소를 바깥에서 받아 보관하고, 받아 온 뒤 구간 유형이 틀린 줄을 고친다.
/// 구간 유형만은 손으로 고칠 수 있어야 한다 — <b>그 한 칸이 할인 규칙을
/// 통째로 바꾸는데</b> 바깥 자료에 늘 들어 있다는 보장이 없다.
/// </para>
/// </summary>
public static class AdminTollEndpoints
{
    public static void MapAdminTollEndpoints(this RouteGroupBuilder admin)
    {
        var toll = admin.MapGroup("/toll");

        toll.MapGet("/status", Status).WithSummary("영업소 보관 현황");
        toll.MapPost("/plazas/sync", Sync).WithSummary("공개 API 에서 영업소를 받아 보관한다");
        toll.MapPost("/plazas", Upload).WithSummary("영업소 줄을 직접 올려 보관한다(인증키가 없을 때의 길)");
        toll.MapPut("/plazas/{id:long}", UpdatePlaza).WithSummary("영업소 한 줄 고치기 — 구간 유형·사용 여부");
    }

    private static async Task<IResult> Status(
        CargoTrustDbContext db, TollPlazaSyncService sync, TollRuleStore rules, CancellationToken ct)
    {
        var all = db.TollPlazas.AsNoTracking();
        var set = await rules.CurrentAsync(ct);

        return Results.Ok(new TollPlazaStatusDto(
            await all.CountAsync(ct),
            await all.CountAsync(p => p.SectionType == SectionType.CLOSED, ct),
            await all.CountAsync(p => p.SectionType == SectionType.OPEN, ct),
            await all.CountAsync(p => p.IsPrivate, ct),
            await all.CountAsync(p => !p.IsActive, ct),
            await all.MaxAsync(p => (DateTimeOffset?)p.SyncedAt, ct),
            sync.HasKey,
            set.Code));
    }

    private static async Task<IResult> Sync(TollPlazaSyncService sync, CancellationToken ct) =>
        Results.Ok(await sync.SyncAsync(ct));

    private static async Task<IResult> Upload(
        TollPlazaSyncService sync, List<TollPlazaUpsert> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return ApiError.BadRequest("올릴 줄이 없습니다.");
        if (rows.Count > 10000) return ApiError.BadRequest("한 번에 10,000줄까지 올립니다.");
        return Results.Ok(await sync.ApplyAsync(rows, "MANUAL", ct));
    }

    private static async Task<IResult> UpdatePlaza(
        CargoTrustDbContext db, long id, TollPlazaUpsert req, CancellationToken ct)
    {
        var plaza = await db.TollPlazas.FirstOrDefaultAsync(p => p.PlazaId == id, ct);
        if (plaza is null) return ApiError.NotFound("영업소를 찾을 수 없습니다.");

        if (!Code.TryParse<SectionType>(req.SectionType, out var parsed))
            return ApiError.BadRequest($"sectionType 은 {Code.Allowed<SectionType>()} 중 하나입니다.");

        plaza.UnitName = Check.Clean(req.UnitName) ?? plaza.UnitName;
        plaza.RouteNo = Check.Clean(req.RouteNo);
        plaza.RouteName = Check.Clean(req.RouteName);
        if (parsed is { } section) plaza.SectionType = section;
        plaza.IsPrivate = req.IsPrivate;
        plaza.IsActive = req.IsActive;
        plaza.Source = "MANUAL";
        plaza.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        return Results.Ok(TollMap.Plaza(plaza));
    }
}
