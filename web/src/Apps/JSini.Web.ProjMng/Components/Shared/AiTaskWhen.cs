using JSini.Web.Components.Data;
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
/// [시각은 UTC 로 온다 — 보여 줄 때만 한국 시각으로 옮긴다]
/// </para>
/// <para>
/// <c>projmng.ai_task</c> 의 시각 칸은 <c>timestamptz</c> 이고 DB 세션도 UTC 라
/// 서버가 주는 값은 <b>UTC</b> 다(<c>Kind = Utc</c>, 전선에는 <c>...Z</c> 로 실린다).
/// 그래서 규칙이 두 줄로 갈린다.
/// </para>
/// <list type="bullet">
///   <item><b>견주기</b>(얼마나 지났나)는 <see cref="AppTime.UtcNow"/> 와.</item>
///   <item><b>보여 주기</b>는 <c>Kst(...)</c> 로 한국 시각으로 옮겨서.</item>
/// </list>
/// <para>
/// <see cref="DateTime.Now"/> · <see cref="DateTime.Today"/> 는 쓰지 않는다.
/// Blazor Server 라 그것은 <b>서버 프로세스의 시각</b>인데, 운영 컨테이너는
/// UTC 이고 개발 장비는 한국 시각이라 <b>개발에서만 아홉 시간 어긋난다</b> —
/// 가장 늦게 들키는 종류의 틀림이다. 아키텍처 테스트가 막는다.
/// </para>
/// <para>
/// 예전에는 이 칸이 <c>timestamp</c>(시간대 없음)라 한국 벽시계 숫자가 그대로
/// 앉아 있었고, 그래서 이 파일은 「옮기지 않는다」가 규칙이었다. 2026-09-29 에
/// DB 의 시각 칸을 전부 <c>timestamptz</c> 로 바꾸면서 뒤집혔다
/// (<c>deploy/sql/utc-timestamptz-2026-09-29.sql</c> · <c>docs/utc-time.md</c>).
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

        // 서버가 준 값은 UTC 다. 보여 주기 직전에 한국 시각으로 옮긴다.
        var local = AppTime.ToKorea(at.Value);

        if (!compact)
        {
            return local.ToString("yyyy-MM-dd HH:mm");
        }

        return DateOnly.FromDateTime(local) == AppTime.Today
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
        // 실패해서 다시 넣어 둔 것. 상태값은 대기지만 **지난 실행의 시작
        // 시각이 그대로 남아 있어**, 아래 「도는 중」 계산에 걸리면 이미 끝난
        // 실행의 시각부터 지금까지를 센다 — 돌지도 않는 건의 시간이 늘어난다.
        // 여기서는 **지난 실행에 걸린 시간**을 적는다.
        if (t.IsRetrying)
        {
            return t.DurationMs is > 0 ? Span(TimeSpan.FromMilliseconds(t.DurationMs.Value)) : "-";
        }

        if (t.IsBusy)
        {
            // 아직 집어 가지 않았다. 「0초」라고 적으면 **돌다가 즉시 끝난 것**과
            // 구별이 안 된다.
            return t.StartedAt is { } running ? Span(AppTime.UtcNow - running) : "대기 중";
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
