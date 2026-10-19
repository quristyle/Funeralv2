using DevExpress.Blazor;

namespace JSini.Web.Components.Layout;

/// <summary>동작 결과를 알리는 얇은 창구. 그리는 일은 DevExpress 토스트가 한다 — 손으로 만들면 테마·크기·애니메이션을 따로 맞춰야 한다.
/// 실패는 알림보다 오래 남기고(60초), 손가락으로 옆으로 쓸면 걷힌다. 화면의 상태 안내(PageNotice)는 건드리지 않는다.</summary>
/// <param name="service">DevExpress 토스트 서비스. 띄우는 곳은 레이아웃의 DxToastProvider 하나다.</param>
public sealed class Toasts(IToastNotificationService service)
{
    /// <summary>알림과 주의가 떠 있는 시간(2초). 남은 시간을 그리려면 값을 알아야 하므로 기본값에 기대지 않고 적어 둔다.</summary>
    public static readonly TimeSpan Brief = TimeSpan.FromSeconds(2);

    /// <summary>실패가 떠 있는 시간. 알림의 서른 배 — 읽고 옮겨 적을 시간이 있어야 한다.</summary>
    public static readonly TimeSpan Failure = TimeSpan.FromSeconds(60);

    /// <summary>app.css 가 칸을 가져야 하는 시간들. ToastTests 가 이 목록으로 대조한다 — 초를 더하고 CSS 를 잊으면 남은 시간이 안 그려진다.</summary>
    public static IEnumerable<TimeSpan> DisplayTimes => [Brief, Failure];

    /// <summary>남은 시간을 그리는 클래스. 초를 이름에 실어 넘긴다 — ToastOptions 로는 클래스 말고 실을 것이 없다.</summary>
    /// <param name="span">떠 있는 시간.</param>
    public static string CountdownClass(TimeSpan span) =>
        $"jsini-toast-time jsini-toast-time--{(int)span.TotalSeconds}s";

    /// <summary>한 장 띄운다. 빈 문구는 아무 일도 하지 않는다.</summary>
    /// <param name="text">보여 줄 문구.</param>
    /// <param name="tone">성격. 색과 얼마나 남는지가 여기서 갈린다.</param>
    public void Show(string? text, NoticeTone tone = NoticeTone.Info)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var span = tone == NoticeTone.Error ? Failure : Brief;

        service.ShowToast(new ToastOptions
        {
            Text = text,
            RenderStyle = Style(tone),
            DisplayTime = span,

            // 남은 시간 표시. 그리는 일은 app.css 가 하고 여기서는 몇 초짜리인지만 넘긴다.
            CssClass = CountdownClass(span),
        });
    }

    /// <summary>우리 말투 셋을 DevExpress 의 다섯 중 셋으로 옮긴다. Success 는 쓰지 않는다 — 저장 성공과 조회 결과 없음이 같은 초록으로 뭉개진다.</summary>
    private static ToastRenderStyle Style(NoticeTone tone) => tone switch
    {
        NoticeTone.Error => ToastRenderStyle.Danger,
        NoticeTone.Warning => ToastRenderStyle.Warning,
        _ => ToastRenderStyle.Info,
    };
}
