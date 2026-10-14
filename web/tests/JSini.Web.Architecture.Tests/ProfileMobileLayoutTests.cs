using System.Text.RegularExpressions;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>
/// 「내 정보」(<c>/admin/profile</c>)가 휴대폰에서 읽히는 모습을 지킨다.
///
/// <para>
/// [왜 글자로 검사하나]
/// </para>
///
/// <para>
/// 이 화면은 좁은 화면 규칙을 공들여 세워 둔 자리다(admin.css 의
/// <c>max-width: 767px</c> 묶음 — 얼굴 판을 눕히고, 탭 머리를 걷고, 손가락
/// 과녁을 키운다). 그런데 뒤에 붙은 세 자리(이메일 인증 · 전화번호 ·
/// 소셜 결합 목록)가 생김새를 <b>전부 인라인 <c>style</c> 로</b> 들고 있어서
/// <b>그 규칙이 닿을 자리가 아예 없었다.</b>
/// </para>
///
/// <para>
/// 360px 에서 재어 본 것 — 「사용자명」의 소셜 딱지가 「소셜결 / 합」 두 줄,
/// 전화번호 칸이 140px 로 찌그러져 적어 둔 번호도 안내 글도 못 읽음,
/// 결합 목록의 공급자 딱지가 「카 / 카 / 오」 세 줄. <b>빌드도 테스트도
/// 전부 통과했고</b> 넓은 화면에서는 멀쩡해 보였다 — 눈으로는 못 지킨다.
/// </para>
/// </summary>
public sealed class ProfileMobileLayoutTests
{
    /// <summary>
    /// 생김새는 <b>클래스로만</b> 적는다. 인라인 <c>style</c> 로 적으면 좁은
    /// 화면 규칙이 걸릴 자리가 없고(미디어 질의는 속성을 겨눌 수 없다),
    /// 테마 토큰도 그 자리에서 밝은 쪽 값으로 못 박히기 쉽다.
    /// </summary>
    [Fact]
    public void 내정보_화면에는_인라인_스타일이_없다()
    {
        var markup = File.ReadAllText(ProfileRazor());

        var inline = Regex.Matches(markup, "style\\s*=\\s*\"[^\"]*\"")
            .Select(m => m.Value)
            .ToList();

        Assert.True(
            inline.Count == 0,
            $"인라인 style 은 좁은 화면 규칙이 닿지 않는다. admin.css 로 옮길 것:\n{string.Join("\n", inline)}");
    }

