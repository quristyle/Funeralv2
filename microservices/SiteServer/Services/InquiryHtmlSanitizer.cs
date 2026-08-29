using HtmlAgilityPack;

namespace SiteServer.Services;

/// <summary>
/// 문의 본문 HTML 정리.
///
/// 공개 폼의 에디터가 만든 HTML 을 그대로 믿지 않는다 — 허용 목록에 있는
/// 태그·속성만 남기고 전부 벗긴다. 스크립트·스타일·iframe 같은 것은 태그째
/// 사라지고 글자만 남는다. 관리 화면(v-html)과 메일 본문 양쪽에 들어가므로
/// **저장하기 전에 한 번** 여기서 거른다.
/// </summary>
public static class InquiryHtmlSanitizer
{
    // 에디터(굵게·기울임·밑줄·취소선·글자색·목록·인용)가 만드는 것만 허용한다.
    private static readonly HashSet<string> AllowedTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "p", "div", "br", "b", "strong", "i", "em", "u", "s", "strike",
        "ul", "ol", "li", "blockquote", "a", "span", "font",
    };

    /// <summary>허용 태그별로 남길 속성. 없는 태그는 속성을 전부 벗긴다.</summary>
    private static readonly Dictionary<string, string[]> AllowedAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["a"] = ["href"],
        ["font"] = ["color", "size"],
        ["span"] = ["style"],
    };

    public static string Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // 주석 제거
        foreach (var comment in doc.DocumentNode
                     .Descendants()
                     .Where(n => n.NodeType == HtmlNodeType.Comment)
                     .ToList())
        {
            comment.Remove();
        }

        CleanNode(doc.DocumentNode);
        return doc.DocumentNode.InnerHtml.Trim();
    }

    /// <summary>본문 미리보기·메일 제목용 평문. 태그를 걷어낸다.</summary>
    public static string ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return HtmlEntity.DeEntitize(doc.DocumentNode.InnerText).Trim();
    }

    private static void CleanNode(HtmlNode parent)
    {
        foreach (var node in parent.ChildNodes.ToList())
        {
            if (node.NodeType == HtmlNodeType.Text) continue;

            if (node.NodeType != HtmlNodeType.Element || !AllowedTags.Contains(node.Name))
            {
                // 허용되지 않은 태그는 태그만 벗기고 글자는 살린다.
                // 단, 내용 자체가 위험한 것(script·style)은 통째로 버린다.
                if (node.Name is "script" or "style" or "iframe" or "object" or "embed")
                {
                    node.Remove();
                    continue;
                }

                var text = HtmlNode.CreateNode(HtmlDocument.HtmlEncode(node.InnerText));
                parent.ReplaceChild(text, node);
                continue;
            }

            // 속성 정리
            var keep = AllowedAttributes.TryGetValue(node.Name, out var attrs) ? attrs : [];
            foreach (var attr in node.Attributes.ToList())
            {
                if (!keep.Contains(attr.Name, StringComparer.OrdinalIgnoreCase))
                {
                    attr.Remove();
                    continue;
                }

                // font size 는 execCommand fontSize 의 1~7 단계만 (그 밖의 값은 버린다)
                if (node.Name.Equals("font", StringComparison.OrdinalIgnoreCase) &&
                    attr.Name.Equals("size", StringComparison.OrdinalIgnoreCase) &&
                    !System.Text.RegularExpressions.Regex.IsMatch(attr.Value.Trim(), "^[1-7]$"))
                {
                    attr.Remove();
                    continue;
                }

                // 링크는 http/https/mailto 만. javascript: 류를 막는다.
                if (node.Name.Equals("a", StringComparison.OrdinalIgnoreCase))
                {
                    var href = attr.Value.Trim();
                    if (!href.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                        !href.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                        !href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                    {
                        attr.Remove();
                    }
                }

                // span 의 style 은 color 만 남긴다. expression·url 이 못 들어오게
                // 통째로 재작성한다.
                if (node.Name.Equals("span", StringComparison.OrdinalIgnoreCase) &&
                    attr.Name.Equals("style", StringComparison.OrdinalIgnoreCase))
                {
                    var color = attr.Value
                        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(s => s.Split(':', 2))
                        .Where(kv => kv.Length == 2 && kv[0].Trim().Equals("color", StringComparison.OrdinalIgnoreCase))
                        .Select(kv => kv[1].Trim())
                        .FirstOrDefault(v => System.Text.RegularExpressions.Regex.IsMatch(
                            v, @"^(#[0-9a-fA-F]{3,8}|rgb\([\d\s,]+\)|[a-zA-Z]+)$"));

                    if (color is null) attr.Remove();
                    else attr.Value = $"color: {color}";
                }
            }

            CleanNode(node);
        }
    }
}
