using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JSini.Web.ProjMng.Api;

/// <summary>마인드맵 한 장. 뿌리 하나에 가지가 달린 나무다 — 저장은 나무만 하고 자리는 그릴 때마다 JS 가 정한다.
/// 글 형식과 모양 이름은 mermaid 의 mindmap 을 따르고, 그림 엔진은 mermaid 가 아니라 maxgraph 다.</summary>
public sealed record MindMapModel
{
    /// <summary>뿌리. mermaid 와 같이 언제나 하나다 — 뿌리가 여럿인 글은 읽을 때 첫 것에 붙인다.</summary>
    [JsonPropertyName("root")]
    public MindMapNode Root { get; init; } = new();

    /// <summary>새 마인드맵. 뿌리 하나로 시작한다.</summary>
    public static MindMapModel Empty => new()
    {
        Root = new MindMapNode { Id = "n1", Text = "중심 주제", Shape = MindMapShape.Circle },
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>저장본(JSON)을 읽는다. 깨졌으면 빈 마인드맵을 준다 — 한 장이 상했다고 화면이 죽으면 고칠 길도 없어진다.</summary>
    public static MindMapModel Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Empty;
        }

        // 저장본이 JSON 이 아니면 mermaid 글로 본다 — DB 속성 화면에 사람이 적은 값은 십중팔구 mermaid 다.
        if (!raw.TrimStart().StartsWith('{'))
        {
            return FromMermaid(raw);
        }

