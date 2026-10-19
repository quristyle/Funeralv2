using JSini.Web.Components.Data;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>
/// 「빠른 지시」 카드와 창이 함께 쓰는 시각 표기. 같은 계산을 두 곳에 두면 한쪽만 고쳐져 같은 건의 시간이 다르게 보인다.
/// 견주기는 AppTime.UtcNow, 보여 줄 때만 Kst 로 옮긴다. DateTime.Now 는 쓰지 않는다 — 개발 장비에서만 아홉 시간 어긋난다.
/// </summary>
internal static class AiTaskWhen
{
    /// <summary>실행일시. 실제로 돌기 시작한 시각이고, 없으면 요청·작성 시각으로 대신한다.</summary>
    /// <param name="t">볼 작업.</param>
    /// <param name="compact">좁은 자리(카드·휴대폰 머리띠)에서 켠다. 오늘에서 먼 만큼만 적는다(Near).</param>
    public static string RunAt(AiTaskDto t, bool compact = false)
    {
        var at = t.StartedAt ?? t.RequestedAt ?? t.CreDt;

        if (at is null)
        {
            return "-";
        }

        var local = AppTime.ToKorea(at.Value);

        if (!compact)
        {
            return local.ToString("yyyy-MM-dd HH:mm");
        }

        return Near(local);
    }

    /// <summary>오늘에서 먼 만큼만 적는 시각. 해는 올해가 아니면 붙인다 — 떼면 작년 건과 올해 건이 같은 꼴로 보인다.</summary>
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

        // 달만 견주면 작년 10월이 이번 달로 읽힌다 — 해까지 함께 본다.
        if (day.Year == today.Year && day.Month == today.Month)
        {
            return local.ToString("dd'일' HH:mm");
        }

        return day.Year == today.Year
            ? local.ToString("MM-dd HH:mm")
            : local.ToString("yyyy-MM-dd HH:mm");
    }

    /// <summary>총작업시간. 도는 중이면 지금까지를 세고(화면이 5초마다 다시 읽는다), 끝난 건은 서버가 잰 값을 쓴다.</summary>
    public static string Elapsed(AiTaskDto t)
    {
        // 재시도 대기 건은 지난 실행의 시작 시각이 남아 있다 — 도는 중 계산에 걸리면 안 되고 지난 실행 시간을 적는다.
        if (t.IsRetrying)
        {
            return t.DurationMs is > 0 ? Span(TimeSpan.FromMilliseconds(t.DurationMs.Value)) : "-";
        }

        if (t.IsBusy)
        {
            // 아직 집어 가지 않았다. 「00:00」이면 즉시 끝난 것과 구별이 안 된다.
            return t.StartedAt is { } running ? Span(AppTime.UtcNow - running) : "대기 중";
        }

        if (t.DurationMs is > 0)
        {
            return Span(TimeSpan.FromMilliseconds(t.DurationMs.Value));
        }

        // 취소·중단으로 서버가 잰 값이 없으면 남은 두 시각으로 센다.
        if (t.StartedAt is { } started && t.FinishedAt is { } finished)
        {
            return Span(finished - started);
        }

        return "-";
    }

    /// <summary>차수 머리줄의 「시작 시각 · 걸린 시간」. 접힌 머리줄이 차수에 대해 말하는 전부라 둘 다 적는다. 오늘 날짜는 뺀다.</summary>
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

        // 도는 중이면 지금까지를 센다.
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

    /// <summary>걸린 시간. mm:ss 로 두 자리를 맞추고, 한 시간이 넘으면 h:mm:ss 로 한다.</summary>
    private static string Span(TimeSpan d)
    {
        if (d < TimeSpan.Zero)
        {
            d = TimeSpan.Zero;
        }

        return d.TotalHours >= 1
            ? $"{(int)d.TotalHours}:{d.Minutes:00}:{d.Seconds:00}"
            : $"{(int)d.TotalMinutes:00}:{d.Seconds:00}";
    }
}
