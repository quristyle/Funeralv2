using System.Text.Json;
using System.Text.Json.Serialization;

namespace JSini.Web.ProjMng.Api;

/// <summary>
/// 테이블 계통도의 저장 자리와 캔버스가 올려 주는 모양.
/// 도면은 DB 접속(= DB 하나 · 스키마 하나) 속성 두 줄에 담긴다 — 도면과 업무항목. 둘을 가르는 까닭은 옛 도구와 같다:
/// 도면을 처음 모습으로 되돌려도 사람이 모아 둔 업무항목은 남아야 한다.
/// </summary>
public static class LineageGraph
{
    /// <summary>도면 한 장. 내용은 옛 WBS 도구(#/tables2)의 Vue Flow JSON 그대로다.</summary>
    public const string GraphKey = "table_lineage";

    /// <summary>업무항목 <c>{ v, items:[{id,name,tables[]}] }</c>.</summary>
    public const string ItemsKey = "table_lineage_items";

    /// <summary>처음 모습. 옮겨 넣을 때 한 벌 떠 둔다 — 「기본 배치로」가 이것으로 되돌린다.</summary>
    public const string SeedKey = "table_lineage_seed";

    /// <summary>속성 종류. ERD 고르개는 <c>entities</c> 를 보고 거르므로 이 줄들이 그쪽 「그림」 목록에 섞이지 않는다.</summary>
    public const string PropType = "lineage";

    public const string EmptyGraph = """{"v":1,"lanes":[],"flows":[],"nodes":[],"edges":[]}""";

    public const string EmptyItems = """{"v":1,"items":[]}""";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    /// <summary>선 종류. 캔버스(table-lineage.js 의 EDGE_KINDS)와 같은 차례·같은 글자여야 한다 — 어긋나면 켜도 안 나오는 선이 생긴다.</summary>
    public static readonly IReadOnlyList<LineageKind> EdgeKinds =
    [
        new("proc", "처리", "#5C6C7A"),
        new("trigger", "트리거", "#B33636"),
        new("ext", "외부", "#6A4FBF"),
        new("batch", "배치", "#2E7D4F"),
        new("read", "읽기", "#9AA3AB"),
    ];

    /// <summary>칸 종류. 캔버스의 NODE_KINDS 와 같다.</summary>
    public static readonly IReadOnlyList<LineageKind> NodeKinds =
    [
        new("master", "기준", ""),
        new("ledger", "원장", ""),
        new("derived", "파생", ""),
        new("snap", "스냅샷", ""),
        new("ext", "외부", ""),
        new("batch", "배치", ""),
        new("proc", "프로시저", ""),
        new("iface", "인터페이스", ""),
        new("eai", "EAI", ""),
    ];

    /// <summary>흐름 순번의 갈래 빛깔 이름. 캔버스의 SEQ_COLORS 차례와 같다.</summary>
    public static readonly IReadOnlyList<string> SeqNames = ["파랑", "초록", "주황", "보라", "청록", "빨강", "회색", "자홍"];

    public static readonly IReadOnlyList<LineageKind> Sides =
    [
        new("", "자동", ""),
        new("t", "위", ""),
        new("r", "오른쪽", ""),
        new("b", "아래", ""),
        new("l", "왼쪽", ""),
    ];

    public static LineageItems ParseItems(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new LineageItems();

        try
        {
            return JsonSerializer.Deserialize<LineageItems>(raw, Json) ?? new LineageItems();
        }
        catch (JsonException)
        {
            return new LineageItems();
        }
    }
}

public sealed record LineageKind(string Value, string Text, string Color);

/// <summary>캔버스가 처음 읽고 돌려주는 요약 — 거르개를 세우는 데 쓴다.</summary>
public sealed class LineageSummary
{
    public int Nodes { get; set; }
    public int Edges { get; set; }
    public Dictionary<string, int> EdgeCounts { get; set; } = [];
    public List<LineageLane> Lanes { get; set; } = [];
    public List<LineageFlow> Flows { get; set; } = [];
    public List<LineageTable> Tables { get; set; } = [];
}

public sealed class LineageTable
{
    public string Id { get; set; } = string.Empty;
    public string? Kr { get; set; }
    public string? Kind { get; set; }
}

public sealed class LineageLane
{
    public int I { get; set; }
    public string? Name { get; set; }
}

public sealed class LineageFlow
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public int Count { get; set; }
}

/// <summary>고른 칸 하나.</summary>
public sealed class LineageNode
{
    public string Id { get; set; } = string.Empty;
    public string? Kr { get; set; }
    public string? Kind { get; set; }
    public string? Pk { get; set; }
    public double? Rows { get; set; }
    public string? Owner { get; set; }
    public string? Note { get; set; }
    public string? Warn { get; set; }
    public string? Trg { get; set; }
    public int? Star { get; set; }
    public List<string>? Writers { get; set; }
    public List<LineageLink> Links { get; set; } = [];
}

public sealed class LineageLink
{
    public string Edge { get; set; } = string.Empty;
    public string Other { get; set; } = string.Empty;
    public bool Out { get; set; }
    public string? Label { get; set; }
    public string? Ty { get; set; }
}

/// <summary>고른 선 하나.</summary>
public sealed class LineageEdge
{
    public string Id { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string? Label { get; set; }
    public string Ty { get; set; } = "proc";
    public bool Key { get; set; }
    public int Seq { get; set; }
    public int Flow { get; set; }
    public string SourceHandle { get; set; } = string.Empty;
    public string TargetHandle { get; set; } = string.Empty;
    public int Pts { get; set; }
}

public sealed class LineageItems
{
    public int V { get; set; } = 1;
    public List<LineageItem> Items { get; set; } = [];

    public string ToJson() => JsonSerializer.Serialize(this, LineageGraph.Json);
}

public sealed class LineageItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<string> Tables { get; set; } = [];
}

/// <summary>대상 DB 에서 받은 표 한 줄. 놓기 창의 고르는 표가 쓴다.</summary>
public sealed class LineageDbTable
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>이미 도면에 있다. 고를 수 없게 흐리게 둔다.</summary>
    public bool Placed { get; set; }
}
