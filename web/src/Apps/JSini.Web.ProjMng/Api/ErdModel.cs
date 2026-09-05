using System.Text.Json;
using System.Text.Json.Serialization;

namespace JSini.Web.ProjMng.Api;

/// <summary>
/// ERD·플로우 다이어그램의 저장 형식 — 옛 Blazor 의 <c>ProjModel/ErdInfo.cs</c>,
/// Vue 의 <c>erd-types.ts</c> 와 1:1.
///
/// DB(<c>sp_dev_db_prop_exec</c> 의 <c>db_pvalue</c>, <c>db_pkey='erd'</c>)에
/// 이 형태의 JSON 이 이미 쌓여 있다. <b>필드 이름을 바꾸면 기존 다이어그램을
/// 읽지 못한다.</b> 그래서 이름을 attribute 로 못박아 두었다 — JS interop 의
/// camelCase 직렬화 규칙이 바뀌어도 와이어 모양이 흔들리지 않는다.
/// </summary>
public sealed record ErdModel
{
    [JsonPropertyName("entities")]
    public List<ErdEntity> Entities { get; init; } = [];

    [JsonPropertyName("relations")]
    public List<ErdRelation> Relations { get; init; } = [];

    /// <summary>비어 있는 모델. 저장본이 없는 새 다이어그램이 여기서 시작한다.</summary>
    public static ErdModel Empty => new();

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// 문자열로 저장된 ERD JSON 을 안전하게 읽는다. 깨져 있으면 빈 모델을 준다 —
    /// 저장본 하나가 손상됐다고 화면이 통째로 죽으면 고칠 방법도 없어진다.
    /// (Vue 의 <c>parseErdModel</c> 과 같은 방어다.)
    /// </summary>
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

    /// <summary>저장용 JSON. Vue 와 같이 들여쓰기를 넣는다 — DB 속성 화면에서 사람이 읽는다.</summary>
    public string ToJson() =>
        JsonSerializer.Serialize(this, new JsonSerializerOptions(JsonOptions) { WriteIndented = true });
}

/// <summary>다이어그램의 도형 하나. ERD 에서는 테이블, 플로우에서는 단계다.</summary>
public sealed record ErdEntity
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("desc")]
    public string? Desc { get; init; }

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
}

/// <summary>도형 사이의 관계선. <c>From</c>·<c>To</c> 는 엔터티 id 다.</summary>
public sealed record ErdRelation
{
    [JsonPropertyName("from")]
    public string From { get; init; } = string.Empty;

    [JsonPropertyName("to")]
    public string To { get; init; } = string.Empty;

    [JsonPropertyName("label")]
    public string? Label { get; init; }
}
