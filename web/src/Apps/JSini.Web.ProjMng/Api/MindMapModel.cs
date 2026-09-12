using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JSini.Web.ProjMng.Api;

/// <summary>
/// 마인드맵 한 장. <b>뿌리 하나에 가지가 달린 나무</b>다 — ERD·유즈케이스처럼
/// 도형과 선을 따로 두지 않는다.
///
/// <para>
/// 저장 자리는 <c>projmng.dev_proj_prop</c> 이고 갈래는 <c>MIND_MAP</c> 이다
/// (<see cref="ProjectPropClient"/>). 프로젝트 하나에 <b>이름별로 한 장</b>이라
/// 유즈케이스와 같은 구도다.
/// </para>
///
/// <para>[왜 좌표를 담지 않나]</para>
///
/// <para>
/// 마인드맵은 <b>배치가 나무 모양에서 저절로 나온다.</b> 사람이 상자를 끌어
/// 놓을 일이 없고, 끌어 놓아 봐야 가지를 하나 더하는 순간 다시 계산된다.
/// 그래서 저장하는 것은 나무뿐이고 자리는 그릴 때마다 JS 가 정한다
/// (<c>wwwroot/js/mind-map.js</c>). ERD 쪽이 <c>x</c>·<c>y</c> 를 담는 것과
/// 정반대인데, 그쪽은 사람이 놓은 자리가 곧 정보이기 때문이다.
/// </para>
///
/// <para>[mermaid 를 본떴다]</para>
///
/// <para>
/// 모양 이름(<see cref="MindMapNode.Shape"/>)과 들여쓰기로 층을 나누는 글
/// 형식은 <c>mermaid</c> 의 <c>mindmap</c> 을 그대로 따른다
/// (<see href="https://github.com/mermaid-js/mermaid"/>). 덕분에 이 화면에서
/// 만든 것을 문서·위키에 <b>그대로 붙여 넣을 수 있고</b>, 반대로 이미 써 둔
/// mermaid 글을 가져올 수도 있다(<see cref="FromMermaid"/>).
/// </para>
///
/// <para>
/// <b>그림 엔진은 mermaid 가 아니라 maxgraph 다.</b> 이 저장소의 그림 화면
/// 셋이 이미 maxgraph 로 그리고 있고 그 파일들은 로컬 정적 자산이다 —
/// 마인드맵 하나 때문에 렌더러를 하나 더 싣지 않는다. mermaid 에서 가져온
/// 것은 <b>글 형식과 모양 이름</b>이다.
/// </para>
/// </summary>
public sealed record MindMapModel
{
    /// <summary>
    /// 뿌리. mermaid 와 같이 <b>언제나 하나</b>다 — 글에 뿌리가 여럿이면
    /// 읽는 쪽에서 첫 것에 붙인다(<see cref="FromMermaid"/>).
    /// </summary>
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

