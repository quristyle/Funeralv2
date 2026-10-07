using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.HelpDesk.Api;

namespace JSini.Web.HelpDesk.Components.Shared;

/// <summary>
/// 요청 목록을 그리는 부품 둘(<c>RequestGrid</c> 데스크톱 ·
/// <c>RequestCards</c> 휴대폰)이 함께 쓰는 뼈대.
/// </summary>
/// <remarks>
/// <para>
/// [왜 부품이 둘인가 (2026-10-08)]
/// </para>
///
/// <para>
/// 한동안 표 하나가 두 모양을 다 맡았다 — 휴대폰이면 페이저를 걷고(가상
/// 스크롤로 바뀐다) 칸 몇을 감추고 제목 칸에 그 값을 들였다. 그런데 휴대폰에서
/// 바라는 모습은 <b>표가 아니다</b> —
/// </para>
///
/// <list type="bullet">
///   <item><b>칸 머리줄이 없다.</b> 칸이 둘뿐인 표의 머리줄은 「그림 · 제목」을
///         적어 자리만 먹는다.</item>
///   <item><b>표 안에서 구르지 않는다.</b> 표가 남은 높이를 다 먹고 제 안에서
///         구르면, 화면 전체를 쓸어올리는 손짓이 표 가장자리에서만 먹히고
///         주소줄도 안 접힌다. 목록은 <b>글처럼</b> 이어져야 한다.</item>
///   <item>줄이 전부 깔리고 <b>맨 아래에 「더보기」</b>가 선다.</item>
/// </list>
///
/// <para>
/// 그 셋은 DevExpress 표를 비틀어서 얻는 모습이 아니다. 머리줄은 테마 CSS 를
/// 덮어야 걷히고, 안쪽 구르기는 가상 스크롤의 전제라 끄면 쪽나누기가 되살아난다 —
/// <b>고칠 자리가 전부 남의 부품 속</b>이라 DevExpress 를 올릴 때마다 깨진다.
/// 그래서 휴대폰 쪽은 표를 쓰지 않고 <c>ul</c> 하나로 새로 세웠다.
/// </para>
///
/// <para>
/// [여기 남는 것은 모양과 무관한 일뿐이다]
/// </para>
///
/// <para>
/// 그림 고르기 · 미리보기 · 회사 이름 풀기처럼 <b>어느 모양으로 그리든 같은
/// 일</b>을 모았다. 둘로 갈라 적어 두면 한쪽만 고치는 날이 오고, 그날 두
/// 기기가 서로 다른 것을 보여 준다. 적는 글자는 <see cref="RequestRowText"/> 다.
/// </para>
/// </remarks>
public abstract class RequestRowList : ComponentBase
{
    /// <summary>줄의 그림을 눌렀을 때 뜨는 미리보기 창. 레이아웃이 한 벌 들고 있다.</summary>
    [Inject] protected ImagePreview Preview { get; set; } = default!;

    /// <summary>회사 이름을 푸는 데 쓴다 — 서버는 회사 <b>아이디</b>만 준다.</summary>
    [Inject] protected HelpDeskContext Context { get; set; } = default!;

    /// <summary>그릴 줄들. 자르는 일은 화면이 한다(「더보기」).</summary>
    [Parameter, EditorRequired] public IReadOnlyList<ImprovementRequest> Rows { get; set; } = [];

    /// <summary>줄을 눌렀다 — 상세로 간다. 가는 쪽은 화면이 정한다.</summary>
    [Parameter] public EventCallback<ImprovementRequest> RowClick { get; set; }

    /// <summary>
    /// 썸네일을 못 받은 파일들. 깨진 네모 대신 빈 자리로 돌아간다.
    /// </summary>
    /// <remarks>
    /// 개발 장비에서 올린 그림은 <b>운영 파일 서버에 바이트가 없다</b> —
    /// 목록 하나에 그런 줄이 여럿이면 깨진 네모가 줄줄이 선다. 한 번 실패한
    /// 주소는 적어 두고 다시 걸지 않는다.
    /// </remarks>
    private readonly HashSet<string> _brokenThumbs = new(StringComparer.Ordinal);

    /// <summary>이 줄을 대신할 그림. 한 번 못 받은 것은 다시 걸지 않는다.</summary>
    protected RequestImage? ThumbOf(ImprovementRequest r)
    {
        var picked = RequestThumb.PickOf(r);
        return picked is not null && _brokenThumbs.Contains(picked.ThumbnailUrl) ? null : picked;
    }

    /// <summary>
    /// 줄의 그림을 눌렀다 — <b>원본</b>을 미리보기로 띄운다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 한 장만 넘기지 않는다. 그 요청에 붙은 그림을 전부 넘기면 창 안에서
    /// 앞뒤로 넘어가므로, 사진이 여러 장인 장애 신고를 상세 화면까지 가지
    /// 않고도 훑어볼 수 있다.
    /// </para>
    /// <para>
    /// 이름이 없는 그림(본문에 박은 것)은 <b>글 제목</b>으로 적는다 —
    /// 머리띠에 「그림」만 떠 있으면 어느 요청의 사진인지 알 수 없다.
    /// </para>
    /// </remarks>
    protected void PreviewImages(ImprovementRequest r) =>
        Preview.OpenAll(RequestThumb.ImagesOf(r)
            .Select(i => new ImagePreviewItem(i.OriginalUrl, i.Name ?? r.Title)));

    /// <summary>그림을 못 받았다. 깨진 네모를 지우고 빈 자리로 돌아간다.</summary>
    protected void ThumbFailed(string url)
    {
        if (_brokenThumbs.Add(url))
        {
            StateHasChanged();
        }
    }

    /// <summary>요청자가 딸린 회사 이름. 서버가 주는 것은 아이디뿐이다.</summary>
    protected string CompanyName(ImprovementRequest r) =>
        Context.CompanyName(r.Customer?.CompanyId);

    /// <summary>
    /// 접수 시각을 <b>우리 시간</b>으로 적는다.
    /// </summary>
    /// <remarks>
    /// 서버가 주는 것은 UTC 라 그대로 그리면 아홉 시간 전으로 보인다 —
    /// 오늘 오전에 올린 글이 목록에는 어제로 적혀서, 「오늘 접수」로 걸러
    /// 놓고 어제 날짜만 보게 된다.
    /// </remarks>
    protected static string Requested(ImprovementRequest r) =>
        r.CreatedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "-";
}
