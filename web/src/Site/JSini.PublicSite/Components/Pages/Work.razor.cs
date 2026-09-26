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

    /// <summary>
    /// 사례 그림의 파일 이름 목록. 기동 때 한 번 훑는다.
    ///
    /// <c>work.funeral</c> → <c>wwwroot/assets/work/funeral.svg</c> 다.
    ///
    /// [DB 에 열을 더하지 않는 이유]
    ///
    /// 그림은 문구와 달리 화면에서 편집하는 것이 아니라 저장소에서 만들어
    /// 배포되는 것이라(브랜드 자산과 같다), 정적 파일로 두는 편이 맞다.
    ///
    /// [주소를 문자열로 조립하지 않는 이유]
    ///
    /// 사례 다섯 중 그림이 있는 것과 없는 것이 섞여 있다. 조립해서 내보내면
    /// 없는 그림도 &lt;img&gt; 로 일단 나가고 404 가 돌아온 뒤에야 지워진다 —
    /// 그 사이에 깨진 그림과 "재현 이미지입니다" 라는 설명만 남는다.
    /// 실제로 있는 파일만 훑어 두면 <b>없는 그림은 애초에 마크업에 들어가지 않는다.</b>
    /// </summary>
    private static string[]? _available;

    private string[] Available => _available ??= LoadAvailable();

    private string[] LoadAvailable()
    {
        var dir = Path.Combine(Environment.WebRootPath, "assets", "work");

        return Directory.Exists(dir)
            ? [.. Directory.EnumerateFiles(dir, "*.svg").Select(Path.GetFileNameWithoutExtension).OfType<string>()]
            : [];
    }

    /// <summary>
    /// 한 사례에 그림이 여럿일 수 있다. <c>work.utility</c> 면
    /// <c>utility.svg</c> · <c>utility-trend.svg</c> 처럼 이름 뒤에 하이픈을 붙여 늘린다.
    ///
    /// <c>utility</c> 로 시작하는 것을 다 집으면 <c>utility2.svg</c> 같은 남의 이름까지
    /// 걸리므로, <b>정확히 같거나 하이픈이 이어지는 것</b>만 집는다.
    /// </summary>
    private List<string> Mockups(string sectionKey)
    {
        var name = sectionKey.StartsWith("work.", StringComparison.Ordinal)
            ? sectionKey["work.".Length..]
            : sectionKey;

        var bases = Available
            .Where(b => b == name || b.StartsWith(name + "-", StringComparison.Ordinal))
            // 이름만 있는 것(대표 화면)이 먼저, 하이픈이 붙은 것은 그 뒤에 이름순.
            // 그냥 정렬하면 `-`(0x2D)가 `.`(0x2E)보다 앞서서 utility-trend 가
            // utility 를 제치고 올라온다 — 대표 화면이 두 번째로 밀린다.
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
