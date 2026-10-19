using System.Text;
using JSini.Web.Components.Data;

namespace JSini.Web.Components.Layout;

/// <summary>공지 본문(HTML)을 화면에 넣기 전에 거른다. 서식이 목적이라 글자로 뭉갤 수 없고 MarkupString 으로 그리면 본문이 브라우저에서 실행되므로, 관리자 계정이 털리면 로그인 전 화면까지 모든 사용자에게 뜬다 — 그래서 아는 태그와 속성만 통과시킨다(남기는 목록).
/// 지우는 목록(script 제거)은 onerror · javascript: · svg 안의 이벤트를 놓친다. 모르는 태그는 껍데기만 버리고 글자는 남긴다. 규칙은 NoticeHtmlTests 가 지킨다.</summary>
public static class NoticeHtml
{
    /// <summary>남겨 두는 태그. 서식 편집기가 실제로 뱉는 것들이다.</summary>
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "p", "br", "div", "span", "strong", "b", "em", "i", "u", "s", "strike", "del", "ins",
        "sub", "sup", "h1", "h2", "h3", "h4", "h5", "h6",
        "ul", "ol", "li", "blockquote", "pre", "code", "hr",
        "a", "img",
        "table", "thead", "tbody", "tfoot", "tr", "th", "td", "caption", "colgroup", "col",
    };

    /// <summary>껍데기뿐 아니라 안까지 통째로 버리는 태그. 나머지 모르는 태그는 글자를 남기지만 이것들은 남기면 style 안의 CSS 나 script 안의 코드가 글자로 쏟아진다.</summary>
    private static readonly HashSet<string> Dropped = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style", "iframe", "object", "embed", "noscript", "template",
        "svg", "math", "form", "input", "button", "select", "textarea", "link", "meta", "base",
    };

    /// <summary>닫는 짝이 없는 태그.</summary>
    private static readonly HashSet<string> Void = new(StringComparer.OrdinalIgnoreCase)
    {
        "br", "hr", "img", "col",
    };

    /// <summary>태그를 가리지 않고 통과시키는 속성.</summary>
    private static readonly HashSet<string> GlobalAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "class", "style", "title", "dir", "lang",
    };

    /// <summary>태그마다 더 통과시키는 속성.</summary>
    private static readonly Dictionary<string, string[]> TagAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["a"] = ["href", "target", "rel"],
        ["img"] = ["src", "alt", "width", "height"],
        ["td"] = ["colspan", "rowspan"],
        ["th"] = ["colspan", "rowspan", "scope"],
        ["col"] = ["span", "width"],
        ["colgroup"] = ["span"],
        ["ol"] = ["start", "type"],
    };

    /// <summary>숫자만 받는 속성. 글자가 섞이면 버린다.</summary>
    private static readonly HashSet<string> NumericAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "width", "height", "colspan", "rowspan", "start", "span",
    };

    /// <summary>링크로 받아 주는 시작. <c>javascript:</c> 는 여기 없으므로 걸러진다.</summary>
    private static readonly string[] LinkSchemes = ["http://", "https://", "mailto:", "tel:", "/", "#"];

    /// <summary>그림으로 받아 주는 시작.</summary>
    private static readonly string[] ImageSchemes = ["http://", "https://", "/", "data:image/"];

    /// <summary>style 값에 들어 있으면 그 속성을 통째로 버리는 조각들. 색·정렬만 오는 자리라 url() 이나 @import 가 보이면 바깥으로 나가려는 것이다.</summary>
    private static readonly string[] StyleBanned = ["url(", "expression", "javascript:", "@import", "behavior", "/*"];

    /// <summary>본문을 화면에 넣어도 되는 HTML 로 바꾼다.</summary>
    /// <param name="html">정화할 원본 HTML.</param>
    /// <param name="imageSourceTransform">정화·중계된 그림 주소를 바꿀 선택 변환기.</param>
    /// <param name="lazyLoadImages">그림을 브라우저가 필요할 때 불러오게 할지 여부.</param>
    public static string Sanitize(
        string? html,
        Func<string, string>? imageSourceTransform = null,
        bool lazyLoadImages = false)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var output = new StringBuilder(html.Length);

        // 연 태그를 쌓아 둔다. 닫는 짝이 없거나 순서가 어긋난 것을 여기서 바로잡는다 —
        // 그대로 내보내면 우리 레이아웃의 <div> 가 공지 본문에 먹힌다.
        var open = new List<string>();

        var i = 0;

        while (i < html.Length)
        {
            var c = html[i];

            if (c != '<')
            {
                output.Append(c);
                i++;
                continue;
            }

            // 주석과 <!DOCTYPE …>
            if (html.AsSpan(i).StartsWith("<!--", StringComparison.Ordinal))
            {
                var end = html.IndexOf("-->", i + 4, StringComparison.Ordinal);
                i = end < 0 ? html.Length : end + 3;
                continue;
            }

            if (i + 1 < html.Length && html[i + 1] == '!')
            {
                var end = html.IndexOf('>', i);
                i = end < 0 ? html.Length : end + 1;
                continue;
            }

            if (!TryReadTag(html, i, out var tag))
            {
                // 태그가 아니라 그냥 부등호다. 남겨 두면 다음에 오는 글자와 붙어
                // 태그가 될 수 있으므로 여기서 escape 한다.
                output.Append("&lt;");
                i++;
                continue;
            }

            i = tag.End;

            if (Dropped.Contains(tag.Name))
            {
                if (!tag.IsClosing && !tag.SelfClosing)
                {
                    i = SkipToClose(html, i, tag.Name);
                }

                continue;
            }

            if (!Allowed.Contains(tag.Name))
            {
                // 껍데기만 버리고 안의 글자는 그대로 흐르게 둔다.
                continue;
            }

            if (tag.IsClosing)
            {
                CloseUpTo(output, open, tag.Name);
                continue;
            }

            AppendOpenTag(output, tag, imageSourceTransform, lazyLoadImages);

            if (!Void.Contains(tag.Name) && !tag.SelfClosing)
            {
                open.Add(tag.Name);
            }
        }

        for (var k = open.Count - 1; k >= 0; k--)
        {
            output.Append("</").Append(open[k]).Append('>');
        }

        return output.ToString();
    }

    /// <summary>
    /// 여는 태그를 걸러 낸 속성만 붙여 내보낸다.
    /// </summary>
    private static void AppendOpenTag(
        StringBuilder output,
        Tag tag,
        Func<string, string>? imageSourceTransform,
        bool lazyLoadImages)
    {
        output.Append('<').Append(tag.Name.ToLowerInvariant());

        var newTab = false;

        foreach (var (name, value) in ReadAttributes(tag.Attributes))
        {
            if (!IsAllowedAttribute(tag.Name, name))
            {
                continue;
            }

            var clean = CleanValue(name, value);

            if (clean is null)
            {
                continue;
            }

            if (tag.Name.Equals("img", StringComparison.OrdinalIgnoreCase)
                && name.Equals("src", StringComparison.OrdinalIgnoreCase)
                && imageSourceTransform is not null)
            {
                clean = imageSourceTransform(clean);
            }

            if (name.Equals("rel", StringComparison.OrdinalIgnoreCase))
            {
                // 아래에서 우리가 붙인다. 여기서도 붙이면 두 번 나온다.
                continue;
            }

            if (name.Equals("target", StringComparison.OrdinalIgnoreCase))
            {
                newTab = true;
                continue;
            }

            output.Append(' ').Append(name.ToLowerInvariant())
                  .Append("=\"").Append(EscapeAttribute(clean)).Append('"');
        }

        if (lazyLoadImages && tag.Name.Equals("img", StringComparison.OrdinalIgnoreCase))
        {
            output.Append(" loading=\"lazy\"");
        }

        // 새 창으로 여는 링크에는 반드시 rel 을 붙인다. 없으면 열린 쪽이
        // `window.opener` 로 우리 화면 주소를 바꿀 수 있다.
        if (newTab)
        {
            output.Append(" target=\"_blank\" rel=\"noopener noreferrer\"");
        }

        if (Void.Contains(tag.Name))
        {
            output.Append(" />");
            return;
        }

        output.Append('>');

        // `<p/>` 처럼 짝 없이 닫아 버린 것. 쌓아 두지 않으므로 여기서 바로 닫는다.
        if (tag.SelfClosing)
        {
            output.Append("</").Append(tag.Name.ToLowerInvariant()).Append('>');
        }
    }

    private static bool IsAllowedAttribute(string tag, string name) =>
        GlobalAttributes.Contains(name)
        || (TagAttributes.TryGetValue(tag, out var extra)
            && extra.Contains(name, StringComparer.OrdinalIgnoreCase));

    /// <summary>속성 값을 본다. 받아 줄 수 없으면 <c>null</c> — 그 속성만 빠진다.</summary>
    private static string? CleanValue(string name, string value)
    {
        var trimmed = value.Trim();

        if (NumericAttributes.Contains(name))
        {
            return trimmed.Length > 0 && trimmed.All(char.IsAsciiDigit) ? trimmed : null;
        }

        if (name.Equals("style", StringComparison.OrdinalIgnoreCase))
        {
            return StyleBanned.Any(b => trimmed.Contains(b, StringComparison.OrdinalIgnoreCase))
                ? null
                : trimmed;
        }

        if (name.Equals("target", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed.Equals("_blank", StringComparison.OrdinalIgnoreCase) ? "_blank" : null;
        }

        if (name.Equals("href", StringComparison.OrdinalIgnoreCase))
        {
            return HasScheme(trimmed, LinkSchemes) ? Relay(trimmed) : null;
        }

        if (name.Equals("src", StringComparison.OrdinalIgnoreCase))
        {
            return HasScheme(trimmed, ImageSchemes) ? Relay(trimmed) : null;
        }

        return trimmed;
    }

    /// <summary>본문에 박힌 /api/file/… 주소를 셸의 중계 경로로 옮긴다. Vue 시절 편집기가 박은 그림 주소는 포털 오리진에 /api 가 없어 옛 공지의 그림이 전부 깨진다. 저장된 값은 고치지 않고 보여 줄 때만 옮긴다 — DB 를 건드리면 되돌릴 수 없다. 규칙은 FileDownload.RelayUrl 한 곳에 있다.</summary>
    private static string Relay(string url) => FileDownload.RelayUrl(url) ?? url;

    /// <summary>주소가 아는 시작(http · 상대 경로 등)으로 열리는지 본다. 공백과 제어문자를 먼저 걷는다 — java 와 script: 사이에 탭을 끼워도 브라우저는 같은 것으로 읽는다.</summary>
    private static bool HasScheme(string value, string[] schemes)
    {
        var packed = new string(value.Where(ch => !char.IsWhiteSpace(ch) && !char.IsControl(ch)).ToArray());

        return schemes.Any(s => packed.StartsWith(s, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>이름이 맞는 자리까지 닫으면서 되돌아간다.</summary>
    private static void CloseUpTo(StringBuilder output, List<string> open, string name)
    {
        var at = open.FindLastIndex(n => n.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (at < 0)
        {
            // 연 적이 없는 것을 닫으려 한다. 내보내면 우리 마크업이 닫힌다.
            return;
        }

        for (var k = open.Count - 1; k >= at; k--)
        {
            output.Append("</").Append(open[k].ToLowerInvariant()).Append('>');
        }

        open.RemoveRange(at, open.Count - at);
    }

    /// <summary>여는 태그 뒤부터 짝이 되는 닫는 태그 뒤까지 건너뛴다.</summary>
    private static int SkipToClose(string html, int from, string name)
    {
        var close = $"</{name}";
        var at = html.IndexOf(close, from, StringComparison.OrdinalIgnoreCase);

        if (at < 0)
        {
            return html.Length;
        }

        var end = html.IndexOf('>', at);
        return end < 0 ? html.Length : end + 1;
    }

    private sealed record Tag(string Name, string Attributes, bool IsClosing, bool SelfClosing, int End);

    /// <summary>&lt; 자리에서 태그 하나를 읽는다. 정규식으로 &gt; 를 찾지 않는다 — 속성 값 안의 부등호를 태그 끝으로 읽으면 뒤가 통째로 어긋난다.</summary>
    private static bool TryReadTag(string html, int start, out Tag tag)
    {
        tag = default!;

        var i = start + 1;
        var closing = false;

        if (i < html.Length && html[i] == '/')
        {
            closing = true;
            i++;
        }

        var nameStart = i;

        while (i < html.Length && (char.IsAsciiLetterOrDigit(html[i]) || html[i] == '-'))
        {
            i++;
        }

        if (i == nameStart || !char.IsAsciiLetter(html[nameStart]))
        {
            return false;
        }

        var name = html[nameStart..i];
        var attrStart = i;
        var quote = '\0';

        while (i < html.Length)
        {
            var ch = html[i];

            if (quote != '\0')
            {
                if (ch == quote)
                {
                    quote = '\0';
                }
            }
            else if (ch is '"' or '\'')
            {
                quote = ch;
            }
            else if (ch == '>')
            {
                break;
            }

            i++;
        }

        if (i >= html.Length)
        {
            return false;
        }

        var attrs = html[attrStart..i].Trim();
        var self = attrs.EndsWith('/');

        tag = new Tag(name, self ? attrs[..^1] : attrs, closing, self, i + 1);
        return true;
    }

    /// <summary>여는 태그의 속성 목록을 이름·값 짝으로 훑는다.</summary>
    private static IEnumerable<(string Name, string Value)> ReadAttributes(string attributes)
    {
        var i = 0;

        while (i < attributes.Length)
        {
            while (i < attributes.Length && char.IsWhiteSpace(attributes[i]))
            {
                i++;
            }

            var nameStart = i;

            while (i < attributes.Length
                   && !char.IsWhiteSpace(attributes[i])
                   && attributes[i] is not ('=' or '/' or '>'))
            {
                i++;
            }

            if (i == nameStart)
            {
                i++;
                continue;
            }

            var name = attributes[nameStart..i];

            while (i < attributes.Length && char.IsWhiteSpace(attributes[i]))
            {
                i++;
            }

            if (i >= attributes.Length || attributes[i] != '=')
            {
                // 값이 없는 속성. 우리가 받아 주는 것 중에는 없으므로 빈 값으로 둔다.
                yield return (name, string.Empty);
                continue;
            }

            i++;

            while (i < attributes.Length && char.IsWhiteSpace(attributes[i]))
            {
                i++;
            }

            if (i >= attributes.Length)
            {
                yield return (name, string.Empty);
                yield break;
            }

            string value;

            if (attributes[i] is '"' or '\'')
            {
                var quote = attributes[i];
                var from = ++i;

                while (i < attributes.Length && attributes[i] != quote)
                {
                    i++;
                }

                value = attributes[from..Math.Min(i, attributes.Length)];
                i++;
            }
            else
            {
                var from = i;

                while (i < attributes.Length && !char.IsWhiteSpace(attributes[i]))
                {
                    i++;
                }

                value = attributes[from..i];
            }

            yield return (name, value);
        }
    }

    /// <summary>속성 값을 따옴표 안에 넣을 수 있게 만든다. &amp; 는 건드리지 않는다 — 원본이 이미 &amp;amp; 로 들어와서 다시 바꾸면 글자로 보인다. 따옴표만 막으면 값이 속성 밖으로 나가지 못한다.</summary>
    private static string EscapeAttribute(string value) =>
        value.Replace("\"", "&quot;", StringComparison.Ordinal)
             .Replace("<", "&lt;", StringComparison.Ordinal)
             .Replace(">", "&gt;", StringComparison.Ordinal);
}
