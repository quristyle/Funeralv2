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
    /// <b>좁은 자리에 적을 때</b> 켠다 — 카드(반 폭)와 휴대폰 머리띠다.
    /// 오늘에서 먼 만큼만 적는다(<see cref="Near(DateTime?)"/>): 오늘 것은
    /// <c>HH:mm</c>, 이달 것은 날짜까지, 그보다 멀어야 달과 해가 붙는다.
    /// 카드에 남는 넷은 거의 오늘 것이고, 반 폭 카드에 연월일을 붙이면
    /// <b>시각이 먼저 잘린다.</b> 책상의 창에서는 자리가 넉넉하니 끄고
    /// 연월일까지 다 적는다.
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

        return Near(local);
    }

    /// <summary>
    /// <b>오늘에서 먼 만큼만 적는 시각.</b> 오늘 것은 시·분만, 이달 것은
    /// 날짜를 더해, 올해 것은 달까지, 그보다 먼 것에만 해를 붙인다.
    /// </summary>
    /// <param name="utc">서버가 준 UTC 시각. 비어 있으면 <c>-</c> 다.</param>
    /// <remarks>
    /// <para>
    /// <b>좁은 화면에서 자리를 아끼려고</b> 쓴다 — 카드(반 폭)와 휴대폰
    /// 머리띠다. 거기 적히는 건은 거의 오늘·이번 달 것이라, 늘 붙던
    /// <c>yyyy-MM-dd</c> 열 글자는 <b>거의 늘 같은 글자</b>였다. 같은 글자가
    /// 넓게 서면 그만큼 뒤가 다음 줄로 밀리고, 정작 갈라 주는 시·분이
    /// 먼저 잘린다.
    /// </para>
    /// <para>
    /// <b>해를 떼는 것은 올해까지다.</b> 「09-14 09:30」만 적고 말면 작년
    /// 9월에 돈 건이 <b>지지난주에 돈 건과 생김새가 같아진다</b> — 떼어도
    /// 되는 것은 읽는 사람이 되채울 수 있는 것뿐이고, 해는 그렇지 않다.
    /// </para>
    /// <para>
    /// <b>이달 것에는 「일」을 붙인다.</b> 맨 숫자로 「14 09:30」이라고 적으면
    /// 앞의 둘이 날짜인지 시각인지 갈리지 않는다(뒤가 이미 시:분이라 더
    /// 그렇다). 한 글자로 그것이 끝난다.
    /// </para>
    /// <para>
    /// 「오늘·이달·올해」는 <b>한국 달력</b>으로 센다(<see cref="AppTime.Today"/>).
    /// UTC 로 세면 한국 시각으로 오전 아홉 시 전에 돈 건이 하루 내내
    /// 「어제」로 보인다.
    /// </para>
    /// </remarks>
    public static string Near(DateTime? utc) =>
        utc is null ? "-" : Near(AppTime.ToKorea(utc.Value));

    /// <summary>위와 같되 <b>이미 한국 시각으로 옮긴</b> 값을 받는다.</summary>
    private static string Near(DateTime local)
    {
        var day = DateOnly.FromDateTime(local);
        var today = AppTime.Today;

        if (day == today)
        {
            return local.ToString("HH:mm");
        }

        // **해를 함께 본다.** 달만 견주면 작년 10월이 이번 달로 읽혀
        // 「14일 09:30」이 된다 — 1년 어긋난 시각이 오늘 것과 같은 꼴로 선다.
        if (day.Year == today.Year && day.Month == today.Month)
        {
            return local.ToString("dd'일' HH:mm");
        }

        return day.Year == today.Year
            ? local.ToString("MM-dd HH:mm")
            : local.ToString("yyyy-MM-dd HH:mm");
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

    /// <summary>
    /// 차수 머리줄에 적을 시각 한 토막 — <b>「시작 시각 · 걸린 시간」</b>이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 차수를 접어 두면 머리줄이 그 차수에 대해 말하는 전부가 된다. 거기
    /// <b>언제 돌았나와 얼마나 걸렸나</b>가 없으면, 접힌 셋 가운데 어느 것이
    /// 오래 걸린 차수인지 펴 보기 전에는 알 수 없다.
    /// </para>
    /// <para>
    /// <b>날짜는 오늘 것이면 뺀다.</b> 한 건의 차수들은 대개 같은 날 안에서
    /// 이어지고, 머리줄에는 배지와 차수 번호가 이미 서 있어 자리가 좁다.
    /// </para>
    /// <para>
    /// 서버가 주는 시각은 UTC 다 — 보여 주기 직전에만 한국 시각으로 옮긴다
    /// (이 파일 머리말).
    /// </para>
    /// </remarks>
    public static string RunWhen(AiTaskRunDto run)
    {
        var parts = new List<string>();

        if (run.StartedAt is { } at)
        {
            var local = AppTime.ToKorea(at);

            parts.Add(DateOnly.FromDateTime(local) == AppTime.Today
                ? local.ToString("HH:mm")
                : local.ToString("MM-dd HH:mm"));
        }

        // **도는 중이면 지금까지를 센다.** 끝난 차수는 두 시각의 사이다.
        if (run.StartedAt is { } from)
        {
            var to = run.FinishedAt ?? (run.IsRunning ? AppTime.UtcNow : null);

            if (to is { } until)
            {
                parts.Add(Span(until - from));
            }
        }

        return parts.Count == 0 ? "-" : string.Join(" · ", parts);
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
