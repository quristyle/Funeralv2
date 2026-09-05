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

    /// <summary>
    /// 프로시저가 <c>req_type</c> 으로 받는다. srch · save · delete.
    ///
    /// <para>
    /// <b>비어 있으면 아예 싣지 않는다.</b> 서버의 <c>RequestDto.ProcType</c> 은
    /// nullable 이 아니라, <c>"ProcType": null</c> 이 들어오면 모델 검증이
    /// <c>The ProcType field is required.</c> 로 <b>400</b> 을 낸다.
    /// 개발 도구·스캔 호출(<c>JsCont</c> · <c>MdCont</c>)이 이 값을 채우지
    /// 않으므로, 싣지 않아야 서버의 기본값(<c>srch</c>)이 쓰인다.
    /// </para>
    /// </summary>
    [JsonPropertyName("ProcType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
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
    ///
    /// <para>
    /// <b>비어 있으면 아예 싣지 않는다. 이것이 없어서 프로젝트관리 화면 전부가
    /// 빈 표였다.</b>
    /// </para>
    /// <para>
    /// 서버의 <c>RequestDto.MultyData</c> 는 nullable 이 아니라, 본문에
    /// <c>"MultyData": null</c> 이 들어오면 모델 검증이
    /// <c>The MultyData field is required.</c> 로 <b>400</b> 을 낸다.
    /// 조회는 이 값을 채우지 않으므로 <b>조회가 전부 400</b> 이었고,
    /// 클라이언트가 그 실패를 삼켜(빈 결과로 바꿔) 화면에는 「자료가 없다」로
    /// 보였다. 서버·화면 어느 쪽을 봐도 이상한 데가 없어 보이는 종류다.
    /// </para>
    /// </summary>
    [JsonPropertyName("MultyData")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Dictionary<string, object?>>? MultyData { get; set; }
}
