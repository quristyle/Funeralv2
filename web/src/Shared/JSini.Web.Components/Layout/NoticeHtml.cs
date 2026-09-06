using System.Text;
using JSini.Web.Components.Data;

namespace JSini.Web.Components.Layout;

/// <summary>
/// 공지 본문(HTML)을 화면에 넣기 전에 거르는 곳.
/// </summary>
/// <remarks>
/// <para>
/// [왜 있는가]
/// </para>
///
/// <para>
/// 공지 본문은 <b>서식이 목적</b>이라 글자로 뭉갤 수 없다(F.A.Q·문의 본문과
/// 다른 점이다 — 그쪽은 태그를 걷어내고 보여 준다). 그래서 <c>MarkupString</c>
/// 으로 그리는데, 그 순간 본문에 든 것이 전부 브라우저 안에서 실행된다.
/// </para>
///
/// <para>
/// "관리자만 쓰니까 괜찮다" 로 두지 않는다 — 관리자 계정 하나가 털리면 그
/// 공지는 <b>로그인 전 화면을 포함해 모든 사용자에게</b> 뜬다. 공지가 전
/// 서비스 공용이라 피해 범위가 이 포털 밖까지 간다.
/// </para>
///
/// <para>
/// [빼는 목록이 아니라 남기는 목록이다]
/// </para>
///
/// <para>
/// <c>&lt;script&gt;</c> 를 지우는 식(deny-list)은 반드시 빠뜨린다 —
/// <c>onerror</c>·<c>javascript:</c>·<c>&lt;svg&gt;</c> 안의 이벤트까지
/// 세어야 하고, 브라우저가 새 길을 하나 더 내면 그때부터 뚫린다.
/// 여기서는 <b>아는 태그와 아는 속성만 통과</b>시키고 나머지는 전부 버린다.
/// 모르는 태그는 껍데기만 버리고 <b>안의 글자는 남긴다</b> — 서식이 조금
/// 빠지는 것이 문장이 통째로 사라지는 것보다 낫다.
/// </para>
///
/// <para>
/// 규칙은 <c>NoticeHtmlTests</c> 가 지킨다.
/// </para>
/// </remarks>
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

    /// <summary>
    /// 껍데기뿐 아니라 <b>안까지 통째로</b> 버리는 태그.
    ///
    /// <para>
    /// 나머지 모르는 태그는 글자를 남기지만 이것들은 남기면 안 된다 —
    /// <c>&lt;style&gt;</c> 안의 CSS 나 <c>&lt;script&gt;</c> 안의 코드가
    /// 화면에 그대로 글자로 쏟아진다.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// <c>style</c> 값에 들어 있으면 그 속성을 통째로 버리는 조각들.
    ///
    /// <para>
    /// 색·정렬만 오는 자리라 <c>url()</c> 이나 <c>@import</c> 가 보이면
    /// 서식이 아니라 바깥으로 나가려는 것이다.
    /// </para>
    /// </summary>
    private static readonly string[] StyleBanned = ["url(", "expression", "javascript:", "@import", "behavior", "/*"];

    /// <summary>공지 본문을 화면에 넣어도 되는 HTML 로 바꾼다.</summary>
    public static string Sanitize(string? html)
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

            AppendOpenTag(output, tag);

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
    private static void AppendOpenTag(StringBuilder output, Tag tag)
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

    /// <summary>
    /// 본문에 박혀 있는 <c>/api/file/…</c> 주소를 셸의 중계 경로로 옮긴다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// [실제로 밟았다 — 옛 공지의 그림이 안 나온다]
    /// </para>
    ///
    /// <para>
    /// Vue 시절 서식 편집기가 그림을 올리면 본문에
    /// <c>&lt;img src="/api/file/download/{guid}"&gt;</c> 가 박혔다. 그때는
    /// 브라우저가 게이트웨이와 같은 오리진이라 열렸지만, 지금 브라우저가 보는
    /// 것은 포털(:5557)이고 <b>거기에는 <c>/api</c> 가 없다.</b> 그래서 옛
    /// 공지를 열면 본문 그림이 전부 깨진 네모로 나온다.
    /// </para>
    ///
    /// <para>
    /// 저장된 값을 고치지 않고 <b>보여 줄 때만</b> 옮긴다. DB 를 건드리면
    /// 되돌릴 수 없고, 옛 값이 그대로 있어야 나중에 무엇이 있었는지 알 수 있다.
    /// </para>
    ///
    /// <para>
    /// 규칙은 <see cref="FileDownload.RelayUrl"/> 한 곳에 있다 — DB 에 저장된
    /// 영정 사진·미디어 주소도 같은 모양이라 같은 것을 쓴다.
    /// </para>
    /// </remarks>
    private static string Relay(string url) => FileDownload.RelayUrl(url) ?? url;

    /// <summary>
    /// 주소가 아는 시작으로 열리는지 본다.
    ///
    /// <para>
    /// 공백과 제어문자를 먼저 걷어낸다 — <c>java</c> 와 <c>script:</c> 사이에
    /// 탭이나 줄바꿈을 끼워 넣어도 브라우저는 같은 것으로 읽는다.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// <c>&lt;</c> 자리에서 태그 하나를 읽는다.
    ///
    /// <para>
    /// 정규식으로 <c>&gt;</c> 를 찾지 않는다 — 속성 값 안의 부등호
    /// (<c>title="a &gt; b"</c>)를 태그 끝으로 읽어 뒤가 통째로 어긋난다.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// 속성 값을 따옴표 안에 넣을 수 있게 만든다.
    ///
    /// <para>
    /// <c>&amp;</c> 는 건드리지 않는다 — 원본이 이미 <c>&amp;amp;</c> 로
    /// 들어오므로 다시 바꾸면 글자로 <c>&amp;amp;</c> 가 보인다. 따옴표만
    /// 막으면 값이 속성 밖으로 나가지 못한다.
    /// </para>
    /// </summary>
    private static string EscapeAttribute(string value) =>
        value.Replace("\"", "&quot;", StringComparison.Ordinal)
             .Replace("<", "&lt;", StringComparison.Ordinal)
             .Replace(">", "&gt;", StringComparison.Ordinal);
}
