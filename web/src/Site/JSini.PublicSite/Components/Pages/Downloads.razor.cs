using Microsoft.AspNetCore.Components;
using JSini.PublicSite.Api;
using JSini.PublicSite.Site;

namespace JSini.PublicSite.Components.Pages;

public partial class Downloads
{
    [Inject] private SiteApi Api { get; set; } = default!;
    [Inject] private IWebHostEnvironment Environment { get; set; } = default!;

    [Parameter] public string? Locale { get; set; }

    /// <summary>거를 분류. 없으면 전체.</summary>
    [SupplyParameterFromQuery(Name = "category")]
    public string? Category { get; set; }

    private Messages T => SiteMessages.For(Locale);
    private string L => SiteMessages.Normalize(Locale);
    private string AndroidAppCategory => L == "en" ? "Android app" : "안드로이드 앱";

    private List<DownloadItem> _items = [];
    private string? _androidAppVersion;
    private long _androidAppSize;
    private bool _androidAppAvailable;

    private bool ShowAndroidApp =>
        _androidAppAvailable &&
        (string.IsNullOrEmpty(Category) || string.Equals(Category, AndroidAppCategory, StringComparison.Ordinal));

    private List<string> Categories
    {
        get
        {
            var categories = _items.Select(x => x.Category)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c!)
                .ToList();
            if (_androidAppAvailable)
            {
                categories.Add(AndroidAppCategory);
            }

            return categories.Distinct(StringComparer.Ordinal).ToList();
        }
    }

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
        // 분류로 걸러 달라고 서버에 넘기지 않는다. 분류 단추를 그리려면 전체 목록이 필요하고, 분류마다 부르면 왕복만 늘어난다.
        _items = await Api.DownloadsAsync(L);
        await LoadAndroidAppAsync();
        Api.RecordVisit($"/{L}/downloads", L);
    }

    private async Task LoadAndroidAppAsync()
    {
        var versionFile = Environment.WebRootFileProvider.GetFileInfo("downloads/android-app-version.txt");
        if (!versionFile.Exists)
        {
            return;
        }

        await using var stream = versionFile.CreateReadStream();
        using var reader = new StreamReader(stream);
        var version = (await reader.ReadToEndAsync()).Trim();
        if (!Version.TryParse(version, out _))
        {
            return;
        }

        var apk = Environment.WebRootFileProvider.GetFileInfo($"downloads/jsini-portal-{version}.apk");
        if (!apk.Exists || apk.Length <= 0)
        {
            return;
        }

        _androidAppVersion = version;
        _androidAppSize = apk.Length;
        _androidAppAvailable = true;
    }

    private string AndroidAppFileName => $"jsini-portal-{_androidAppVersion}.apk";
}