    /// <summary>
    /// 늦게 붙은 세 자리가 좁은 화면 규칙을 실제로 갖고 있는가.
    /// 클래스만 붙이고 규칙을 안 적으면 증상이 고치기 전과 똑같다.
    /// </summary>
    [Fact]
    public void 늦게_붙은_세_자리에_좁은화면_규칙이_있다()
    {
        var phone = PhoneBlock();

        // 이메일 — 새 주소 칸이 한 줄을 통째로 갖는다(220px 못 박기를 푼 자리).
        Assert.Contains(".ad-mailbox__input", phone, StringComparison.Ordinal);

        // 전화번호 — 단추 줄이 넘칠 때 아래로 내려간다.
        Assert.Contains(".ad-phone {", phone, StringComparison.Ordinal);
        Assert.Contains("flex-wrap: wrap", phone, StringComparison.Ordinal);

        // 소셜 결합 목록 — 공급자 딱지를 윗줄로 올리고 글에 폭을 다 준다.
        Assert.Contains(".ad-sociallist__item", phone, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>세로로 세운 줄에 <c>flex-grow</c> 를 주면 단추의 키가 자란다.</b>
    /// 실제로 밟았다 — <c>.ad-mailbox .dxbl-btn { flex: 1 1 7rem }</c> 하나로
    /// 「이메일 변경」이 160px 짜리 덩어리가 됐다. 늘리는 규칙은 가로줄
    /// (<c>__line</c>)에만 건다.
    /// </summary>
    [Fact]
    public void 이메일_판의_늘리는_규칙은_가로줄에만_걸린다()
    {
        var phone = PhoneBlock();

        var grow = Regex.Matches(phone, @"\.ad-mailbox[^{]*\{[^}]*flex:\s*1\s+1[^}]*\}")
            .Select(m => m.Value)
            .ToList();

        Assert.All(grow, rule => Assert.DoesNotContain(
            ".ad-mailbox .dxbl-btn", rule, StringComparison.Ordinal));

        Assert.Contains(".ad-mailbox__line > .dxbl-btn", phone, StringComparison.Ordinal);
    }

    /// <summary>
    /// 화면이 쓰는 <c>jsini-badge--*</c> 수식어가 CSS 에 모두 있는가.
    ///
    /// <para>
    /// 없는 이름을 써도 <b>아무 오류가 안 난다</b> — 브라우저가 그냥 무시하고
    /// 기본 딱지(회색)로 그린다. 그래서 「대표」와 「보조」가, 「인증완료」와
    /// 「등록됨」이 글자만 다르고 생김새가 똑같은 채로 한참 굴러갔다
    /// (<c>--info</c> · <c>--success</c> · <c>--muted</c> 셋 모두 정의가 없었다).
    /// 날씨 특보의 「해제」도 같은 꼴이었다(<c>--ok</c>).
    /// </para>
    /// </summary>
    [Fact]
    public void 화면이_쓰는_딱지_수식어가_모두_CSS_에_있다()
    {
        var defined = Directory
            .EnumerateFiles(Path.Combine(SolutionRoot(), "src"), "*.css", SearchOption.AllDirectories)
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"\.(jsini-badge--[a-z0-9-]+)")
                .Select(m => m.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);

        var used = Directory
            .EnumerateFiles(Path.Combine(SolutionRoot(), "src"), "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"(?<!\.)(jsini-badge--[a-z0-9-]+)")
                .Select(m => m.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);

        var missing = used.Except(defined).OrderBy(x => x, StringComparer.Ordinal).ToList();

        Assert.True(
            missing.Count == 0,
            $"CSS 에 없는 딱지 수식어는 조용히 기본 회색으로 그려진다: {string.Join(", ", missing)}");
    }

    /// <summary>
    /// 경계 767px 는 화면(<c>DxLayoutBreakpoint</c>)과 CSS 가 <b>같은 값</b>이라야
    /// 한다. 어긋나면 그 사이 폭에서 어느 쪽도 아닌 화면이 나온다.
    /// </summary>
    [Fact]
    public void 휴대폰_경계가_화면과_CSS_에서_같다()
    {
        Assert.Contains("MaxWidth=\"767\"", File.ReadAllText(ProfileRazor()), StringComparison.Ordinal);
        Assert.Contains(".ad-profile__tabs .dxbl-tabs-tablist", PhoneBlock(), StringComparison.Ordinal);
    }

    /// <summary>
    /// 「내 정보」의 좁은 화면 묶음 — <c>.ad-profile</c> 을 품은
    /// <c>@media (max-width: 767px)</c> 하나를 글자로 잘라 돌려준다.
    /// </summary>
    private static string PhoneBlock()
    {
        var css = File.ReadAllText(AdminCss());

        foreach (Match m in Regex.Matches(css, @"@media \(max-width: 767px\) \{"))
        {
            var start = m.Index + m.Length;
            var depth = 1;
            var i = start;

            while (i < css.Length && depth > 0)
            {
                if (css[i] == '{') depth++;
                else if (css[i] == '}') depth--;
                i++;
            }

            var body = css[start..(i - 1)];

            if (body.Contains(".ad-profile", StringComparison.Ordinal))
            {
                return body;
            }
        }

        Assert.Fail("admin.css 에서 내 정보의 좁은 화면 묶음을 찾지 못했다.");
        return string.Empty;
    }

    private static string ProfileRazor() => Path.Combine(
        SolutionRoot(), "src", "Apps", "JSini.Web.Admin", "Components", "Pages", "Profile.razor");

    private static string AdminCss() => Path.Combine(
        SolutionRoot(), "src", "Apps", "JSini.Web.Admin", "wwwroot", "admin.css");

    private static string SolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
