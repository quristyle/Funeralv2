using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.HelpDesk.Api;

namespace JSini.Web.HelpDesk.Components.Shared;

/// <summary>요청 목록 부품 둘(RequestGrid 데스크톱 · RequestCards 휴대폰)이 함께 쓰는 뼈대. 모양과 무관한 일 — 그림 고르기·미리보기·회사 이름 풀기 — 만 모은다.
/// 휴대폰은 표를 쓰지 않는다 — 표를 비틀면 머리줄과 안쪽 구르기가 DevExpress 안에 남아 버전을 올릴 때마다 깨진다.</summary>
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

    /// <summary>썸네일을 못 받은 파일들. 깨진 네모 대신 빈 자리로 돌아간다. 개발 장비에서 올린 그림은 운영 파일 서버에 바이트가 없어 실패하므로, 한 번 실패한 주소는 다시 걸지 않는다.</summary>
    private readonly HashSet<string> _brokenThumbs = new(StringComparer.Ordinal);

    /// <summary>이 줄을 대신할 그림. 한 번 못 받은 것은 다시 걸지 않는다.</summary>
    protected RequestImage? ThumbOf(ImprovementRequest r)
    {
        var picked = RequestThumb.PickOf(r);
        return picked is not null && _brokenThumbs.Contains(picked.ThumbnailUrl) ? null : picked;
    }

    /// <summary>줄의 그림을 눌렀다 — 원본을 미리보기로 띄운다. 한 장만이 아니라 그 요청의 그림 전부를 넘겨 창 안에서 앞뒤로 넘긴다.
    /// 이름 없는 그림(본문에 박은 것)은 글 제목으로 적는다 — 머리띠에 「그림」만 뜨면 어느 요청의 사진인지 알 수 없다.</summary>
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

    /// <summary>접수 시각을 우리 시간으로 적는다. 서버 값은 UTC 라 그대로 그리면 아홉 시간 전으로 보인다.</summary>
    protected static string Requested(ImprovementRequest r) =>
        r.CreatedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "-";
}
