using CargoTrustServer.Common;
using CargoTrustServer.Companies;
using CargoTrustServer.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CargoTrustServer.Toll;

/// <summary>
/// 톨게이트 심야할인 — <c>/toll/*</c>.
///
/// <para>
/// 계산 둘(<c>calc</c> · <c>suggest</c>)이 알맹이고 나머지는 그 둘을 쓰기 좋게
/// 하는 것들이다. 규칙(<c>rules</c>)을 내보내는 까닭은 화면이 「무엇을 기준으로
/// 셌는지」를 결과 옆에 적어야 하기 때문이다 — 숫자만 띄우면 그 숫자가
/// 어느 제도의 것인지 아무도 모른다.
/// </para>
/// </summary>
public static class TollEndpoints
{
    public static void MapTollEndpoints(this RouteGroupBuilder api)
    {
        var toll = api.MapGroup("/toll").WithTags("Toll");

        toll.MapGet("/rules", Rules).WithSummary("지금 쓰는 할인 규칙 — 야간창과 비율 띠");
        toll.MapGet("/plazas", Plazas).WithSummary("영업소 검색 — 구간 유형(개방식/폐쇄식)이 함께 온다");
        toll.MapPost("/discount/calc", Calc).WithSummary("① 진입·진출 시각 → 심야할인율");
        toll.MapPost("/discount/suggest", Suggest).WithSummary("② 한쪽 시각 + 목표 할인율 → 나머지 시각 추천");
        toll.MapGet("/history", History).WithSummary("내 계산 이력");
    }

    private static async Task<IResult> Rules(TollService toll, CancellationToken ct) =>
        Results.Ok(await toll.RulesAsync(ct));

    private static async Task<IResult> Calc(TollService toll, TollCalcRequest req, CancellationToken ct) =>
        await toll.CalcAsync(req, ct);

    private static async Task<IResult> Suggest(TollService toll, TollSuggestRequest req, CancellationToken ct) =>
        await toll.SuggestAsync(req, ct);

    private static async Task<IResult> History(TollService toll, int? limit, CancellationToken ct) =>
        Results.Ok(await toll.HistoryAsync(limit ?? 30, ct));

    /// <summary>
    /// 영업소 검색. 이름 조각으로 찾고, 안 주면 앞에서부터 상한만큼 준다.
    ///
    /// <para>
    /// 민자 구간은 요금·할인 체계가 따로라 기본으로 뺀다(<c>includePrivate</c> 로 켠다).
    /// 섞어 놓으면 사용자가 고른 영업소가 우리가 세지 못하는 구간일 수 있고,
    /// 그때 틀리는 방향이 <b>할인이 되는 줄 알았는데 안 되는</b> 쪽이다.
    /// </para>
    /// </summary>
    private static async Task<IResult> Plazas(
        CargoTrustDbContext db, IOptions<CargoTrustOptions> options,
        string? q, bool? includePrivate, CancellationToken ct)
    {
        // **bool? 이어야 한다.** 최소 API 는 널 못 받는 값 타입 질의 인자를 「반드시
        // 와야 하는 것」으로 보고, 안 오면 봉투도 없는 맨 400 을 돌려준다 —
        // 화면에는 까닭 없는 실패로만 보인다.
        var query = db.TollPlazas.AsNoTracking().Where(p => p.IsActive);
        if (includePrivate != true) query = query.Where(p => !p.IsPrivate);

        if (Check.Clean(q) is { } text)
        {
            var pattern = "%" + CompanyEndpoints.EscapeLike(text) + "%";
            query = query.Where(p =>
                EF.Functions.ILike(p.UnitName, pattern, "\\")
                || EF.Functions.ILike(p.UnitCode, pattern, "\\")
                || (p.RouteName != null && EF.Functions.ILike(p.RouteName, pattern, "\\")));
        }

        // 코드값을 문자열로 바꾸는 일은 DB 가 못 한다(enum.ToString 은 번역되지 않는다).
        // 줄을 먼저 읽고 여기서 옮긴다.
        var rows = await query
            .OrderBy(p => p.UnitName)
            .Take(options.Value.ListLimit)
            .ToListAsync(ct);

        return Results.Ok(rows.Select(TollMap.Plaza).ToList());
    }
}

/// <summary>영업소 줄을 바깥 모양으로 옮기는 한 곳 — 사용자·관리자 양쪽이 쓴다.</summary>
public static class TollMap
{
    public static TollPlazaDto Plaza(TollPlaza p) => new(
        p.PlazaId, p.UnitCode, p.UnitName, p.RouteNo, p.RouteName,
        p.SectionType.ToString(), p.IsPrivate, p.Lat, p.Lon);
}
