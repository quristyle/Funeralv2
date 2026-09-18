using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>
/// 「빠른 지시」 화면과 그 창이 함께 쓰는 시각 표기.
/// </summary>
/// <remarks>
/// <para>
/// 카드와 창이 <b>같은 값을 다른 폭에서</b> 보여 준다. 두 곳에 같은 계산을
/// 따로 적어 두면 한쪽만 고쳐져 <b>같은 건의 「총작업시간」이 카드와 창에서
/// 다르게 보이는</b> 날이 온다 — 그때 어느 쪽이 맞는지 알아낼 방법이 없다.
/// </para>
/// <para>
/// 화면에만 쓰는 표기라 <see cref="AiTaskDto"/> 에 얹지 않는다. 그쪽의
/// <c>StatusText</c> 는 모든 화면이 같게 읽어야 하는 이름이지만, 여기 둘은
/// <b>좁은 화면에서 자리를 아끼려고</b> 고른 모양이다.
/// </para>
/// </remarks>
internal static class AiTaskWhen
{
    /// <summary>
    /// 실행일시 — <b>실제로 돌기 시작한 시각</b>이다. 아직 대기 중이면 그런
    /// 시각이 없으므로 보낸 시각으로 대신하고, 그마저 없으면 비운다.
    /// </summary>
    /// <param name="t">볼 작업.</param>
    /// <param name="compact">
    /// 카드에 적을 때 켠다. 오늘 것은 <c>HH:mm</c> 만 적는다 — 카드에 남는
    /// 넷은 거의 오늘 것이고, 반 폭 카드에 날짜를 붙이면 <b>시각이 먼저
    /// 잘린다.</b> 창에서는 자리가 넉넉하니 연월일까지 다 적는다.
    /// </param>
    public static string RunAt(AiTaskDto t, bool compact = false)
    {
        var at = t.StartedAt ?? t.RequestedAt ?? t.CreDt;

        if (at is null)
        {
            return "-";
        }

        var local = at.Value.ToLocalTime();

        if (!compact)
        {
            return local.ToString("yyyy-MM-dd HH:mm");
        }

        return local.Date == DateTime.Today
            ? local.ToString("HH:mm")
            : local.ToString("MM-dd HH:mm");
    }

    /// <summary>
    /// 총작업시간. <b>끝난 건은 서버가 잰 값</b>(<c>DurationMs</c>)을 그대로
    /// 쓰고, 도는 중이면 시작한 때부터 지금까지를 센다 — 화면이 몇 초마다
    /// 다시 읽으므로 그 자리에서 늘어난다.
    /// </summary>
    public static string Elapsed(AiTaskDto t)
    {
        if (t.DurationMs is > 0)
        {
            return Span(TimeSpan.FromMilliseconds(t.DurationMs.Value));
        }

        if (t.StartedAt is { } started)
        {
            var running = DateTime.Now - started.ToLocalTime();

            return running < TimeSpan.Zero ? "-" : Span(running);
        }

        // 아직 시작도 안 했다. 「0초」라고 적으면 **돌다가 즉시 끝난 것**과
        // 구별이 안 된다.
        return t.IsBusy ? "대기 중" : "-";
    }

    /// <summary>걸린 시간 한 토막. 큰 자리 둘까지만 적는다.</summary>
    private static string Span(TimeSpan d) =>
        d.TotalMinutes < 1 ? $"{(int)d.TotalSeconds}초"
        : d.TotalHours < 1 ? $"{(int)d.TotalMinutes}분 {d.Seconds}초"
        : $"{(int)d.TotalHours}시간 {d.Minutes}분";
}
