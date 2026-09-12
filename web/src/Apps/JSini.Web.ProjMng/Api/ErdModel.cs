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

    /// <summary>
    /// <b>옛 도구(draw.io)로 그린 그림인가.</b>
    ///
    /// <para>
    /// 운영 자료에 그런 것이 섞여 있었다 — 유즈케이스 20건이 <b>전부</b>
    /// <c>&lt;mxGraphModel …&gt;</c> XML 이었다. <see cref="Parse"/> 는 그것을
    /// <b>빈 모델</b>로 돌려주므로 화면은 「저장된 그림이 없습니다」로 보이고,
    /// 그 상태에서 새로 그려 저장하면 <b>옛 그림이 덮어써진다.</b>
    /// 되돌릴 수 없다.
    /// </para>
    ///
    /// <para>
    /// 2026-09-13 에 <b>남아 있던 것을 모두 옮겼다</b>
    /// (<c>scripts/projmng-drawio-to-diagram.py</c>) — 유즈케이스 20건과
    /// DB 접속 속성 8건. 원본은 지우지 않고 갈래를 달리해 남겨 두었으므로,
    /// <b>이 판정에 걸리는 것은 그 보관본과 누군가 새로 붙여 넣은 XML 뿐이다.</b>
    /// </para>
    ///
    /// <para>
    /// 그래서 화면은 이 판정으로 저장을 막고 그 이유를 말한다. 여는 꺾쇠
    /// 하나로 가른다 — JSON 저장본은 언제나 <c>{</c> 로 시작한다.
    /// </para>
    /// </summary>
    public static bool IsLegacyDrawing(string? raw) =>
        raw is not null && raw.TrimStart().StartsWith('<');

    /// <summary>
    /// <b>이 값이 그림인가</b> — 우리 JSON 이거나 옛 도구의 XML 이거나.
    /// </summary>
    /// <remarks>
    /// <para>
    /// DB 접속 속성(<c>dev_db_prop</c>)에는 그림만 있는 것이 아니다. 같은 표에
    /// 공통코드 질의(<c>code_master</c> · <c>code_detail</c>)와 프로시저 서식
    /// (<c>sp_fmt</c>)이 함께 들어 있고, 그것들은 SQL 글자다. 그림 고르개에
    /// 그 이름이 섞이면 <b>열었을 때 빈 캔버스가 뜨고 이유를 알 수 없다.</b>
    /// </para>
    ///
    /// <para>
    /// 여는 괄호로만 가르지 않는다 — 이 표의 SQL 은 <c>select</c> 로 시작하지만
    /// 언젠가 <c>{</c> 로 시작하는 값이 들어올 수 있다. <c>entities</c> 라는
    /// 낱말까지 함께 본다.
    /// </para>
    /// </remarks>
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

