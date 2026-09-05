using System.Text.Json.Serialization;

namespace JSini.Web.ProjMng.Api;

/// <summary>
/// ProjMngServer 로 보내는 요청 봉투. 서버의 <c>RequestDto</c> 와 1:1 이다.
///
/// 속성 이름이 파스칼 케이스 그대로인 것에 주의 — 서버가 그렇게 받는다.
/// 다른 서비스처럼 camelCase 로 보내면 전부 <c>null</c> 로 들어간다.
/// </summary>
public sealed class ProjMngRequest
{
    /// <summary>
    /// 호출할 프로시저 이름. <c>sp_</c> 로 시작하면 업무 프로시저,
    /// <c>md_</c> 는 서버측 파일 스캔이다.
    /// </summary>
    [JsonPropertyName("ProcName")]
    public string ProcName { get; set; } = string.Empty;

    /// <summary>프로시저가 <c>req_type</c> 으로 받는다. srch · save · delete.</summary>
    [JsonPropertyName("ProcType")]
    public string? ProcType { get; set; }

    /// <summary>프로젝트에 정의된 외부 DB 로 붙을지 (개발 도구 화면들이 쓴다).</summary>
    [JsonPropertyName("IsProjDb")]
    public bool IsProjDb { get; set; }

    /// <summary>프로시저 파라미터. <b>값은 모두 문자열이다</b>(서버가 그렇게 받는다).</summary>
    [JsonPropertyName("MainParam")]
    public Dictionary<string, string> MainParam { get; set; } = [];

    /// <summary>
    /// 다건 저장용 변경 행 목록. 비어 있지 않으면 서버가 다건 처리 경로로 간다.
    ///
    /// <c>SSUserId</c> 는 보내지 않는다 — 프론트에서 채워도 서버가 게이트웨이
    /// 신원(<c>X-User-Id</c>)으로 덮어쓴다. 위조해도 의미가 없다.
    /// </summary>
    [JsonPropertyName("MultyData")]
    public List<Dictionary<string, object?>>? MultyData { get; set; }
}
