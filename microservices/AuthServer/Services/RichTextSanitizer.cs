using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace AuthServer.Services;

/// <summary>
/// 서식 있는 본문(HTML)을 저장 전에 세탁한다.
/// </summary>
/// <remarks>
/// 왜 필요한가.
///
/// 화면의 편집기(tiptap)는 자기 스키마에 없는 태그를 알아서 버린다. 하지만 그것은
/// **화면 쪽 정리**일 뿐이다. 같은 API 에 직접 요청을 보내면 무엇이든 들어온다.
/// 그 본문은 조회 화면에서 `v-html` 로 그려지므로, 걸러지지 않으면 남의 화면에서
/// 스크립트가 도는 길이 된다.
///
/// Q&amp;A 는 **일반 사용자도 본문을 쓴다.** 관리자만 쓰는 공지와 사정이 다르다.
/// 그래서 도움말(F.A.Q · Q&amp;A) 본문은 여기를 반드시 지나게 한다.
///
/// 방식은 허용 목록이다. 목록에 없는 것은 지운다 —
/// 금지 목록으로 막으면 새 태그·속성이 생길 때마다 구멍이 난다.
///
/// [영상 넣기]
/// <c>allowEmbeds</c> 를 켜면 <c>&lt;iframe&gt;</c> 을 남긴다. 다만 아무 주소나 두지 않고
/// <see cref="EmbedHosts"/> 에 적은 영상 서비스만 통과시킨다. 임의 주소를 허용하면
/// 관리 화면 안에 남의 페이지를 띄워 버튼을 가리는 길(클릭재킹)이 열린다.
/// 켜는 자리는 두 곳뿐이다 — F.A.Q 답변(관리자만 쓴다)과 **관리자가 쓴** Q&amp;A 글.
/// </remarks>
public static class RichTextSanitizer
{
    /// <summary>남겨 둘 태그</summary>
    private static readonly HashSet<string> AllowedTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "p", "br", "div", "span", "hr",
        "strong", "b", "em", "i", "u", "s", "del", "ins", "mark", "sub", "sup",
        "h1", "h2", "h3", "h4", "h5", "h6",
        "ul", "ol", "li", "blockquote", "pre", "code",
        "a", "img",
        "table", "thead", "tbody", "tfoot", "tr", "th", "td", "caption"
    };

    /// <summary>내용까지 통째로 버릴 태그. 안쪽 글자를 남기면 코드가 새어 나온다.</summary>
    /// <remarks>
    /// <c>iframe</c> 도 기본은 여기 있다. <c>allowEmbeds</c> 를 켠 경우에만 예외로 살린다.
    /// </remarks>
    private static readonly HashSet<string> DropWithContent = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style", "iframe", "object", "embed", "form", "template",
        "noscript", "svg", "math", "link", "meta", "base", "frame", "frameset"
    };

    /// <summary>
    /// 영상 넣기를 허용할 호스트. 여기 없는 주소의 iframe 은 버린다.
    /// </summary>
    /// <remarks>
    /// 새 서비스를 늘릴 일이 생기면 이 목록에 한 줄 더하면 된다.
    /// 늘리기 전에 그 서비스가 **남의 페이지를 그대로 띄워 주는 통로**가 아닌지 확인한다.
    /// </remarks>
    private static readonly HashSet<string> EmbedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "www.youtube.com", "youtube.com",
        "www.youtube-nocookie.com", "youtube-nocookie.com",
        "youtu.be",
        "player.vimeo.com",
        "tv.naver.com",
        "play-tv.kakao.com"
    };

    /// <summary>iframe 에 남길 속성</summary>
    /// <remarks>
    /// <c>srcdoc</c> 은 절대 넣지 않는다 — 주소 검사를 지나지 않고 임의 HTML 을 실행한다.
    /// <c>name</c> 도 넣지 않는다(창 이름으로 다른 프레임을 겨냥할 수 있다).
    /// </remarks>
    private static readonly HashSet<string> IframeAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "src", "width", "height", "title", "frameborder",
        "allow", "allowfullscreen", "referrerpolicy", "loading", "style"
    };

    /// <summary>
    /// <c>style</c> 에 남길 CSS 속성. 크기와 여백만 남긴다.
    /// </summary>
    /// <remarks>
    /// style 을 그대로 두면 <c>position:fixed</c> 로 화면 전체를 덮을 수 있다.
    /// 넣으려는 것은 "가로 816, 높이 480" 같은 크기 지정이므로 그것만 통과시킨다.
    /// </remarks>
    private static readonly HashSet<string> StyleProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "width", "height", "min-width", "min-height", "max-width", "max-height",
        "aspect-ratio", "margin", "margin-top", "margin-bottom",
        "margin-left", "margin-right", "border", "border-radius", "display"
    };

    /// <summary>모든 태그에 허용할 속성</summary>
    private static readonly HashSet<string> GlobalAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "class", "title", "dir", "lang"
    };

    /// <summary>태그별로 더 허용할 속성</summary>
    private static readonly Dictionary<string, HashSet<string>> TagAttributes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["a"] = new(StringComparer.OrdinalIgnoreCase) { "href", "target", "rel" },
            ["img"] = new(StringComparer.OrdinalIgnoreCase) { "src", "alt", "width", "height" },
            ["td"] = new(StringComparer.OrdinalIgnoreCase) { "colspan", "rowspan" },
            ["th"] = new(StringComparer.OrdinalIgnoreCase) { "colspan", "rowspan", "scope" },
            ["ol"] = new(StringComparer.OrdinalIgnoreCase) { "start" },
            ["code"] = new(StringComparer.OrdinalIgnoreCase) { "data-language" }
        };

    /// <summary>주소로 받아들일 형태. 그 밖(javascript: 등)은 버린다.</summary>
    private static readonly Regex SafeUrl = new(
        @"^(https?://|mailto:|tel:|/|\./|\#)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// 본문을 세탁해서 돌려준다. 비어 있으면 그대로 돌려준다.
    /// </summary>
    /// <param name="html">화면에서 받은 본문</param>
    /// <param name="allowEmbeds">
    /// 참이면 영상 넣기(<c>&lt;iframe&gt;</c>)를 허용한다. 허용해도 주소는
    /// <see cref="EmbedHosts"/> 에 적은 영상 서비스만 통과한다.
    /// 관리자가 쓴 본문에만 켠다.
    /// </param>
    public static string? Sanitize(string? html, bool allowEmbeds = false)
    {
        if (string.IsNullOrWhiteSpace(html)) return html;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        Clean(doc.DocumentNode, allowEmbeds);

        return doc.DocumentNode.InnerHtml;
    }

    /// <summary>
    /// 알맹이가 없는 본문인지. 태그를 벗겨 낸 글자와 이미지 둘 다 없으면 비어 있다고 본다.
    /// </summary>
    /// <remarks>
    /// 편집기는 아무것도 안 써도 `&lt;p&gt;&lt;/p&gt;` 를 보낸다.
    /// 문자열 길이만 보면 이걸 '내용 있음' 으로 받아들인다.
    /// 이미지만 붙여넣은 본문은 글자가 없어도 내용이 있는 것이다.
    /// </remarks>
    public static bool IsEmpty(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return true;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // 이미지나 영상만 있는 본문은 글자가 없어도 내용이 있는 것이다.
        if (doc.DocumentNode.SelectSingleNode("//img | //iframe") is not null) return false;

        var text = HtmlEntity.DeEntitize(doc.DocumentNode.InnerText) ?? string.Empty;

        // 줄바꿈용 공백(&nbsp; 포함)만 남은 경우도 비어 있다고 본다.
        return string.IsNullOrWhiteSpace(text.Replace(' ', ' '));
    }

    /// <summary>
    /// 노드를 훑어 허용 목록에 맞게 고친다.
    /// </summary>
    /// <remarks>
    /// 자식을 지우면서 순회하면 목록이 흔들리므로 미리 복사해 두고 돈다.
    /// </remarks>
    private static void Clean(HtmlNode node, bool allowEmbeds)
    {
        foreach (var child in node.ChildNodes.ToList())
        {
            switch (child.NodeType)
            {
                case HtmlNodeType.Comment:
                    // 주석은 남길 이유가 없다. 조건부 주석으로 코드를 숨길 수도 있다.
                    child.Remove();
                    continue;

                case HtmlNodeType.Text:
                    continue;

                case HtmlNodeType.Element:
                    break;

                default:
                    continue;
            }

            var isIframe = child.Name.Equals("iframe", StringComparison.OrdinalIgnoreCase);

            // 영상 넣기를 켠 경우의 iframe 만 예외로 살린다.
            if (isIframe && allowEmbeds)
            {
                // iframe 안쪽 내용은 "이 브라우저는 프레임을 지원하지 않습니다" 같은
                // 대체 문구 자리다. 남길 이유가 없고 코드가 숨을 수 있으니 비운다.
                child.RemoveAllChildren();

                if (CleanIframe(child)) continue;

                child.Remove();
                continue;
            }

            if (DropWithContent.Contains(child.Name))
            {
                child.Remove();
                continue;
            }

            // 안쪽을 먼저 정리한다. 바깥을 벗길 때 이미 깨끗한 상태여야 한다.
            Clean(child, allowEmbeds);

            if (AllowedTags.Contains(child.Name))
            {
                CleanAttributes(child);
                continue;
            }

            // 허용 목록에 없는 태그는 껍데기만 벗기고 안쪽 내용은 살린다.
            // (예: <font>글자</font> → 글자)
            Unwrap(child);
        }
    }

    /// <summary>
    /// 영상 iframe 을 손질한다. 남겨도 되는 것이면 참을 돌려준다.
    /// </summary>
    /// <remarks>
    /// 주소가 허용 목록 밖이면 거짓을 돌려준다 — 부르는 쪽이 태그를 지운다.
    /// 상대경로·<c>javascript:</c>·<c>data:</c> 는 모두 여기서 걸린다
    /// (호스트를 뽑을 수 없으므로 통과하지 못한다).
    /// </remarks>
    private static bool CleanIframe(HtmlNode node)
    {
        var src = node.GetAttributeValue("src", string.Empty).Trim();

        if (!Uri.TryCreate(src, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) return false;
        if (!EmbedHosts.Contains(uri.Host)) return false;

        foreach (var attribute in node.Attributes.ToList())
        {
            var name = attribute.Name;

            if (name.StartsWith("on", StringComparison.OrdinalIgnoreCase) ||
                !IframeAttributes.Contains(name))
            {
                attribute.Remove();
                continue;
            }

            if (name.Equals("style", StringComparison.OrdinalIgnoreCase))
            {
                var cleaned = CleanStyle(attribute.Value);
                if (string.IsNullOrEmpty(cleaned)) attribute.Remove();
                else attribute.Value = cleaned;
            }
        }

        // 영상 서비스로 나가는 요청에 우리 주소 전체를 실어 보내지 않는다.
        node.SetAttributeValue("referrerpolicy", "strict-origin-when-cross-origin");

        // http 로 들어와도 https 로 바꿔 둔다. 관리 화면은 https 로 서비스되므로
        // 그대로 두면 브라우저가 혼합 콘텐츠로 막아 아무것도 보이지 않는다.
        if (uri.Scheme == Uri.UriSchemeHttp)
        {
            node.SetAttributeValue("src",
                new UriBuilder(uri) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri.ToString());
        }

        return true;
    }

    /// <summary>
    /// <c>style</c> 에서 크기·여백 관련 속성만 남긴다.
    /// </summary>
    private static string CleanStyle(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var kept = new List<string>();

        foreach (var declaration in value.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = declaration.Split(':', 2);
            if (parts.Length != 2) continue;

            var property = parts[0].Trim();
            var setting = parts[1].Trim();

            if (!StyleProperties.Contains(property)) continue;

            // url(...) 은 크기 지정에 쓸 일이 없다. 바깥으로 요청이 나가는 길이라 막는다.
            if (setting.Contains("url(", StringComparison.OrdinalIgnoreCase)) continue;

            kept.Add($"{property}:{setting}");
        }

        return kept.Count == 0 ? string.Empty : string.Join(';', kept);
    }

    /// <summary>태그를 없애고 자식들을 그 자리에 남긴다.</summary>
    private static void Unwrap(HtmlNode node)
    {
        var parent = node.ParentNode;
        foreach (var child in node.ChildNodes.ToList())
        {
            parent.InsertBefore(child, node);
        }
        node.Remove();
    }

    private static void CleanAttributes(HtmlNode node)
    {
        TagAttributes.TryGetValue(node.Name, out var extra);

        foreach (var attribute in node.Attributes.ToList())
        {
            var name = attribute.Name;

            // on* 은 전부 실행 지점이다.
            var allowed = !name.StartsWith("on", StringComparison.OrdinalIgnoreCase)
                          && (GlobalAttributes.Contains(name) || (extra?.Contains(name) ?? false));

            if (!allowed)
            {
                attribute.Remove();
                continue;
            }

            if ((name.Equals("href", StringComparison.OrdinalIgnoreCase) ||
                 name.Equals("src", StringComparison.OrdinalIgnoreCase)) &&
                !SafeUrl.IsMatch(attribute.Value?.Trim() ?? string.Empty))
            {
                attribute.Remove();
            }
        }

        // 새 창으로 열리는 링크는 원래 창을 넘겨주지 않게 한다.
        if (node.Name.Equals("a", StringComparison.OrdinalIgnoreCase) &&
            node.GetAttributeValue("target", string.Empty).Length > 0)
        {
            node.SetAttributeValue("target", "_blank");
            node.SetAttributeValue("rel", "noopener noreferrer");
        }
    }
}
