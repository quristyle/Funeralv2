using Microsoft.AspNetCore.Components;
using JSini.Web.LifeEnv.Api;

namespace JSini.Web.LifeEnv.Components.Shared;

public partial class WeekForecastRow
{
    /// <summary>
    /// 그릴 날들. <b>차례를 여기서 바꾸지 않는다</b> — 서버가 날짜 순으로 준다.
    /// </summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<MidTermForecast> Days { get; set; } = [];

    /// <summary>
    /// 하늘 상태 한 칸. <b>오전·오후가 같으면 한 번만 적는다.</b>
    /// </summary>
    /// <remarks>
    /// 중기예보의 8~10일치는 기상청이 오전·오후를 가르지 않아 서버가 같은 값을
    /// 양쪽에 넣는다. 그것을 「구름많음 / 구름많음」으로 적으면 사람은 둘을
    /// 견주려다 같은 말임을 뒤늦게 안다. 둘 다 비면 「-」다 — 빈칸 둘을 보여
    /// 주면 자료가 없는 것이 아니라 <b>화면이 깨진 것</b>으로 읽힌다.
    /// </remarks>
    private static string Sky(MidTermForecast day)
    {
        var am = day.AmSky?.Trim() ?? "";
        var pm = day.PmSky?.Trim() ?? "";

        if (am.Length == 0 && pm.Length == 0) return "-";
        if (am.Length == 0) return pm;
        if (pm.Length == 0) return am;

        return string.Equals(am, pm, StringComparison.Ordinal) ? am : $"{am} / {pm}";
    }

    /// <summary>강수확률 한 칸. 하늘 상태와 같은 규칙으로 접는다.</summary>
    private static string Pop(MidTermForecast day)
        => day.AmPop == day.PmPop
            ? $"강수 {day.AmPop}%"
            : $"강수 {day.AmPop}% / {day.PmPop}%";
}
