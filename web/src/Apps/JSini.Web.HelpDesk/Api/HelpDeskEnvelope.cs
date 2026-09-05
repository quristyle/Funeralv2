using System.Text.Json.Serialization;

namespace JSini.Web.HelpDesk.Api;

/// <summary>
/// 헬프데스크(HelpDeskServer)의 응답 봉투. <b>다른 서비스와 모양이 다르다.</b>
///
/// <code>
///   funeralv2 계열 : { code: 'S000', data: { result: [...] } }   → GatewayClient 가 처리
///   헬프데스크     : { success: true, message, data, meta,
///                      totalcount, totalpagecount }               → 이 파일
/// </code>
///
/// 서버는 JinRestApi 를 그대로 이식한 것이라 봉투를 바꾸면 아직 살아 있는
/// JinReception 과 어긋난다. 그래서 봉투 차이는 이 앱의 로컬 클라이언트가
/// 흡수한다 — Vue 의 <c>api/helpdesk/request.ts</c>(helpdeskClient)와 같은 구도다.
///
/// 목록 API 의 총건수는 meta 가 아니라 봉투 최상위의 <c>totalcount</c> /
/// <c>totalpagecount</c>(전부 소문자)에 실려 온다. 서버의 ApiResponseBuilder 가
/// 그렇게 만든다.
/// </summary>
internal sealed class HelpDeskEnvelope<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>
    /// 일부 엔드포인트는 <c>success</c> 대신 <c>ok</c> 로 말한다.
    ///
    /// 이식한 코드라 두 이름이 섞여 있다. 한쪽만 보면 그 엔드포인트를 쓰는
    /// 화면이 「요청을 처리하지 못했습니다」로 끝난다 — 사용자 속성 화면이
    /// 실제로 그랬다. 서버를 통일하는 편이 맞지만 그건 JinReception 과 함께
    /// 봐야 하는 일이라, 받는 쪽에서 흡수한다.
    /// </summary>
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
