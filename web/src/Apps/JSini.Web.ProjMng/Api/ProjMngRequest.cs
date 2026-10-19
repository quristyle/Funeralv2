using System.Text.Json.Serialization;

namespace JSini.Web.ProjMng.Api;

/// <summary>ProjMngServer 로 보내는 요청 봉투. 서버 RequestDto 와 1:1 이다. 속성 이름은 파스칼 케이스 그대로 — camelCase 로 보내면 전부 null 로 들어간다.</summary>
public sealed class ProjMngRequest
{
    /// <summary>호출할 프로시저 이름. sp_ 는 업무 프로시저, md_ 는 서버측 파일 스캔이다.</summary>
    [JsonPropertyName("ProcName")]
    public string ProcName { get; set; } = string.Empty;

    /// <summary>프로시저가 req_type 으로 받는다(srch · save · delete). 비어 있으면 싣지 않는다 — null 이 오면 서버가 400 을 낸다.</summary>
    [JsonPropertyName("ProcType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProcType { get; set; }

    /// <summary>프로젝트에 정의된 외부 DB 로 붙을지 (개발 도구 화면들이 쓴다).</summary>
    [JsonPropertyName("IsProjDb")]
    public bool IsProjDb { get; set; }

    /// <summary>프로시저 파라미터. <b>값은 모두 문자열이다</b>(서버가 그렇게 받는다).</summary>
    [JsonPropertyName("MainParam")]
    public Dictionary<string, string> MainParam { get; set; } = [];

    /// <summary>다건 저장용 변경 행. 비어 있으면 싣지 않는다 — null 이 오면 서버가 400 을 내고 조회가 빈 표로 삼켜진다.
    /// SSUserId 는 보내지 않는다 — 서버가 게이트웨이 신원으로 덮어쓴다.</summary>
    [JsonPropertyName("MultyData")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Dictionary<string, object?>>? MultyData { get; set; }
}
