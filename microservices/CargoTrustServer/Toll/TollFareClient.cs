using System.Text.Json;
using CargoTrustServer.Common;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace CargoTrustServer.Toll;

/// <summary>영업소 둘 사이의 통행료·거리·주행시간.</summary>
/// <param name="FromCode">출발 영업소 코드.</param>
/// <param name="FromName">출발 영업소 이름.</param>
/// <param name="ToCode">도착 영업소 코드.</param>
/// <param name="ToName">도착 영업소 이름.</param>
/// <param name="DistanceKm">거리(km).</param>
/// <param name="DriveMinutes">도로공사가 보는 주행시간. 추천의 소요시간 바닥값으로 쓴다.</param>
/// <param name="Fares">차종별 정상요금(원). 열쇠는 <see cref="VehicleClass"/> 다.</param>
public record TollFare(
    string FromCode,
    string FromName,
    string ToCode,
    string ToName,
    decimal DistanceKm,
    int DriveMinutes,
    IReadOnlyDictionary<VehicleClass, int> Fares);

/// <summary>
/// 영업소간 통행요금을 그때그때 바깥에 묻는다. 영업소는 통째로 보관하지만 요금은 쌍이라 35만 줄이므로, 묻는 쌍만 짧게 기억한다.
/// 응답의 할인요금 칸은 0 으로 오므로 할인액은 우리 비율로 곱한다 — 야간 비율로 할인율을 정하는 곳은 NightDiscountEngine 한 군데다.
/// </summary>
public class TollFareClient(
    HttpClient http,
    IMemoryCache cache,
    IOptions<TollOptions> options,
    ILogger<TollFareClient> logger)
{
    /// <summary>
    /// 요금은 몇 해에 한 번 바뀐다. 그래도 하루로 끊는 까닭은 — 올랐을 때
    /// 하루 안에 따라가되, 같은 구간을 되풀이해 묻는 사람에게는 왕복이 없게.
    /// </summary>
    private static readonly TimeSpan CacheFor = TimeSpan.FromHours(24);

    /// <summary>차종 칸 이름. 6번이 경차다 — 1~5종 뒤에 붙어 있어 순서로 짐작하면 틀린다.</summary>
    private static readonly (VehicleClass Class, string Field)[] FareFields =
    [
        (VehicleClass.C1, "nrmlKnd1Amt"),
        (VehicleClass.C2, "nrmlKnd2Amt"),
        (VehicleClass.C3, "nrmlKnd3Amt"),
        (VehicleClass.C4, "nrmlKnd4Amt"),
        (VehicleClass.C5, "nrmlKnd5Amt"),
        (VehicleClass.LIGHT, "nrmlKnd6Amt"),
    ];

    public bool HasKey =>
        !string.IsNullOrWhiteSpace(options.Value.ServiceKey) && !options.Value.ServiceKey!.StartsWith("__");

    /// <summary>못 받으면 null — 통행료가 없다고 할인율 계산까지 막지 않는다.</summary>
    public async Task<TollFare?> GetAsync(string fromCode, string toCode, CancellationToken ct)
    {
        var from = fromCode.Trim();
        var to = toCode.Trim();
        if (from.Length == 0 || to.Length == 0) return null;
        if (!HasKey)
        {
            logger.LogWarning("Toll:ServiceKey 가 없어 통행료를 묻지 않는다.");
            return null;
        }

        var key = $"toll-fare:{from}:{to}";
        if (cache.TryGetValue<TollFare>(key, out var cached)) return cached;

        var url = options.Value.FareUrl
            .Replace("{key}", Uri.EscapeDataString(options.Value.ServiceKey!))
            .Replace("{from}", Uri.EscapeDataString(from))
            .Replace("{to}", Uri.EscapeDataString(to));

        TollFare? fare;
        try
        {
            using var response = await http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("통행료 조회 실패 — {From}→{To} 응답 {Status}", from, to, (int)response.StatusCode);
                return null;
            }
            fare = Parse(await response.Content.ReadAsStringAsync(ct), from, to);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "통행료 조회 — {From}→{To} 못 받았다", from, to);
            return null;
        }

        // 못 찾은 쌍도 기억한다. 안 그러면 없는 구간을 고른 사람이 누를 때마다 바깥을 부른다.
        cache.Set(key, fare, CacheFor);
        return fare;
    }

    internal static TollFare? Parse(string body, string from, string to)
    {
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("list", out var list) || list.ValueKind != JsonValueKind.Array) return null;

        foreach (var row in list.EnumerateArray())
        {
            if (Text(row, "dprtrTolofCd") != from || Text(row, "arrvTolofCd") != to) continue;

            // 거리는 차로 폭별로 나뉘어 온다(2·4·6차로). 구간 길이는 그 합이다.
            var distance = Dec(row, "crgw2SumDstne") + Dec(row, "crgw4SumDstne") + Dec(row, "crgw6SumDstne");

            var fares = new Dictionary<VehicleClass, int>();
            foreach (var (cls, field) in FareFields)
            {
                var amount = Int(row, field);
                if (amount > 0) fares[cls] = amount;
            }

            return new TollFare(
                from, Text(row, "dprtrTolofNm"),
                to, Text(row, "arrvTolofNm"),
                distance,
                Int(row, "hourUntDrveHour") * 60 + Int(row, "mmUntDrveHour"),
                fares);
        }

        return null;
    }

    private static string Text(JsonElement row, string name) =>
        row.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()!.Trim()
            : string.Empty;

    private static int Int(JsonElement row, string name) =>
        int.TryParse(Text(row, name), out var n) ? n : 0;

    private static decimal Dec(JsonElement row, string name) =>
        decimal.TryParse(Text(row, name), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0m;
}
