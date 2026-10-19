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
    /// 하늘 상태 한 칸. 오전·오후가 같으면 한 번만 적는다. 둘 다 비면 「-」 — 빈칸 둘은 화면이 깨진 것으로 읽힌다.
    /// </summary>
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