/// <summary>다이어그램의 도형 하나. ERD 에서는 테이블, 플로우에서는 단계다.</summary>
public sealed record ErdEntity
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("desc")]
    public string? Desc { get; init; }

    /// <summary>
    /// 상자에 늘어놓을 칸 목록(<c>prj_rid (PK) integer</c>).
    ///
    /// <para>
    /// <b>저장본에는 남기지 않는다.</b> 대상 DB 를 읽어 만든 값이라 그림의
    /// 성질이 아니고, 남기면 표가 바뀐 뒤에도 옛 칸이 그려진다.
    /// 불러올 때마다 새로 채운다(<see cref="Gone"/> 과 같은 판단).
    /// </para>
    /// </summary>
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

    /// <summary>
    /// <b>DB 에서 사라진 표인가.</b> 업무 흐름 화면이 켜고, 그림이 그 도형을
    /// 흐리게 그린다.
    ///
    /// <para>
    /// <b>저장본에는 남기지 않는다.</b> 이 값은 「지금 그 DB 에 있는가」라는
    /// 조회 결과이지 그림의 성질이 아니다 — 표를 되살리면 다음 불러오기에서
    /// 저절로 꺼진다. 남겨 두면 ERD 화면이 같은 저장본을 읽을 때 옛 판정을
    /// 물려받는다. 화면이 저장 직전에 끈다.
    /// </para>
    /// </summary>
    [JsonPropertyName("gone")]
    public bool Gone { get; init; }

    /// <summary>
    /// <b>사람이 캔버스에서 만든 도형인가.</b>
    ///
    /// <para>
    /// 표에서 온 도형과 갈라야 하는 자리가 둘이다. 하나는 <b>이름 편집</b> —
    /// 표에서 온 것은 이름·설명의 정본이 DB 라 캔버스에서 고칠 수 없고
    /// (고치려면 [테이블·컬럼 설명 관리]에서 코멘트를 고친다), 손으로 만든
    /// 것은 캔버스가 정본이라 고칠 수 있다. 다른 하나는 업무 흐름 화면의
    /// <b>「사라진 표」 표시</b> — 손으로 만든 도형은 애초에 표가 아니므로
    /// 그 판정에서 빼야 한다.
    /// </para>
    ///
    /// <para>
    /// <see cref="Gone"/> 과 달리 <b>저장본에 남긴다.</b> 이것은 조회 결과가
    /// 아니라 그림의 성질이다.
    /// </para>
    /// </summary>
    [JsonPropertyName("manual")]
    public bool Manual { get; init; }

    /// <summary>
    /// <b>손으로 만든 도형의 종류</b>(<c>rhombus</c> · <c>fcDoc</c> ·
    /// <c>bpmn.task</c> …). 도구상자가 주는 값이고, 정본은 JS 쪽 목록이다
    /// (<c>diagram-viewer.js</c> 의 <c>SHAPES</c>·stencil).
    ///
    /// <para>
    /// 한동안 이 칸이 없었다. 그래서 <b>저장 한 번으로 도형이 전부 네모가
    /// 됐다</b> — 불러오기가 손그림을 모두 같은 모양으로 그렸기 때문이다.
    /// 표에서 온 도형에는 없는 값이다.
    /// </para>
    /// </summary>
    [JsonPropertyName("kind")]
    public string? Kind { get; init; }

    /// <summary>
    /// <b>붙여넣은 그림</b>(data URL). 클립보드나 바탕화면에서 온 캡처·도식이다.
    ///
    /// <para>
    /// 파일 서버에 올리지 않고 저장본 안에 담는다. 그림이 다이어그램 한 장에
    /// 딸린 조각이라 따로 관리할 것이 없고, 주소로 두면 <b>그림을 그리는 쪽
    /// (브라우저의 SVG)이 인증 없이 파일 서버를 부르게 된다.</b>
    /// </para>
    ///
    /// <para>
    /// 대신 <b>넣기 전에 줄인다</b> — 긴 변 1100px, 한 장 420KB 가 상한이다
    /// (<c>diagram-viewer.js</c>). 원본을 그대로 담으면 저장이 회로의 수신
    /// 한도를 넘겨 <b>연결이 끊긴다</b>(셸의 <c>MaximumReceiveMessageSize</c>).
    /// </para>
    /// </summary>
    [JsonPropertyName("image")]
    public string? Image { get; init; }
}

/// <summary>도형 사이의 관계선. <c>From</c>·<c>To</c> 는 엔터티 id 다.</summary>
public sealed record ErdRelation
{
    /// <summary>
    /// <b>외래키에서 저절로 그어진 선인가.</b>
    ///
    /// <para>
    /// 사람이 끌어 그린 선과 갈라야 한다. 이 선은 대상 DB 의 제약에서 나오므로
    /// <b>저장하지 않는다</b> — 불러올 때마다 다시 그린다. 저장하면 제약을 뗀
    /// 뒤에도 선이 남고, 지워도 다음 불러오기에 되살아나 「지워지지 않는 선」이
    /// 된다.
    /// </para>
    /// </summary>
    [JsonPropertyName("auto")]
    public bool Auto { get; init; }

    [JsonPropertyName("from")]
    public string From { get; init; } = string.Empty;

    [JsonPropertyName("to")]
    public string To { get; init; } = string.Empty;

    [JsonPropertyName("label")]
    public string? Label { get; init; }
}
