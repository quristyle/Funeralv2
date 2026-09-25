using DevExpress.Blazor;

namespace JSini.Web.Components.Layout;

/// <summary>
/// 동작의 결과를 알리는 자리. <b>그리는 일은 DevExpress 가 한다</b> —
/// 이것은 <see cref="IToastNotificationService"/> 앞에 세운 얇은 창구다.
/// </summary>
/// <remarks>
/// <para>
/// [무엇을 대신하는가]
/// </para>
///
/// <para>
/// 「저장했습니다」 · 「지웠습니다」 · 「저장하지 못했습니다 — …」 같은 <b>동작의
/// 결과</b>를 화면 위쪽 안내 줄(<see cref="PageNotice"/>)로 띄우고 있었다.
/// 화면 132개가 <c>&lt;PageNotice Text="@Notice" Tone="@Tone" /&gt;</c> 한 줄을
/// 똑같이 들고 있었고, 그 자리에 세 가지 문제가 있었다.
/// </para>
///
/// <list type="number">
///   <item>
///     <b>본 사람도 안 본 사람도 같은 것을 본다.</b> 안내 줄은 다음 동작 때까지
///     남는다. 저장하고 5분 뒤에 돌아와도 「저장했습니다」가 그대로 붙어 있어,
///     그것이 <b>방금 한 일인지 아까 한 일인지</b> 알 수 없다.
///   </item>
///   <item>
///     <b>자리를 차지한다.</b> 조건줄과 표 사이에 줄이 하나 생겼다 없어지면서
///     표가 통째로 위아래로 밀린다 — 저장할 때마다 보고 있던 줄이 움직인다.
///   </item>
///   <item>
///     <b>스크롤을 내려 둔 사람에게는 안 보인다.</b> 안내 줄은 화면 맨 위에
///     있고 긴 표의 아래쪽에서 지우면 아무 일도 안 일어난 것처럼 보인다.
///     실제로 「삭제가 안 된다」는 신고가 그렇게 들어왔다.
///   </item>
/// </list>
///
/// <para>
/// [토스트를 손으로 만들지 않는다]
/// </para>
///
/// <para>
/// 한 번 직접 그렸다가 걷어냈다. <c>DxToastProvider</c> · <c>DxToast</c> ·
/// <see cref="IToastNotificationService"/> 가 이미 있고, <b>그쪽이 우리가 손으로
/// 맞출 수 없는 것들을 이미 맞춰 준다</b> — 테마 스물둘의 색, 서랍에서 고른
/// 크기(<c>SizeMode</c>), 어두운 모드, 미끄러지는 애니메이션, 겹침 순서.
/// 손으로 만든 판은 그 전부를 따로 따라가야 하고, 테마를 올릴 때마다
/// <b>토스트만 옛 색으로 남는</b> 쪽으로 어긋난다.
/// </para>
///
/// <para>
/// 등록도 할 것이 없다 — <c>AddDevExpressBlazor()</c> 가 이 서비스를
/// <c>Scoped</c> 로 이미 넣는다(<c>JSiniWebApp</c>).
/// </para>
///
/// <para>
/// [그래도 이 창구를 한 겹 둔다]
/// </para>
///
/// <para>
/// 부르는 쪽(<see cref="Data.DataPage"/> 와 첨부 부품 둘)이 아는 것은
/// <see cref="NoticeTone"/> 셋뿐이다. 그 셋을 <c>ToastRenderStyle</c> 과
/// <b>「실패는 얼마나 오래 남는가」로 옮기는 규칙을 한 곳에 둔다</b> —
/// 부르는 자리마다 <c>ToastOptions</c> 를 손으로 채우게 하면 화면마다 색과
/// 시간이 갈린다. 이 포털에서 갈라짐이 시작되는 자리가 늘 그런 곳이었다.
/// </para>
///
/// <para>
/// [실패도 걷힌다 — 다만 훨씬 오래 남는다]
/// </para>
///
/// <para>
/// 한동안 <b>실패는 사람이 닫을 때까지</b> 남겼다(<c>TimeSpan.MaxValue</c>).
/// 실패를 못 보고 지나가면 「저장한 줄 알았는데 안 된」 상태가 되고, 그것이
/// 안내 줄 시절에도 제일 비싼 실수였기 때문이다. 그런데 그렇게 두면 반대쪽이
/// 생긴다 — 서버가 한 번 흔들린 자국이 <b>화면 구석에 종일 붙어</b> 있고,
/// 넷까지 쌓이면(<c>MaxToastCount</c>) 그 아래 것이 가려진다.
/// </para>
///
/// <para>
/// 그래서 실패도 걷히게 하되 <b>읽고 옮겨 적을 만큼</b>(<see cref="Failure"/>)
/// 남긴다. 못 보고 지나가는 것은 <b>언제 걷히는지를 보여 주어</b> 막는다 —
/// 남은 시간이 토스트 아래에 막대와 초로 뜬다(app.css 의 「토스트의 남은
/// 시간」). 급하면 닫기 단추가 그대로 있고, 더 봐야 하면 토스트를 한 번 누르면
/// 된다(<c>MainLayout</c> 의 <c>FreezeOnClick</c> — 그때는 표시도 함께 멈춘다).
/// </para>
///
/// <para>
/// [손가락으로는 쓸어 치운다]
/// </para>
///
/// <para>
/// 닫기 단추는 손가락보다 작아 휴대폰에서 잘 빗나간다. 그래서 <b>토스트를
/// 옆으로 쓸면 기다리지 않고 바로 걷힌다</b> — 끄는 동안 손가락을 따라가고
/// 충분히 가면 날아간다(theme.js 의 「쓸어 치우기」 · app.css 의 같은 이름
/// 절). 치우는 일은 우리가 하지 않고 <b>그 닫기 단추를 대신 누른다</b> —
/// DOM 에서 직접 지우면 DevExpress 의 목록에 유령이 남아
/// <c>MaxToastCount</c> 가 어긋난다. 마우스 끌기는 그대로 둔다. 실패 문구를
/// <b>끌어서 고르고 복사하는</b> 것이 위에 적은 쓰임새이기 때문이다.
/// </para>
///
/// <para>
/// [그래도 <see cref="PageNotice"/> 를 지우지 않는다]
/// </para>
///
/// <para>
/// 그 부품이 담는 것이 둘이었다 — <b>동작의 결과</b>와 <b>화면의 상태</b>다.
/// 「왼쪽에서 메뉴를 고르십시오」 · 「이 화면은 조회만 합니다」는 사라지면 안
/// 된다. 잠깐 떴다 지는 것은 <b>방금 무슨 일이 일어났는가</b>뿐이다.
/// 그래서 <b>앞엣것만</b> 여기로 옮겼다.
/// </para>
/// </remarks>
/// <param name="service">
/// DevExpress 의 토스트 서비스. 띄우는 곳은 레이아웃에 놓인
/// <c>DxToastProvider</c> 하나다.
/// </param>
public sealed class Toasts(IToastNotificationService service)
{
    /// <summary>
    /// 알림과 주의가 떠 있는 시간. DevExpress 판의 기본값과 같은 5초다.
    ///
    /// <para>
    /// <b>기본값에 기대지 않고 적어 둔다.</b> 남은 시간을 그리려면 그 시간을
    /// <b>알고 있어야</b> 하는데, <c>null</c> 로 두면 얼마인지 여기서 알 수 없다.
    /// </para>
    /// </summary>
    public static readonly TimeSpan Brief = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 실패가 떠 있는 시간. 알림의 열두 배다.
    ///
    /// <para>
    /// 실패 문구에는 서버가 준 이유가 붙어 두 줄이 되는 일이 잦고, 읽고 나서
    /// <b>적어 두거나 옮겨 붙일</b> 시간까지 있어야 한다. 5초는 그것을 못 한다.
    /// </para>
    /// </summary>
    public static readonly TimeSpan Failure = TimeSpan.FromSeconds(60);

