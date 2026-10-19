using Microsoft.AspNetCore.Components;
using JSini.PublicSite.Api;
using JSini.PublicSite.Site;

namespace JSini.PublicSite.Components.Pages;

public partial class Work
{
    [Inject] private SiteApi Api { get; set; } = default!;
    [Inject] private IWebHostEnvironment Environment { get; set; } = default!;

    [Parameter] public string? Locale { get; set; }

    private Messages T => SiteMessages.For(Locale);
    private string L => SiteMessages.Normalize(Locale);

    private List<Section> _sections = [];

    /// <summary>사례 그림의 파일 이름 목록. 기동 때 한 번 훑는다. 그림은 저장소에서 만들어 배포되는 자산이라 DB 가 아니라 정적 파일이다. 실제로 있는 파일만 훑어 두면 없는 그림은 마크업에 들어가지 않는다(조립하면 404 가 돌아온 뒤에야 지워진다).</summary>
    private static string[]? _available;

    private string[] Available => _available ??= LoadAvailable();

    private string[] LoadAvailable()
    {
        var dir = Path.Combine(Environment.WebRootPath, "assets", "work");

        return Directory.Exists(dir)
            ? [.. Directory.EnumerateFiles(dir, "*.svg").Select(Path.GetFileNameWithoutExtension).OfType<string>()]
            : [];
    }

    /// <summary>한 사례에 그림이 여럿일 수 있다. 이름 뒤에 하이픈이 이어진 것만 집는다(utility2.svg 같은 남의 이름을 걸러낸다).</summary>
    private List<string> Mockups(string sectionKey)
    {
        var name = sectionKey.StartsWith("work.", StringComparison.Ordinal)
            ? sectionKey["work.".Length..]
            : sectionKey;

        var bases = Available
            .Where(b => b == name || b.StartsWith(name + "-", StringComparison.Ordinal))
            // 이름만 있는 것(대표 화면)이 먼저다. 그냥 정렬하면 '-' 가 '.' 보다 앞서 utility-trend 가 대표 화면을 제친다.
            .OrderBy(b => b == name ? 0 : 1)
            .ThenBy(b => b, StringComparer.Ordinal)
            .ToList();

        return [.. bases.Select(b => $"assets/work/{b}.svg")];
    }

    protected override async Task OnInitializedAsync()
    {
        _sections = await Api.SectionsAsync(L, "work.");
        Api.RecordVisit($"/{L}/work", L);
    }
}
