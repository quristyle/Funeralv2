using Microsoft.AspNetCore.Components;
using JSini.PublicSite.Api;
using JSini.PublicSite.Site;

namespace JSini.PublicSite.Components.Pages;

public partial class Downloads
{
    [Inject] private SiteApi Api { get; set; } = default!;

    [Parameter] public string? Locale { get; set; }

    /// <summary>거를 분류. 없으면 전체.</summary>
    [SupplyParameterFromQuery(Name = "category")]
    public string? Category { get; set; }

    private Messages T => SiteMessages.For(Locale);
    private string L => SiteMessages.Normalize(Locale);

    private List<DownloadItem> _items = [];

    private List<string> Categories =>
        [.. _items.Select(x => x.Category)
                  .Where(c => !string.IsNullOrWhiteSpace(c))
                  .Select(c => c!)
                  .Distinct(StringComparer.Ordinal)];

    private List<DownloadItem> Shown => string.IsNullOrEmpty(Category)
        ? _items
        : [.. _items.Where(x => string.Equals(x.Category, Category, StringComparison.Ordinal))];

    /// <summary>1MB 를 넘으면 MB, 아니면 KB. 원본과 같은 규칙이다.</summary>
    private static string FormatSize(long bytes)
    {
        var mb = bytes / 1024d / 1024d;
        return mb >= 1 ? $"{mb:0.0} MB" : $"{Math.Round(bytes / 1024d)} KB";
    }

    protected override async Task OnInitializedAsync()
    {
        // **분류로 걸러 달라고 서버에 넘기지 않는다.** 분류 단추를 그리려면
        // 어차피 전체 목록이 필요하고, 자료실은 목록이 길어야 수십 건이다.
        // 분류마다 따로 부르면 왕복만 늘고 단추가 사라진다.
        _items = await Api.DownloadsAsync(L);
        Api.RecordVisit($"/{L}/downloads", L);
    }
}