    /// <summary>
    /// app.css 가 칸을 갖고 있어야 하는 시간들. <b>테스트가 이 목록으로
    /// 대조한다</b>(<c>ToastTests</c>) — 여기에 초를 하나 더 만들고 CSS 를
    /// 잊으면 그 토스트만 남은 시간이 안 그려진다.
    /// </summary>
    public static IEnumerable<TimeSpan> DisplayTimes => [Brief, Failure];

    /// <summary>
    /// 남은 시간을 그리게 하는 클래스. 초를 <b>이름에 실어</b> 넘긴다 —
    /// <c>ToastOptions</c> 로는 클래스 말고 아무것도 실어 보낼 수 없다.
    /// </summary>
    /// <param name="span">떠 있는 시간.</param>
    public static string CountdownClass(TimeSpan span) =>
        $"jsini-toast-time jsini-toast-time--{(int)span.TotalSeconds}s";

    /// <summary>한 장 띄운다. 빈 문구는 아무 일도 하지 않는다.</summary>
    /// <param name="text">보여 줄 문구.</param>
    /// <param name="tone">성격. 색과 <b>얼마나 남는지</b>가 여기서 갈린다.</param>
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

            // 남은 시간 표시. 그리는 일은 app.css 가 하고 여기서 넘기는 것은
            // **몇 초짜리인가** 하나뿐이다.
            CssClass = CountdownClass(span),
        });
    }

    /// <summary>
    /// 우리 말투 셋을 DevExpress 의 다섯 중 셋으로 옮긴다.
    ///
    /// <para>
    /// <c>Success</c> 를 쓰지 않는다. 우리 쪽에는 「성공」이라는 말투가 없고,
    /// 있다고 쳐도 <b>저장이 잘된 것과 조회 결과가 없는 것이 같은 초록</b>이
    /// 되어 두 가지가 한 색으로 뭉개진다.
    /// </para>
    /// </summary>
    private static ToastRenderStyle Style(NoticeTone tone) => tone switch
    {
        NoticeTone.Error => ToastRenderStyle.Danger,
        NoticeTone.Warning => ToastRenderStyle.Warning,
        _ => ToastRenderStyle.Info,
    };
}
