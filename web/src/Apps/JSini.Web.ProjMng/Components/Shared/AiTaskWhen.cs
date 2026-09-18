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
/// <para>
/// [<c>ToLocalTime()</c> 을 부르지 않는다 — 부르면 아홉 시간이 더해진다]
/// </para>
/// <para>
/// <c>projmng.ai_task</c> 의 시각 칸은 <b>시간대 없는</b> <c>timestamp</c> 이고
/// 서버가 <c>now()</c> 로 찍는다. 컨테이너는 전부 <c>Asia/Seoul</c> 이라
/// (<c>deploy/docker</c>) 거기 앉는 값은 <b>이미 우리 시계의 벽시계 시각</b>이다.
/// 그 값은 전선을 타고 <c>Kind=Unspecified</c> 로 오는데,
/// <see cref="DateTime.ToLocalTime"/> 은 <b><c>Unspecified</c> 를 UTC 로 치고</b>
/// 옮긴다 — 08:00 이 17:00 이 된다.
/// </para>
/// <para>
/// 그래서 실제로 <b>도는 건의 시간이 「-」로 보였다.</b> 지금까지가 음수(미래에
/// 시작한 것으로 보이니)라 아래 <see cref="Elapsed"/> 가 포기하고 「-」를 적었다.
/// 「AI 작업」 화면의 같은 계산(<c>RunElapsedText</c>)은 처음부터 그냥 뺐다 —
/// 이 파일만 어긋나 있었다.
/// </para>
/// <para>
/// 진짜 UTC 로 오는 자료(<c>AuthServer</c> 의 접속 기록 같은 것)와 규칙이 다르니
/// <b>여기 값을 다른 화면으로 옮길 때 그대로 베끼지 않는다.</b>
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

        // 옮기지 않는다. 위 머리말 참고 — 이 값은 이미 우리 시계다.
        var local = at.Value;

        if (!compact)
        {
            return local.ToString("yyyy-MM-dd HH:mm");
        }

        return local.Date == DateTime.Today
            ? local.ToString("HH:mm")
            : local.ToString("MM-dd HH:mm");
    }

    /// <summary>
    /// 총작업시간. <b>도는 중이면 지금까지를 센다</b> — 화면이 5초마다 목록을
    /// 다시 읽으므로 그 자리에서 늘어난다. 끝난 건은 서버가 잰 값
    /// (<c>DurationMs</c>)을 그대로 쓴다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>도는 중인지를 먼저 본다.</b> 지난번에 돌아서 <c>DurationMs</c> 가 남은
    /// 건을 다시 보내면(서버가 요청 때 지우지만 순서가 어긋날 수 있다) 그 옛
    /// 값이 <b>멈춘 채로</b> 보인다 — 도는 건의 시간은 늘어나야 한다.
    /// </para>
    /// <para>
    /// 음수는 「-」로 흘리지 않고 0 으로 눌러 적는다. 두 시계가 몇 초 어긋나면
    /// 방금 시작한 건이 통째로 사라지는데, <b>그 자리에 아무것도 없는 것이
    /// 「0초」보다 나쁘다.</b>
    /// </para>
    /// </remarks>
    public static string Elapsed(AiTaskDto t)
    {
        if (t.IsBusy)
        {
            // 아직 집어 가지 않았다. 「0초」라고 적으면 **돌다가 즉시 끝난 것**과
            // 구별이 안 된다.
            return t.StartedAt is { } running ? Span(DateTime.Now - running) : "대기 중";
        }

        if (t.DurationMs is > 0)
        {
            return Span(TimeSpan.FromMilliseconds(t.DurationMs.Value));
        }

        // 끝났는데 잰 값이 없다(취소·중단으로 서버가 못 적은 경우). 남은 두
        // 시각으로 대신 센다.
        if (t.StartedAt is { } started && t.FinishedAt is { } finished)
        {
            return Span(finished - started);
        }

        return "-";
    }

    /// <summary>걸린 시간 한 토막. 큰 자리 둘까지만 적는다.</summary>
    private static string Span(TimeSpan d)
    {
        if (d < TimeSpan.Zero)
        {
            d = TimeSpan.Zero;
        }

        return d.TotalMinutes < 1 ? $"{(int)d.TotalSeconds}초"
            : d.TotalHours < 1 ? $"{(int)d.TotalMinutes}분 {d.Seconds}초"
            : $"{(int)d.TotalHours}시간 {d.Minutes}분";
    }
}
