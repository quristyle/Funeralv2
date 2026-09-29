namespace JSini.Web.Components.Data;

/// <summary>
/// 미리보기로 띄울 그림 한 장.
/// </summary>
/// <param name="Url">화면에 그릴 주소. <b>썸네일이 아니라 원본을 준다</b> —
/// 확대해서 보려고 여는 창이라 150×150 을 키워 봐야 뭉개진 네모만 커진다.</param>
/// <param name="Title">머리띠에 적을 이름. 없으면 「그림」으로 적는다.</param>
/// <param name="DownloadUrl">
/// 「내려받기」가 갈 주소. 비우면 <paramref name="Url"/> 로 간다 —
/// 보는 것과 받는 것이 다른 갈래인 경우에만 적는다(자료실이 그렇다).
/// </param>
public sealed record ImagePreviewItem(string Url, string? Title = null, string? DownloadUrl = null)
{
    /// <summary>실제로 내려받을 주소.</summary>
    public string Download =>
        string.IsNullOrWhiteSpace(DownloadUrl) ? Url : DownloadUrl;
}

/// <summary>
/// 지금 미리보기가 어떤 모습인가. <b>정본은 브라우저에 있다</b>
/// (<c>wwwroot/js/image-preview.js</c>) — 이것은 도구띠를 그리려고 받아 둔 사본이다.
/// </summary>
/// <remarks>
/// 확대율은 휠·손가락으로도 바뀌므로 C# 이 혼자 알 수 없다. 두 곳에 두면
/// 반드시 어긋나고, 어긋나면 도구띠의 「100%」가 거짓말을 한다.
/// </remarks>
public sealed record ImagePreviewState(
    double Zoom = 1,
    int Rotate = 0,
    bool FlipX = false,
    bool FlipY = false,
    bool Gray = false,
    bool Invert = false,
    int Brightness = 100,
    int Contrast = 100)
{
    /// <summary>아무것도 안 한 모습.</summary>
    public static readonly ImagePreviewState Fresh = new();
}

/// <summary>
/// 그림 미리보기를 <b>어느 화면에서나</b> 여는 손잡이.
/// </summary>
/// <remarks>
/// <para>
/// [화면마다 창을 짓지 않는다]
/// </para>
///
/// <para>
/// 큰 그림을 띄우는 창을 화면마다 <c>CommPopup</c> 으로 적고 있었다(내 정보의
/// 프로필 사진이 그랬다). 그러면 창마다 되는 일이 다르다 — 어떤 창은 확대가
/// 되고 어떤 창은 안 되고, 회전은 어디에도 없다. <b>스캔해서 올린 장애 사진이
/// 옆으로 누워 있는 것</b>이 헬프데스크에서 가장 흔한 그림인데, 돌려 볼 자리가
/// 없으면 받아서 열어 보는 수밖에 없다.
/// </para>
///
/// <para>
/// 그래서 창은 <b>레이아웃에 한 벌만</b> 두고(<c>ImagePreviewHost</c>) 화면은
/// 이 손잡이로 「이것을 띄워 달라」고만 한다. <c>ThemeDrawer</c> ·
/// <c>NotificationDrawer</c> 와 같은 구도다 — 여는 쪽과 그리는 쪽이 형제도
/// 부모 자식도 아니라 파라미터로 이을 수 없다.
/// </para>
///
/// <para>
/// [화면이 손댈 수 없는 그림도 있다]
/// </para>
///
/// <para>
/// 서식 편집기로 쓴 본문은 <c>&lt;img&gt;</c> 가 HTML 안에 박혀 있어
/// <c>@onclick</c> 을 걸 자리가 없다. 그쪽은 감싼 칸에
/// <c>data-imgview-scope</c> 만 적어 두면 <c>ImagePreviewHost</c> 가 문서에
/// 세워 둔 문지기가 받아 준다 — 그 칸의 그림 <b>전부</b>가 목록이 되어
/// 창 안에서 앞뒤로 넘어간다.
/// </para>
///
/// <para>
/// <b>scoped 다.</b> 한 사람이 연 그림이 남의 화면에 뜨면 안 된다.
/// </para>
/// </remarks>
public sealed class ImagePreview
{
    /// <summary>띄울 것이 바뀌었다. <c>ImagePreviewHost</c> 가 받는다.</summary>
    public event Action? Changed;

    /// <summary>지금 창에 걸린 그림들. 닫혀 있으면 빈 목록.</summary>
    public IReadOnlyList<ImagePreviewItem> Items { get; private set; } = [];

    /// <summary>그중 몇 번째를 보고 있나(0부터).</summary>
    public int Index { get; private set; }

    /// <summary>창이 열려 있나.</summary>
    public bool IsOpen => Items.Count > 0;

    /// <summary>몇 장인가.</summary>
    public int Count => Items.Count;

    /// <summary>지금 보이는 그림. 닫혀 있으면 <c>null</c>.</summary>
    public ImagePreviewItem? Current =>
        Index >= 0 && Index < Items.Count ? Items[Index] : null;

    /// <summary>
    /// 그림 한 장을 띄운다. 주소가 비면 <b>아무 일도 하지 않는다</b> —
    /// 부르는 쪽이 「그림이 없을 수도 있는 값」을 그대로 넘기는 자리가 많다.
    /// </summary>
    public void Open(string? url, string? title = null, string? downloadUrl = null)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        OpenAll([new ImagePreviewItem(url, title, downloadUrl)]);
    }

    /// <summary>
    /// 여러 장을 띄우고 <paramref name="index"/> 번째부터 보여 준다.
    /// </summary>
    /// <remarks>
    /// 이름이 <see cref="Open"/> 이 아닌 것은 <b>겹치기를 피하려고</b>다.
    /// 둘을 같은 이름으로 두면 <c>Open(null)</c> 이 어느 쪽인지 정해지지 않아
    /// 컴파일이 막힌다 — 그 글자는 「그림이 없을 수도 있는 값」을 넘기는
    /// 자리에서 실제로 나온다.
    /// </remarks>
    public void OpenAll(IEnumerable<ImagePreviewItem> items, int index = 0)
    {
        var list = items
            .Where(i => !string.IsNullOrWhiteSpace(i.Url))
            .ToArray();

        if (list.Length == 0)
        {
            return;
        }

        Items = list;
        Index = Math.Clamp(index, 0, list.Length - 1);
        Changed?.Invoke();
    }

    /// <summary>창을 닫는다. 이미 닫혀 있으면 아무 일도 하지 않는다.</summary>
    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        Items = [];
        Index = 0;
        Changed?.Invoke();
    }

    /// <summary>
    /// 앞뒤로 넘긴다. <b>끝에서 되돌아 온다</b> — 여러 장을 훑어보는 창이라
    /// 마지막에서 멈춰 서면 처음으로 가려고 창을 닫았다 다시 열게 된다.
    /// </summary>
    public void Step(int delta)
    {
        if (Count < 2 || delta == 0)
        {
            return;
        }

        Index = (Index + delta % Count + Count) % Count;
        Changed?.Invoke();
    }
}
