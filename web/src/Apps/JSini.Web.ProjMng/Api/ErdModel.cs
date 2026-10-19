using System.Text.Json;
using System.Text.Json.Serialization;

namespace JSini.Web.ProjMng.Api;

/// <summary>ERD·플로우 다이어그램 저장 형식. DB 의 JSON 과 맞으므로 필드 이름을 바꾸면 기존 그림을 읽지 못한다.</summary>
public sealed record ErdModel
{
    [JsonPropertyName("entities")]
    public List<ErdEntity> Entities { get; init; } = [];

    [JsonPropertyName("relations")]
    public List<ErdRelation> Relations { get; init; } = [];

    /// <summary>보던 자리와 화면 설정. 옛 저장본에는 없으므로(null) 그때는 화면을 건드리지 않는다.</summary>
    [JsonPropertyName("view")]
    public ErdViewport? View { get; init; }

    /// <summary>비어 있는 모델. 저장본이 없는 새 다이어그램이 여기서 시작한다.</summary>
    public static ErdModel Empty => new();

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>깨진 저장본은 빈 모델로 읽는다 — 저장본 하나 때문에 화면이 죽지 않게 한다.</summary>
    public static ErdModel Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Empty;
        }

        try
        {
            return JsonSerializer.Deserialize<ErdModel>(raw, JsonOptions) ?? Empty;
        }
        catch (JsonException)
        {
            return Empty;
        }
    }

    /// <summary>옛 draw.io XML 그림인가. Parse 는 이것을 빈 모델로 돌리므로 저장 시 덮어쓰기를 막는 데 쓴다.</summary>
    public static bool IsLegacyDrawing(string? raw) =>
        raw is not null && raw.TrimStart().StartsWith('<');

    /// <summary>우리 JSON 그림이거나 옛 XML 인가. DB 속성에는 SQL 도 섞여 있어 여는 괄호만으로 가르지 않는다.</summary>
    public static bool IsDiagram(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var head = raw.TrimStart();

        return IsLegacyDrawing(head)
               || (head.StartsWith('{') && head.Contains("\"entities\"", StringComparison.Ordinal));
    }

    /// <summary>저장용 JSON. Vue 와 같이 들여쓰기를 넣는다 — DB 속성 화면에서 사람이 읽는다.</summary>
    public string ToJson() =>
        JsonSerializer.Serialize(this, new JsonSerializerOptions(JsonOptions) { WriteIndented = true });
}

/// <summary>그림을 열 때 되돌릴 화면 상태(배율·이동·미니맵·도구상자). 내용이 아니라 보는 방식이라 바뀌어도 「저장하지 않은 변경」이 되지 않는다.</summary>
public sealed record ErdViewport
{
    /// <summary>배율. <c>1</c> 이 100%.</summary>
    [JsonPropertyName("scale")]
    public double Scale { get; init; } = 1;

    /// <summary>가로 이동값(그래프 좌표).</summary>
    [JsonPropertyName("dx")]
    public double Dx { get; init; }

    /// <summary>세로 이동값(그래프 좌표).</summary>
    [JsonPropertyName("dy")]
    public double Dy { get; init; }

    /// <summary>미니맵 표시. 기본은 켬.</summary>
    [JsonPropertyName("minimap")]
    public bool Minimap { get; init; } = true;

    /// <summary>바탕 none·grid·dots(기본 none). 자석 설정도 함께 되돌린다.</summary>
    [JsonPropertyName("background")]
    public string Background { get; init; } = "none";

    /// <summary>도구상자 펼침. 기본은 펼침.</summary>
    [JsonPropertyName("tools")]
    public bool Tools { get; init; } = true;

    /// <summary>도구상자 고정 핀. 그림 옆에 자리를 차지한다.</summary>
    [JsonPropertyName("pinned")]
    public bool Pinned { get; init; } = true;
}

public sealed record ErdEntity
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("desc")]
    public string? Desc { get; init; }

    /// <summary>상자에 늘어놓을 칸 목록. 대상 DB 에서 읽어 채우며 저장본에는 남기지 않는다(남기면 옛 칸이 그려진다).</summary>
    [JsonPropertyName("fields")]
    public List<string>? Fields { get; init; }

    /// <summary>좌표·크기. 없으면(새 항목) DiagramViewer 가 격자로 흩뿌린다.</summary>
    [JsonPropertyName("x")]
    public int? X { get; init; }

    [JsonPropertyName("y")]
    public int? Y { get; init; }

    [JsonPropertyName("w")]
    public int? W { get; init; }

    [JsonPropertyName("h")]
    public int? H { get; init; }

    /// <summary>DB 에서 사라진 표인가. 조회 결과라 저장본에 남기지 않고, 화면이 저장 직전에 끈다.</summary>
    [JsonPropertyName("gone")]
    public bool Gone { get; init; }

    /// <summary>캔버스에서 손으로 만든 도형인가. 이름 편집 가능 여부와 「사라진 표」 판정 제외에 쓰이며 저장본에 남는다.</summary>
    [JsonPropertyName("manual")]
    public bool Manual { get; init; }

    /// <summary>손그림 도형 종류(rhombus·fcDoc·bpmn.task …). 정본은 diagram-viewer.js 의 SHAPES. 이 값이 빠지면 불러올 때 모든 도형이 네모로 그려진다.</summary>
    [JsonPropertyName("kind")]
    public string? Kind { get; init; }

    /// <summary>붙여넣은 그림(data URL). 파일 서버를 거치지 않고 저장본에 담는다. 넣기 전에 긴 변 1100px·420KB 로 줄인다 — 원본은 회로 수신 한도를 넘겨 연결을 끊는다.</summary>
    [JsonPropertyName("image")]
    public string? Image { get; init; }
}

/// <summary>도형 사이의 관계선. <c>From</c>·<c>To</c> 는 엔터티 id 다.</summary>
public sealed record ErdRelation
{
    /// <summary>외래키에서 자동으로 그은 선. 저장하지 않고 불러올 때마다 다시 그린다(저장하면 제약을 뗀 뒤에도 선이 남는다).</summary>
    [JsonPropertyName("auto")]
    public bool Auto { get; init; }

    [JsonPropertyName("from")]
    public string From { get; init; } = string.Empty;

    [JsonPropertyName("to")]
    public string To { get; init; } = string.Empty;

    [JsonPropertyName("label")]
    public string? Label { get; init; }
}
