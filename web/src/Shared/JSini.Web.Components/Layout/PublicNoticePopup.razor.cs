using Microsoft.AspNetCore.Components;
using JSini.Web.Models;

namespace JSini.Web.Components.Layout;

public partial class PublicNoticePopup
{
    [Inject] private PublicNoticeStore Store { get; set; } = default!;
    [Inject] private ILogger<PublicNoticePopup> Log { get; set; } = default!;

    private IReadOnlyList<NoticeDto> _notices = [];

    /// <summary>
    /// <b>서버 기준</b> 날짜. 「오늘 하루 보지 않기」의 하루를 가르는 값이다.
    ///
    /// <para>
    /// 브라우저 날짜로 가르지 않는 이유는 <b>로그인 뒤 팝업과 같은 열쇠를
    /// 쓰기 때문</b>이다(<c>jsini-notice-dismissed</c>). 저쪽은 회로 안이라
    /// 서버 날짜밖에 없어서, 여기서 브라우저 날짜를 쓰면 두 팝업이 같은 값을
    /// 서로 다른 기준으로 읽는다. 시간대가 다른 사용자는 「하루」의 경계가
    /// 몇 시간 어긋나는데, 어긋난 결과는 공지를 한 번 더 보는 것뿐이다.
    /// </para>
    /// </summary>
    private static string Today => DateTime.Now.ToString("yyyy-MM-dd");

    protected override async Task OnInitializedAsync()
    {
        // 통이 있으면 왕복이 없다. 처음 열리는 순간에만 게이트웨이를 다녀오고,
        // 그 왕복은 한도(2초)가 걸려 있다 — 못 읽으면 빈 목록이다.
        // **공지를 못 읽었다고 로그인 화면을 세우지 않는다.**
        try
        {
            _notices = await Store.GetAsync();
        }
        catch (Exception ex)
        {
            // 통이 이미 삼키므로 여기까지 오지 않는 것이 정상이다. 그래도
            // 막아 두는 이유는, 새는 날 증상이 「로그인 화면이 500」이라서다.
            Log.LogWarning(ex, "공개 공지를 읽지 못했습니다.");
            _notices = [];
        }
    }

    // 게시일과 파일 크기 서식은 `NoticePopup` 의 것을 그대로 쓴다. 두 팝업이
    // 갈라져도 되는 것은 골격뿐이라, 사용자가 읽는 글자를 여기 다시 적지 않는다.
}
