using System.Text.Json;

namespace LifeEnvServer.Utilities;

/// <summary>
/// 기상청 중기예보 응답 두 벌(getMidLandFcst · getMidTa)을 하루 한 줄로 접는다. 날짜가 칸 이름 안에 들어 있어(wf4Am · taMin7)
/// 읽는 일을 등록 지역 수집기와 내 위치 주간 예보가 같이 쓴다. 기상청을 부르지 않으므로 시험이 붙는다.
/// </summary>
public static class MidTermForecastReader
{
    /// <summary>중기예보 하루치. DayAfter 는 발표일 기준 며칠 뒤(3~10), Date 는 그 실제 날짜다.</summary>
    public record Day(
        int DayAfter,
        DateOnly Date,
        string AmSky,
        string PmSky,
        int AmPop,
        int PmPop,
        int MinTemp,
        int MaxTemp);

    /// <summary>두 응답을 3~10일치로 편다. 하늘 상태가 통째로 빈 날은 기상청이 아직 안 낸 것이므로 버린다.</summary>
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
