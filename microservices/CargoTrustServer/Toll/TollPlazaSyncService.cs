using System.Text.Json;
using CargoTrustServer.Common;
using CargoTrustServer.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CargoTrustServer.Toll;

/// <summary>설정 <c>Toll:*</c></summary>
public class TollOptions
{
    public const string Section = "Toll";

    /// <summary>
    /// 영업소 목록을 주는 공개 API 의 주소.
    /// 자리표시자는 <c>{key}</c> 인증키 · <c>{page}</c> 쪽 번호 · <c>{rows}</c> 쪽 크기.
    ///
    /// <para>
    /// 기본값은 고속도로 공공데이터 포털(data.ex.co.kr)의 <b>상·하행 영업소 목록</b>
    /// (<c>basicinfo/updownIcList</c>)이다. 2026-10-06 실측으로 477건이 오고
    /// <c>unitCode</c> 가 전국에서 유일하다 — 그래서 그 값을 열쇠로 쓴다.
    /// </para>
    ///
    /// <para>
    /// 다른 출처로 바꿔도 된다. 읽는 쪽이 칸 이름을 <b>여러 후보로</b> 찾고
    /// 줄 배열도 봉투 모양에 기대지 않고 찾으므로, 주소만 갈아 끼우면 된다.
    /// </para>
    /// </summary>
    public string PlazaListUrl { get; set; } =
        "https://data.ex.co.kr/openapi/basicinfo/updownIcList?key={key}&type=json&numOfRows={rows}&pageNo={page}";

    /// <summary>
    /// 영업소간 통행요금을 주는 주소. 자리표시자는 <c>{key}</c> · <c>{from}</c> · <c>{to}</c>.
    ///
    /// <para>
    /// 같은 포털(data.ex.co.kr)이고 <b>같은 인증키</b>를 쓴다 — 키를 따로 받을 필요가 없다.
    /// </para>
    /// </summary>
    public string FareUrl { get; set; } =
        "https://data.ex.co.kr/openapi/toll/bhoinstIntoTollList?key={key}&type=json&numOfRows=5&pageNo=1&dprtrTolofCd={from}&arrvTolofCd={to}";

    /// <summary>공공데이터 인증키. 없으면 동기화를 건너뛰고 까닭을 남긴다.</summary>
    public string? ServiceKey { get; set; }

    /// <summary>
    /// 한 쪽에 달라고 할 줄 수.
    ///
    /// <para>
    /// <b>달라는 만큼 다 주지 않는다.</b> 1000 을 줘도 99건만 왔다(2026-10-06 실측).
    /// 그래서 이 값은 희망이고, 실제 멈추는 자리는 「새 줄이 더 안 나오는 쪽」이다.
    /// </para>
    /// </summary>
    public int PageRows { get; set; } = 100;

    /// <summary>훑을 최대 쪽 수. 쪽 번호를 무시하는 API 를 만나도 돌지 않게 하는 빗장.</summary>
    public int MaxPages { get; set; } = 50;

    /// <summary>받아 올 최대 줄 수. 터무니없는 응답으로 표를 채우지 않게.</summary>
    public int MaxRows { get; set; } = 5000;
}

