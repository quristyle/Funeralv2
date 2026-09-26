using Microsoft.AspNetCore.Components;
using JSini.Web.Shell.Security;

namespace JSini.Web.Shell.Components;

/// <summary>단추 한 개. 서버 목록에 있으면 <see cref="Enabled"/> 가 참이다.</summary>
/// <param name="Key">공급자 열쇠 (<c>kakao</c> …).</param>
/// <param name="DisplayName">화면에 적을 이름.</param>
/// <param name="Enabled">누를 수 있는가. 거짓이면 「준비 중」으로 선다.</param>
public sealed record SocialButtonItem(string Key, string DisplayName, bool Enabled);

public partial class SocialButtons
{
    /// <summary>
    /// 늘 자리를 보이는 공급자. <b>순서가 곧 화면의 순서다</b> — 사내 사용자가
    /// 가장 많이 쓰는 것부터 둔다.
    /// </summary>
    private static readonly (string Key, string DisplayName)[] Known =
    [
        ("kakao", "카카오"),
        ("naver", "네이버"),
        ("google", "구글"),
    ];

    /// <summary>서버가 알려 준, 지금 쓸 수 있는 공급자.</summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<SocialProvider> Providers { get; set; } = [];

    /// <summary>단추가 갈 주소. 로그인은 되돌아올 곳을 붙이고 가입 신청은 안 붙인다.</summary>
    [Parameter]
    public Func<string, string> Href { get; set; } = key => $"/social/{key}/start";

    /// <summary>단추 글자의 꼬리. 「카카오 + 로 로그인」처럼 붙는다.</summary>
    [Parameter]
    public string Verb { get; set; } = "로 계속하기";

    /// <summary>
    /// 참이면 누를 때 [로그인 유지] 체크를 주소에 싣는 표시를 단다
    /// (<c>data-social-link</c> — 로그인 화면의 스크립트가 읽는다).
    /// </summary>
    [Parameter]
    public bool CarryKeepSignedIn { get; set; }

    private List<SocialButtonItem> Items { get; set; } = [];

    protected override void OnParametersSet()
    {
        var enabled = Providers.ToDictionary(p => p.Key, StringComparer.OrdinalIgnoreCase);

        Items = [.. Known.Select(k => enabled.TryGetValue(k.Key, out var p)
            ? new SocialButtonItem(k.Key, p.DisplayName, true)
            : new SocialButtonItem(k.Key, k.DisplayName, false))];

        // 설정만으로 더 붙인 공급자. 그림이 없을 뿐 똑같이 누를 수 있다.
        Items.AddRange(Providers
            .Where(p => !Known.Any(k => string.Equals(k.Key, p.Key, StringComparison.OrdinalIgnoreCase)))
            .Select(p => new SocialButtonItem(p.Key.ToLowerInvariant(), p.DisplayName, true)));
    }

    /// <summary>
    /// 받침이 있으면 「으로」, 없으면 「로」 — 「카카오로」 · 「구글로」 · 「깃허브로」.
    /// 한글이 아닌 이름은 「로」로 둔다.
    /// </summary>
    private string Label(SocialButtonItem item)
    {
        var name = item.DisplayName;
        var verb = Verb;

        if (verb.StartsWith('로') && name.Length > 0)
        {
            var last = name[^1];
            var hasFinal = last is >= '가' and <= '힣' && (last - '가') % 28 is not (0 or 8);
            if (hasFinal)
            {
                verb = "으" + verb;
            }
        }

        return name + verb;
    }

    private static string Initial(SocialButtonItem item) =>
        item.DisplayName.Length > 0 ? item.DisplayName[..1].ToUpperInvariant() : "?";
}