        try
        {
            var model = JsonSerializer.Deserialize<MindMapModel>(raw, JsonOptions);

            return model?.Root is null ? Empty : model;
        }
        catch (JsonException)
        {
            return Empty;
        }
    }

    /// <summary>저장용 JSON. 사람이 DB 속성 화면에서 읽으므로 들여쓴다.</summary>
    public string ToJson() =>
        JsonSerializer.Serialize(this, new JsonSerializerOptions(JsonOptions) { WriteIndented = true });

    /// <summary>마디 수. 화면이 「비었다」를 가리는 데 쓴다. 저장본에는 담지 않는다 — 나무에서 세면 나오는 값이라 옛 값이 남는다.</summary>
    [JsonIgnore]
    public int Count => Root.Count;

    /* ── mermaid mindmap 글 ──────────────────────────────────────── */
    /* 층은 들여쓰기가, 모양은 감싸는 기호가 정한다. */

    /// <summary>mermaid 글을 나무로 읽는다. 틀린 줄이 있어도 던지지 않는다 — 반쯤 적다 만 글이 정상이고 못 읽은 줄은 빠진다.</summary>
    public static MindMapModel FromMermaid(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Empty;
        }

        MindMapNode? root = null;
        MindMapNode? last = null;

        // 자식은 자기보다 덜 들여쓴 가장 가까운 마디에 붙는다 — mermaid 의 층 규칙 그대로다.
        var stack = new List<(int Indent, MindMapNode Node)>();
        var seq = 0;
        var usedIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var rawLine in text.ReplaceLineEndings("\n").Split('\n'))
        {
            var line = rawLine.TrimEnd();

            // 빈 줄과 주석(`%%`)은 층에 영향을 주지 않는다.
            if (line.Length == 0 || line.TrimStart().StartsWith("%%", StringComparison.Ordinal))
            {
                continue;
            }

            var indent = Indent(line);
            var body = line.Trim();

            // 머리말. `mindmap` 한 줄뿐이고 뿌리보다 덜 들여쓰여 있다.
            if (root is null && body.Equals("mindmap", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // `::icon(fa fa-book)` — 바로 앞 마디를 꾸민다. 새 마디가 아니다.
            if (body.StartsWith("::icon(", StringComparison.Ordinal) && body.EndsWith(')'))
            {
                if (last is not null)
                {
                    last.Icon = body[7..^1].Trim();
                }

                continue;
            }

            // `:::강조 큼` — 마찬가지로 앞 마디를 꾸민다.
            if (body.StartsWith(":::", StringComparison.Ordinal))
            {
                if (last is not null)
                {
                    last.ClassName = body[3..].Trim();
                }

                continue;
            }

            var node = ParseNode(body);

            // 이름이 없거나 이미 쓰인 이름이면 새로 짓는다 — 이름은 고른 마디를 가리키는 열쇠라 유일하기만 하면 된다.
            if (node.Id.Length == 0 || !usedIds.Add(node.Id))
            {
                do
                {
                    seq++;
                    node.Id = $"n{seq}";
                }
                while (!usedIds.Add(node.Id));
            }

            // 뿌리는 남긴다. 뿌리가 둘인 글은 뒤엣것을 뿌리의 자식으로 받는다.
            while (stack.Count > 1 && stack[^1].Indent >= indent)
            {
                stack.RemoveAt(stack.Count - 1);
            }

            if (stack.Count == 0)
            {
                root = node;
            }
            else
            {
                stack[^1].Node.Children.Add(node);
            }

            stack.Add((indent, node));
            last = node;
        }

        return root is null ? Empty : new MindMapModel { Root = root };
    }

    /// <summary>나무를 mermaid 글로 적는다. <see cref="FromMermaid"/> 로 다시 읽으면 같은 나무가 나온다.</summary>
    public string ToMermaid()
    {
        var sb = new StringBuilder();
        sb.Append("mindmap\n");

        Write(sb, Root, 1);

        return sb.ToString();
    }

    private static void Write(StringBuilder sb, MindMapNode node, int depth)
    {
        sb.Append(' ', depth * 2).Append(node.Line()).Append('\n');

        if (!string.IsNullOrWhiteSpace(node.Icon))
        {
            sb.Append(' ', depth * 2).Append("::icon(").Append(node.Icon).Append(")\n");
        }

        if (!string.IsNullOrWhiteSpace(node.ClassName))
        {
            sb.Append(' ', depth * 2).Append(":::").Append(node.ClassName).Append('\n');
        }

        foreach (var child in node.Children)
        {
            Write(sb, child, depth + 1);
        }
    }

    /// <summary>들여쓴 칸 수. 탭은 네 칸으로 센다 — 상대 순서만 맞으면 어떤 칸 수로 세도 층은 같다.</summary>
    private static int Indent(string line)
    {
        var n = 0;

        foreach (var c in line)
        {
            if (c == ' ') n++;
            else if (c == '\t') n += 4;
            else break;
        }

        return n;
    }

    /// <summary>마디 한 줄을 읽는다. 닫는 기호부터 본다 — 여는 기호로 가르면 <c>((</c> 와 <c>(</c> 가 겹쳐 원과 둥근상자를 가릴 수 없다.</summary>
    private static MindMapNode ParseNode(string body)
    {
        foreach (var (open, close, shape) in Delimiters)
        {
            if (!body.EndsWith(close, StringComparison.Ordinal))
            {
                continue;
            }

            var start = body.IndexOf(open, StringComparison.Ordinal);

            // 여는 기호가 닫는 기호와 같은 자리를 물면 감싼 것이 아니다.
            if (start < 0 || start + open.Length > body.Length - close.Length)
            {
                continue;
            }

            return new MindMapNode
            {
                Id = body[..start].Trim(),
                Text = Unquote(body[(start + open.Length)..^close.Length]),
                Shape = shape,
            };
        }

        // 감싼 기호가 없으면 글자가 그대로 이름이자 내용이다(mermaid 와 같다).
        return new MindMapNode { Id = string.Empty, Text = Unquote(body), Shape = MindMapShape.Default };
    }

    /// <summary>감싸는 기호와 그 모양. 순서가 규칙이다 — 위에서부터 맞는 것을 쓴다.</summary>
    private static readonly (string Open, string Close, string Shape)[] Delimiters =
    [
        ("((", "))", MindMapShape.Circle),
        ("))", "((", MindMapShape.Bang),
        ("{{", "}}", MindMapShape.Hexagon),
        ("[", "]", MindMapShape.Square),
        (")", "(", MindMapShape.Cloud),
        ("(", ")", MindMapShape.Rounded),
    ];

    /// <summary>따옴표·역따옴표를 벗기고 &lt;br/&gt; 을 줄바꿈으로 되돌린다.</summary>
    private static string Unquote(string text)
    {
        var value = text.Trim();

        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            value = value[1..^1].Trim();
        }

        if (value.Length >= 2 && value[0] == '`' && value[^1] == '`')
        {
            value = value[1..^1].Trim();
        }

        return value
            .Replace("<br/>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>글자를 mermaid 에 넣을 수 있게 만든다. 감싸는 기호가 글자 안에 있으면 따옴표로 묶는다 — 묶지 않으면 「매출(순)」 같은 제목을 엉뚱한 데서 자른다.</summary>
    internal static string Escape(string? text)
    {
        var value = (text ?? string.Empty)
            .ReplaceLineEndings("\n")
            .Replace("\n", "<br/>", StringComparison.Ordinal)
            .Replace("\"", "'", StringComparison.Ordinal);

        return value.AsSpan().IndexOfAny("()[]{}") >= 0 ? $"\"{value}\"" : value;
    }
}

/// <summary>마디 하나. 자식을 직접 들고 있다 — 선은 언제나 부모-자식이라 관계를 따로 적을 것이 없다.
/// 나무를 손보는 일이 본업이라 record 지만 속성은 열어 둔다.</summary>
public sealed record MindMapNode
{
    /// <summary>마디를 가리키는 이름. 그림에서만 쓰며, 고른 마디를 JS 와 주고받는 열쇠다. 글자를 고쳐도 바뀌지 않는다.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "n1";

    /// <summary>보이는 글자. 줄바꿈이 들어 있으면 그대로 여러 줄로 그린다.</summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = "중심 주제";

    /// <summary>모양. <see cref="MindMapShape"/> 의 값이고 mermaid 와 같은 이름이다.</summary>
    [JsonPropertyName("shape")]
    public string Shape { get; set; } = MindMapShape.Default;

    /// <summary>mermaid 의 <c>::icon(...)</c>. 그리지는 않지만, 바깥에서 가져온 글을 고쳐 돌려줄 때 잃지 않도록 값만 들고 있다.</summary>
    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    /// <summary>mermaid 의 <c>:::클래스</c>. 아이콘과 같은 이유로 값만 들고 있다.</summary>
    [JsonPropertyName("class")]
    public string? ClassName { get; set; }

    /// <summary>가지를 접어 둔 상태. 사람의 판단이라 저장한다 — 다만 mermaid 글에는 담기지 않는다(그 문법에 접힘이 없다).</summary>
    [JsonPropertyName("collapsed")]
    public bool Collapsed { get; set; }

    [JsonPropertyName("children")]
    public List<MindMapNode> Children { get; set; } = [];

    /// <summary>자기를 포함한 마디 수.</summary>
    [JsonIgnore]
    public int Count => 1 + Children.Sum(c => c.Count);

    /// <summary>이 마디를 mermaid 한 줄로 적는다.</summary>
    internal string Line()
    {
        var text = MindMapModel.Escape(Text);

        return Shape switch
        {
            MindMapShape.Square => $"[{text}]",
            MindMapShape.Rounded => $"({text})",
            MindMapShape.Circle => $"(({text}))",
            MindMapShape.Bang => $")){text}((",
            MindMapShape.Cloud => $"){text}(",
            MindMapShape.Hexagon => $"{{{{{text}}}}}",

            // 기본 모양은 감싸는 기호가 없어 따옴표만으로는 기호 섞인 글자를 못 막는다 — 네모로 적어 모양을 잃지 않는다.
            _ => text.StartsWith('"') ? $"[{text}]" : text,
        };
    }
}

/// <summary>마디 모양. mermaid 의 여섯 가지 그대로다 — 이름을 바꾸면 글을 주고받을 수 없다.</summary>
public static class MindMapShape
{
    /// <summary>감싸는 기호 없이 글자만. mermaid 의 기본.</summary>
    public const string Default = "default";

    public const string Square = "square";
    public const string Rounded = "rounded";
    public const string Circle = "circle";

    /// <summary>mermaid 의 <c>bang</c> — 터지는 모양. 「중요」를 뜻하는 자리다.</summary>
    public const string Bang = "bang";

    public const string Cloud = "cloud";
    public const string Hexagon = "hexagon";
}
