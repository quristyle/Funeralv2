using System.Text.Json;

namespace LifeEnvServer.Utilities;

/// <summary>
/// 기상청 중기예보 응답 두 벌(<c>getMidLandFcst</c> · <c>getMidTa</c>)을
/// <b>하루 한 줄</b>로 접는다.
/// </summary>
/// <remarks>
/// <para>
/// 두 응답은 날짜가 <b>칸 이름 안에 들어 있는</b> 납작한 모양이다 —
/// <c>wf4Am</c> · <c>rnSt4Pm</c> · <c>taMin7</c> 처럼 「며칠 뒤」가 이름에 붙어 있고,
/// 8~10일치는 오전·오후 구분이 아예 없다(<c>wf8</c>). 그것을 읽어 내는 일이
/// <b>두 군데</b>에 있었다 — 등록 지역을 모아 두는 수집기와 내 위치의 주간 예보다.
/// 한쪽만 고치면 <b>같은 화면에서 등록 지역과 내 위치가 다른 말을 한다.</b>
/// </para>
/// <para>
/// <b>기상청을 부르지 않는다.</b> 받아 온 글만 읽는다 — 그래야 시험이 붙는다.
/// </para>
/// </remarks>
public static class MidTermForecastReader
{
    /// <summary>
    /// 중기예보 하루치. <c>DayAfter</c> 는 <b>발표일로부터 며칠 뒤인가</b>(3~10)이고,
    /// <c>Date</c> 는 그것을 발표일에 더한 실제 날짜다.
    /// </summary>
    public record Day(
        int DayAfter,
        DateOnly Date,
        string AmSky,
        string PmSky,
        int AmPop,
        int PmPop,
        int MinTemp,
        int MaxTemp);

    /// <summary>
    /// 두 응답을 3~10일치로 편다. <b>하늘 상태가 통째로 빈 날은 버린다</b> —
    /// 그 날은 기상청이 아직 안 낸 것이라, 0℃ 짜리 빈 칸을 그리면 거짓말이 된다.
    /// </summary>
    /// <param name="landJson"><c>getMidLandFcst</c> 원문. 비면 빈 목록.</param>
    /// <param name="tempJson"><c>getMidTa</c> 원문. 비면 빈 목록.</param>
    /// <param name="baseDate"><c>yyyyMMddHHmm</c> 발표 차수. 앞 여덟 자가 날짜 계산의 기준이다.</param>
    public static List<Day> Read(string? landJson, string? tempJson, string baseDate)
    {
        var days = new List<Day>();

        if (string.IsNullOrEmpty(landJson) || string.IsNullOrEmpty(tempJson)) return days;
        if (baseDate.Length < 8) return days;

        try
        {
            using var landDoc = JsonDocument.Parse(landJson);
            using var tempDoc = JsonDocument.Parse(tempJson);

            // 기상청은 키가 없을 때도 200 으로 답한다(본문이 다른 모양이다).
            // 그때 여기서 던지면 부르는 쪽이 통째로 멎으므로 빈 목록으로 돌아간다.
            if (!TryFirstItem(landDoc, out var landItem) || !TryFirstItem(tempDoc, out var tempItem))
            {
                return days;
            }

            // **JsonDocument 가 살아 있는 동안에만** 그 안의 JsonElement 를 읽을 수 있다.
            // 그래서 읽어 내는 일이 이 using 블록 안에서 끝나야 한다.
            var announce = DateTime.ParseExact(baseDate[..8], "yyyyMMdd", null);

            for (var i = 3; i <= 10; i++)
            {
                string amSky, pmSky;
                int amPop, pmPop;

                if (i <= 7)
                {
                    amSky = Str(landItem, $"wf{i}Am");
                    pmSky = Str(landItem, $"wf{i}Pm");
                    amPop = Int(landItem, $"rnSt{i}Am");
                    pmPop = Int(landItem, $"rnSt{i}Pm");
                }
                else
                {
                    // 8~10일은 오전·오후가 갈리지 않는다. 같은 값을 양쪽에 둔다 —
                    // 화면이 둘이 같으면 한 칸으로 접는다.
                    amSky = pmSky = Str(landItem, $"wf{i}");
                    amPop = pmPop = Int(landItem, $"rnSt{i}");
                }

                if (string.IsNullOrEmpty(amSky) && string.IsNullOrEmpty(pmSky)) continue;

                days.Add(new Day(
                    i,
                    DateOnly.FromDateTime(announce.AddDays(i)),
                    amSky,
                    pmSky,
                    amPop,
                    pmPop,
                    Int(tempItem, $"taMin{i}"),
                    Int(tempItem, $"taMax{i}")));
            }
        }
        catch (JsonException)
        {
            return [];
        }
        catch (FormatException)
        {
            return [];
        }

        return days;
    }

    private static bool TryFirstItem(JsonDocument doc, out JsonElement item)
    {
        item = default;

        if (!doc.RootElement.TryGetProperty("response", out var response)) return false;
        if (!response.TryGetProperty("body", out var body)) return false;
        if (!body.TryGetProperty("items", out var items)) return false;
        if (!items.TryGetProperty("item", out var list)) return false;
        if (list.ValueKind != JsonValueKind.Array) return false;

        foreach (var first in list.EnumerateArray())
        {
            item = first;
            return true;
        }

        return false;
    }

    /// <summary>기상청은 같은 칸을 문자열로도 숫자로도 보낸다.</summary>
    private static string Str(JsonElement el, string key)
    {
        if (!el.TryGetProperty(key, out var p)) return "";
        return p.ValueKind == JsonValueKind.String ? p.GetString() ?? "" : p.GetRawText();
    }

    private static int Int(JsonElement el, string key)
    {
        if (!el.TryGetProperty(key, out var p)) return 0;
        if (p.ValueKind == JsonValueKind.Number) return p.GetInt32();
        return p.ValueKind == JsonValueKind.String && int.TryParse(p.GetString(), out var v) ? v : 0;
    }

    /// <summary>
    /// 지금 시각에 유효한 <b>발표 차수</b>(<c>yyyyMMddHHmm</c>). 중기예보는 KST 06시·18시에 나온다.
    /// </summary>
    public static string BaseDate(DateTime nowKst) => nowKst.Hour switch
    {
        < 6 => nowKst.AddDays(-1).ToString("yyyyMMdd") + "1800",
        < 18 => nowKst.ToString("yyyyMMdd") + "0600",
        _ => nowKst.ToString("yyyyMMdd") + "1800",
    };
}