/// <summary>
/// 영업소를 바깥에서 받아 <b>우리 표에 보관</b>한다.
///
/// <para>
/// [왜 실시간으로 부르지 않는가]
/// </para>
///
/// <para>
/// 영업소는 1년에 몇 줄 바뀐다. 그런데 계산 화면은 하루에도 여러 번 열린다.
/// 매번 바깥을 부르면 느린 것은 둘째 치고 — <b>바깥이 죽으면 우리 화면이 함께
/// 죽는다.</b> 받아서 보관하면 바깥이 조용해도 어제 자료로 계속 돈다.
/// </para>
///
/// <para>
/// [칸 이름을 하나로 못 박지 않는다]
/// </para>
///
/// <para>
/// 같은 자료를 공공데이터포털과 고속도로 포털이 서로 다른 칸 이름으로 준다
/// (<c>unitCode</c> · <c>unitNo</c> · <c>tcsUnitCode</c> …). 하나로 박아 두면
/// 출처를 바꾸는 순간 조용히 0건이 들어온다. 후보를 늘어놓고 <b>처음 찾은 것</b>을
/// 쓰고, 열쇠가 되는 칸을 못 찾으면 그 줄은 건너뛰고 수를 센다.
/// </para>
///
/// <para>
/// [개방식·폐쇄식을 못 알아내면 폐쇄식으로 둔다]
/// </para>
///
/// <para>
/// 모르는 채로 두면 계산이 아예 안 되고, 개방식으로 두면 <b>아닌 구간에 일괄
/// 50% 가 붙는다.</b> 둘 중 덜 나쁜 쪽은 폐쇄식이다 — 비율대로 세므로 틀려도
/// 할인이 과하게 나오지 않는다. 몇 줄이 그렇게 들어왔는지는 결과에 적는다.
/// </para>
/// </summary>
public class TollPlazaSyncService(
    HttpClient http,
    CargoTrustDbContext db,
    IOptions<TollOptions> options,
    ILogger<TollPlazaSyncService> logger)
{
    private static readonly string[] CodeKeys = ["unitCode", "unitNo", "tcsUnitCode", "unitcode", "UNIT_CODE", "unitId"];
    private static readonly string[] NameKeys = ["unitName", "unitNm", "tcsUnitName", "unitname", "UNIT_NAME"];
    private static readonly string[] RouteNoKeys = ["routeNo", "routeNum", "tcsRouteNo", "ROUTE_NO"];
    private static readonly string[] RouteNameKeys = ["routeName", "routeNm", "ROUTE_NAME"];
    private static readonly string[] LatKeys = ["yValue", "latitude", "lat", "yPos", "Y_VALUE"];
    private static readonly string[] LonKeys = ["xValue", "longitude", "lon", "xPos", "X_VALUE"];

    /// <summary>개방식/폐쇄식을 가리키는 칸. 값이 「개방」이면 개방식이다.</summary>
    private static readonly string[] SectionKeys = ["opratType", "operationType", "unitOprType", "busiTypeCode", "busiType", "OPRAT_TYPE"];

    /// <summary>민자 여부. 값에 「민자」가 들어 있으면 민자로 본다.</summary>
    private static readonly string[] PrivateKeys = ["exprwyOperInstCode", "operInstNm", "operatorName", "mngInstNm"];

    /// <summary>인증키가 쓸 만한가. 추적 파일의 자리표시자는 미설정으로 본다.</summary>
    public bool HasKey =>
        !string.IsNullOrWhiteSpace(options.Value.ServiceKey) && !options.Value.ServiceKey!.StartsWith("__");

    /// <summary>
    /// 바깥에서 받아 와 표에 반영한다. <b>쪽을 넘겨 가며</b> 끝까지 받는다.
    ///
    /// <para>
    /// 멈추는 자리를 「받은 줄이 0」이 아니라 <b>「새 영업소코드가 하나도 없음」</b>으로
    /// 잡았다. 쪽 번호를 무시하고 늘 첫 쪽을 주는 API 를 만나면 앞엣것은 영영 돌지만
    /// 뒤엣것은 두 번째 쪽에서 멈춘다. 쪽 수 빗장(<c>MaxPages</c>)은 그 위의 보험이다.
    /// </para>
    /// </summary>
    public async Task<TollPlazaSyncResult> SyncAsync(CancellationToken ct)
    {
        if (!HasKey)
        {
            logger.LogWarning("Toll:ServiceKey 가 없어 영업소 동기화를 건너뛴다.");
            return new TollPlazaSyncResult(0, 0, 0, 0, "EX_API",
                "공공데이터 인증키(Toll:ServiceKey)가 설정되지 않았습니다. "
                + "키를 넣거나, 내려받은 영업소 자료를 올리기(POST /admin/toll/plazas)로 보관하십시오.");
        }

        var key = Uri.EscapeDataString(options.Value.ServiceKey!);
        var rows = new List<TollPlazaUpsert>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pages = 0;

        for (var page = 1; page <= options.Value.MaxPages; page++)
        {
            var url = options.Value.PlazaListUrl
                .Replace("{key}", key)
                .Replace("{page}", page.ToString())
                .Replace("{rows}", options.Value.PageRows.ToString());

            string body;
            try
            {
                using var response = await http.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode)
                {
                    // 첫 쪽부터 막히면 못 받은 것이고, 중간이면 받은 데까지는 쓴다.
                    var reason = $"영업소 목록을 받지 못했습니다(HTTP {(int)response.StatusCode}).";
                    logger.LogWarning("영업소 동기화 — {Page}쪽에서 응답 {Status}", page, (int)response.StatusCode);
                    if (rows.Count == 0) return new TollPlazaSyncResult(0, 0, 0, 0, "EX_API", reason);
                    break;
                }
                body = await response.Content.ReadAsStringAsync(ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                logger.LogWarning(ex, "영업소 동기화 — {Page}쪽에서 바깥에 닿지 못했다", page);
                if (rows.Count == 0)
                    return new TollPlazaSyncResult(0, 0, 0, 0, "EX_API", $"영업소 목록을 받지 못했습니다 — {ex.Message}");
                break;
            }

            List<TollPlazaUpsert> got;
            try
            {
                got = Parse(body, options.Value.MaxRows);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "영업소 동기화 — {Page}쪽 응답을 읽지 못했다", page);
                if (rows.Count == 0)
                    return new TollPlazaSyncResult(0, 0, 0, 0, "EX_API", $"응답을 읽지 못했습니다 — {ex.Message}");
                break;
            }

            pages = page;
            var fresh = got.Where(r => seen.Add(r.UnitCode)).ToList();
            if (fresh.Count == 0) break;

            rows.AddRange(fresh);
            if (rows.Count >= options.Value.MaxRows) break;
        }

        var applied = await ApplyAsync(rows, "EX_API", ct);
        logger.LogInformation("영업소 동기화 — {Pages}쪽에서 {Received}건, 새로 {Created}, 고침 {Updated}, 건너뜀 {Skipped}",
            pages, applied.Received, applied.Created, applied.Updated, applied.Skipped);
        return applied with
        {
            Message = $"{pages}쪽에서 {applied.Received}건 — 새로 {applied.Created}건, "
                      + $"고침 {applied.Updated}건, 건너뜀 {applied.Skipped}건.",
        };
    }

    /// <summary>
    /// 받아 온(또는 손으로 올린) 줄을 표에 반영한다. <b>지우지 않는다</b> —
    /// 한 번 받은 영업소가 다음 응답에 안 보인다고 지우면, 응답이 반쪽만 와도
    /// 표가 반쪽이 된다. 안 쓰는 줄은 <c>is_active</c> 로 끈다.
    /// </summary>
    public async Task<TollPlazaSyncResult> ApplyAsync(
        IReadOnlyList<TollPlazaUpsert> rows, string source, CancellationToken ct)
    {
        var created = 0;
        var updated = 0;
        var skipped = 0;
        var now = DateTimeOffset.UtcNow;

        var codes = rows.Select(r => r.UnitCode).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
        var existing = await db.TollPlazas.Where(p => codes.Contains(p.UnitCode)).ToDictionaryAsync(p => p.UnitCode, ct);

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.UnitCode) || string.IsNullOrWhiteSpace(row.UnitName))
            {
                skipped++;
                continue;
            }

            Code.TryParse<SectionType>(row.SectionType, out var parsed);
            var section = parsed ?? SectionType.CLOSED;

            if (existing.TryGetValue(row.UnitCode, out var plaza))
            {
                plaza.UnitName = row.UnitName;
                plaza.RouteNo = row.RouteNo;
                plaza.RouteName = row.RouteName;
                plaza.SectionType = section;
                plaza.IsPrivate = row.IsPrivate;
                plaza.Lat = row.Lat;
                plaza.Lon = row.Lon;
                plaza.IsActive = row.IsActive;
                plaza.Source = source;
                plaza.SyncedAt = now;
                plaza.UpdatedAt = now;
                updated++;
            }
            else
            {
                var added = new TollPlaza
                {
                    UnitCode = row.UnitCode,
                    UnitName = row.UnitName,
                    RouteNo = row.RouteNo,
                    RouteName = row.RouteName,
                    SectionType = section,
                    IsPrivate = row.IsPrivate,
                    Lat = row.Lat,
                    Lon = row.Lon,
                    IsActive = row.IsActive,
                    Source = source,
                    SyncedAt = now,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                db.TollPlazas.Add(added);
                existing[row.UnitCode] = added;
                created++;
            }
        }

        await db.SaveChangesAsync(ct);
        return new TollPlazaSyncResult(rows.Count, created, updated, skipped, source,
            $"받은 {rows.Count}건 중 새로 {created}건, 고침 {updated}건, 건너뜀 {skipped}건.");
    }

    /// <summary>
    /// 응답 어디에 줄 배열이 있는지 모른다 — 포털마다 봉투가 다르다.
    /// 객체를 훑어 <b>객체들의 배열</b> 중 가장 큰 것을 줄 목록으로 본다.
    /// </summary>
    internal static List<TollPlazaUpsert> Parse(string body, int maxRows)
    {
        using var doc = JsonDocument.Parse(body);
        var best = FindRowArray(doc.RootElement, 0);
        if (best is null) return [];

        var rows = new List<TollPlazaUpsert>();
        foreach (var item in best.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            if (rows.Count >= maxRows) break;

            var code = Str(item, CodeKeys);
            var name = Str(item, NameKeys);
            if (code is null || name is null) continue;

            var sectionRaw = Str(item, SectionKeys);
            var operatorRaw = Str(item, PrivateKeys);

            // 코드가 "002 " 처럼 뒤에 공백을 달고 온다(2026-10-06 실측). 열쇠가 되는
            // 값이라 걷어 내지 않으면 같은 영업소가 두 줄이 된다. 다른 칸도 같이 걷는다.
            rows.Add(new TollPlazaUpsert(
                code.Trim(),
                name.Trim(),
                Str(item, RouteNoKeys)?.Trim(),
                Str(item, RouteNameKeys)?.Trim(),
                LooksOpen(sectionRaw) ? nameof(SectionType.OPEN) : nameof(SectionType.CLOSED),
                LooksPrivate(operatorRaw),
                Dec(item, LatKeys),
                Dec(item, LonKeys)));
        }

        return rows;
    }

    private static JsonElement? FindRowArray(JsonElement element, int depth)
    {
        if (depth > 6) return null;

        if (element.ValueKind == JsonValueKind.Array)
        {
            var objects = 0;
            foreach (var item in element.EnumerateArray())
                if (item.ValueKind == JsonValueKind.Object) objects++;
            return objects > 0 ? element : null;
        }

        if (element.ValueKind != JsonValueKind.Object) return null;

        JsonElement? best = null;
        var bestCount = 0;
        foreach (var property in element.EnumerateObject())
        {
            var found = FindRowArray(property.Value, depth + 1);
            if (found is null) continue;
            var count = found.Value.GetArrayLength();
            if (count > bestCount) { best = found; bestCount = count; }
        }
        return best;
    }

    private static bool LooksOpen(string? raw) =>
        raw is not null && (raw.Contains("개방") || raw.Equals("OPEN", StringComparison.OrdinalIgnoreCase) || raw == "1");

    private static bool LooksPrivate(string? raw) =>
        raw is not null && (raw.Contains("민자") || raw.Contains("민간"));

    private static string? Str(JsonElement item, string[] keys)
    {
        foreach (var key in keys)
        {
            if (!item.TryGetProperty(key, out var value)) continue;
            var text = value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.ToString(),
                _ => null,
            };
            if (!string.IsNullOrWhiteSpace(text)) return text;
        }
        return null;
    }

    private static decimal? Dec(JsonElement item, string[] keys)
    {
        var raw = Str(item, keys);
        return decimal.TryParse(raw, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : null;
    }
}