    /// <summary>
    /// 저장본(JSON)을 읽는다. 깨져 있으면 <b>빈 마인드맵</b>을 준다 —
    /// 저장본 하나가 상했다고 화면이 죽으면 고칠 길도 함께 없어진다
    /// (<see cref="ErdModel.Parse"/> 와 같은 방어다).
    /// </summary>
    public static MindMapModel Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Empty;
        }

        // 저장본이 JSON 이 아니면 mermaid 글로 본다. 사람이 DB 속성 화면에서
        // 직접 적어 넣을 수 있는 값이고, 그때 적는 것은 십중팔구 mermaid 다.
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

    /// <summary>
    /// 마디 수. 화면이 「비었다」를 가리는 데 쓴다.
    ///
    /// <para>
    /// <b>저장본에 담지 않는다.</b> 나무에서 세면 나오는 값이라 담아 봐야
    /// 옛 값이 남을 뿐이고, JS 로 오가는 짐도 그만큼 는다. 한동안 빠져 있어
    /// 운영 저장본에 <c>"count": 7</c> 이 같이 적혀 나갔다 — 읽는 쪽이 그 칸을
    /// 모르므로 탈은 안 났지만, <b>세어 보지 않은 수가 자료에 적혀 있는</b>
    /// 상태라 언젠가 그것을 믿는 코드가 생긴다.
    /// </para>
    /// </summary>
    [JsonIgnore]
    public int Count => Root.Count;

    /* ── mermaid mindmap 글 ───────────────────────────────────────

       mermaid 문법은 이렇게 생겼다.

           mindmap
             root((중심 주제))
               가지 하나
                 square[네모]
               가지 둘
               ::icon(fa fa-book)

       층은 **들여쓰기**가 정하고, 모양은 **감싸는 기호**가 정한다.
       ------------------------------------------------------------ */

    /// <summary>
    /// mermaid 글을 나무로 읽는다.
    ///
    /// <para>
    /// <b>틀린 줄이 있어도 던지지 않는다.</b> 이 글은 사람이 손으로 적는
    /// 자리이고, 반쯤 적다 만 상태가 정상이다. 못 읽은 줄은 그냥 빠진다.
    /// </para>
    /// </summary>
    public static MindMapModel FromMermaid(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Empty;
        }

        MindMapNode? root = null;
        MindMapNode? last = null;

        // (들여쓰기 칸 수, 그 자리의 마디). 자식은 자기보다 덜 들여쓴 가장
        // 가까운 마디에 붙는다 — mermaid 가 층을 정하는 방법 그대로다.
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

            // 글에 이름이 없거나 이미 쓰인 이름이면 새로 짓는다. 이름은
            // 화면에서 「고른 마디」를 가리키는 데만 쓰므로 유일하기만 하면 된다.
            if (node.Id.Length == 0 || !usedIds.Add(node.Id))
            {
                do
                {
                    seq++;
                    node.Id = $"n{seq}";
                }
                while (!usedIds.Add(node.Id));
            }

            // 자기보다 깊거나 같은 것은 형제 이하다 — 걷어 낸다. 뿌리는
            // 남긴다(뿌리가 둘인 글은 뒤엣것을 뿌리의 자식으로 받는다).
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

    /// <summary>
    /// 나무를 mermaid 글로 적는다. <see cref="FromMermaid"/> 로 다시 읽으면
    /// 같은 나무가 나온다.
    /// </summary>
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

    /// <summary>
    /// 들여쓴 칸 수. 탭은 네 칸으로 센다 — 탭과 공백을 섞어 적은 글에서
    /// 층이 뒤집히지 않게 하려는 것뿐이고, 몇 칸으로 세든 <b>상대 순서</b>만
    /// 맞으면 결과는 같다.
    /// </summary>
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

    /// <summary>
    /// 마디 한 줄(<c>이름((글자))</c>)을 읽는다.
    ///
    /// <para>
    /// <b>닫는 기호부터 본다.</b> 여는 기호로 가르면 <c>((</c> 와 <c>(</c> 가
    /// 같은 자리에서 겹쳐 원과 둥근상자를 가릴 수 없다. 닫는 쪽은 겹치지
    /// 않는다 — <c>))</c> 로 끝나면 원이고 <c>((</c> 로 끝나면 폭발이다.
    /// </para>
    /// </summary>
    private static MindMapNode ParseNode(string body)
    {
        foreach (var (open, close, shape) in Delimiters)
        {
            if (!body.EndsWith(close, StringComparison.Ordinal))
            {
                continue;
            }

            var start = body.IndexOf(open, StringComparison.Ordinal);

            // 여는 기호가 닫는 기호와 같은 자리를 물면 감싼 것이 아니다
            // (`))` 한 쌍뿐인 줄 따위).
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

    /// <summary>
    /// 감싸는 기호와 그것이 뜻하는 모양. <b>순서가 규칙의 일부다</b> —
    /// 위에서부터 맞는 것을 쓴다(<see cref="ParseNode"/> 머리말).
    /// </summary>
    private static readonly (string Open, string Close, string Shape)[] Delimiters =
    [
        ("((", "))", MindMapShape.Circle),
        ("))", "((", MindMapShape.Bang),
        ("{{", "}}", MindMapShape.Hexagon),
        ("[", "]", MindMapShape.Square),
        (")", "(", MindMapShape.Cloud),
        ("(", ")", MindMapShape.Rounded),
    ];

    /// <summary>
    /// 따옴표·역따옴표를 벗기고 <c>&lt;br/&gt;</c> 을 줄바꿈으로 되돌린다.
    /// mermaid 는 긴 글자를 <c>["`…`"]</c> 로 감싸는 길을 두고 있다.
    /// </summary>
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

    /// <summary>
    /// 글자를 mermaid 에 넣을 수 있게 만든다.
    ///
    /// <para>
    /// 줄바꿈은 <c>&lt;br/&gt;</c> 으로 바꾸고, 감싸는 기호가 글자 안에 있으면
    /// 따옴표로 묶는다. <b>묶지 않으면 읽는 쪽이 엉뚱한 자리에서 자른다</b> —
    /// 「매출(순)」 같은 우리말 제목에서 바로 걸린다.
    /// </para>
    /// </summary>
    internal static string Escape(string? text)
    {
        var value = (text ?? string.Empty)
            .ReplaceLineEndings("\n")
            .Replace("\n", "<br/>", StringComparison.Ordinal)
            .Replace("\"", "'", StringComparison.Ordinal);

        return value.AsSpan().IndexOfAny("()[]{}") >= 0 ? $"\"{value}\"" : value;
    }
}

/// <summary>
/// 마디 하나. <b>자식을 직접 들고 있다</b> — 마인드맵은 선이 언제나
/// 부모-자식이라 관계를 따로 적을 것이 없다.
/// </summary>
/// <remarks>
/// <see cref="ErdEntity"/> 와 달리 <c>record</c> 지만 속성이 고쳐 쓸 수 있게
/// 열려 있다. 나무를 손보는 일(자식 더하기·글자 고치기)이 이 화면의 본업이고,
/// 그때마다 <c>with</c> 로 위쪽을 통째로 다시 만들면 코드가 읽히지 않는다.
/// </remarks>
public sealed record MindMapNode
{
    /// <summary>
    /// 마디를 가리키는 이름. <b>그림에서만 쓴다</b> — 「고른 마디」를 JS 와
    /// 주고받는 열쇠다. 글자를 고쳐도 바뀌지 않는다.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = "n1";

    /// <summary>보이는 글자. 줄바꿈이 들어 있으면 그대로 여러 줄로 그린다.</summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = "중심 주제";

    /// <summary>모양. <see cref="MindMapShape"/> 의 값이고 mermaid 와 같은 이름이다.</summary>
    [JsonPropertyName("shape")]
    public string Shape { get; set; } = MindMapShape.Default;

    /// <summary>
    /// mermaid 의 <c>::icon(...)</c>.
    ///
    /// <para>
    /// <b>여기서는 그리지 않는다.</b> 그 문법이 가리키는 것은 Font Awesome 같은
    /// 바깥 아이콘 묶음인데 포털은 그것을 싣지 않는다(메뉴 아이콘은 iconify 로
    /// 뽑아 둔 CSS 한 벌이다). 그래도 <b>값은 잃지 않고 들고 있다</b> —
    /// 바깥에서 가져온 글을 고쳐 돌려줄 때 이 줄이 사라지면 안 된다.
    /// </para>
    /// </summary>
    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    /// <summary>mermaid 의 <c>:::클래스</c>. 아이콘과 같은 이유로 값만 들고 있다.</summary>
    [JsonPropertyName("class")]
    public string? ClassName { get; set; }

    /// <summary>
    /// 가지를 접어 두었는가.
    ///
    /// <para>
    /// <b>저장한다.</b> 「지금 펼쳐 보고 있는가」가 아니라 「이 가지는 접어 둔
    /// 채로 보는 것이 낫다」는 사람의 판단이기 때문이다 — 마디가 백 개인
    /// 마인드맵을 열 때마다 다시 접게 하지 않는다. 대신 mermaid 글에는
    /// 담기지 않는다(그 문법에 접힘이 없다).
    /// </para>
    /// </summary>
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

            // 기본 모양은 감싸는 기호가 없다. 그래서 글자에 기호가 섞여 있으면
            // 따옴표만으로는 못 막는다 — 네모로 적어 모양을 잃지 않는다.
            _ => text.StartsWith('"') ? $"[{text}]" : text,
        };
    }
}

/// <summary>
/// 마디 모양. <b>mermaid 의 여섯 가지 그대로다</b> — 이름을 바꾸면 글을
/// 주고받을 수 없다.
/// </summary>
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
