using System.Globalization;

using JSini.Shared.Infrastructure.Time;

namespace AIAgentServer.Services.Portal;

/// <summary>업무 비서 도구가 주고받는 "yyyy-MM-dd" 한국 날짜 문자열을 UTC 조회 경계로 바꾼다. 변환 규칙은 전부
/// <see cref="AppTime"/> 에 맡긴다 — 여기서 ±9시간을 다시 계산하면 이 코드베이스에 두 번째 시간대 규칙이 생긴다.</summary>
public static class PortalDay
{
    private const string Format = "yyyy-MM-dd";

    /// <summary>한국 달력 하루의 시작(0시) — 포함 경계.</summary>
    public static DateTime StartUtc(string koreanDate) => AppTime.StartOfDayUtc(Parse(koreanDate));

    /// <summary>한국 달력 하루가 끝나고 다음 날이 시작되는 0시 — <b>제외 경계</b>(그 시각 자체는 포함하지 않는다).
    /// 끝을 "그날 23:59:59" 로 잡으면 그 뒤 소수초에 찍힌 자료가 조용히 빠진다 — NotificationServer 가
    /// 이미 같은 범위를 <c>StartOfDayUtc(to.AddDays(1))</c> 로 두는 것과 같은 규칙이다.</summary>
    public static DateTime EndExclusiveUtc(string koreanDate) => AppTime.StartOfDayUtc(Parse(koreanDate).AddDays(1));

    /// <summary>UTC 시각을 "yyyy-MM-dd HH:mm" 한국 벽시계 글로.</summary>
    public static string ToKorean(DateTime utc) => AppTime.ToKorea(utc).ToString($"{Format} HH:mm", CultureInfo.InvariantCulture);

    /// <summary>모델이 만든 날짜 글을 읽는다. 형식이 틀리면 조용히 엉뚱한 범위를 돌려주는 대신 그 자리에서
    /// 터뜨린다 — 도구 경계에서 바로 드러나야 모델이 "형식이 틀렸다" 는 것을 알고 다시 묻지, 안으로
    /// 들어가면 틀린 범위가 그냥 0건 결과로 둔갑해 버린다.</summary>
    private static DateOnly Parse(string koreanDate)
    {
        if (!DateOnly.TryParseExact(koreanDate, Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            throw new ArgumentException($"날짜 형식이 올바르지 않습니다(yyyy-MM-dd): '{koreanDate}'", nameof(koreanDate));

        return date;
    }
}
