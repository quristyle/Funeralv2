using System.Text.Json.Serialization;

namespace JSini.Web.HelpDesk.Api;

/// <summary>헬프데스크(HelpDeskServer)의 응답 봉투. { success, message, data, meta, totalcount, totalpagecount } 꼴이라 GatewayClient 가 아니라 여기서 벗긴다.
/// 서버가 JinRestApi 이식이라 봉투를 바꾸면 살아 있는 JinReception 과 어긋난다 — 차이는 이 로컬 클라이언트가 흡수한다. 목록의 총건수는 봉투 최상위(소문자)에 실린다.</summary>
internal sealed class HelpDeskEnvelope<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>일부 엔드포인트는 success 대신 ok 로 말한다. 두 이름이 섞여 있어 한쪽만 보면 「처리하지 못했습니다」로 끝난다 — 받는 쪽에서 흡수한다.</summary>
    [JsonPropertyName("ok")]
    public bool? Ok { get; set; }

    /// <summary>성공했는가. 두 이름 중 하나라도 참이면 성공이다.</summary>
    [JsonIgnore]
    public bool IsSuccess => Success || Ok == true;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public T? Data { get; set; }

    [JsonPropertyName("meta")]
    public HelpDeskMeta? Meta { get; set; }

    [JsonPropertyName("totalcount")]
    public int? TotalCount { get; set; }

    [JsonPropertyName("totalpagecount")]
    public int? TotalPageCount { get; set; }
}

/// <summary>봉투의 부가 정보. 지금 화면들은 rowCount 정도만 본다.</summary>
internal sealed class HelpDeskMeta
{
    [JsonPropertyName("rowCount")]
    public int? RowCount { get; set; }

    [JsonPropertyName("columnCount")]
    public int? ColumnCount { get; set; }

    [JsonPropertyName("duration")]
    public string? Duration { get; set; }
}

/// <summary>목록 조회 결과 — 자료와 전체 건수를 함께 돌려준다.</summary>
public sealed record HelpDeskPage<T>(IReadOnlyList<T> Items, int TotalCount, int TotalPageCount);
