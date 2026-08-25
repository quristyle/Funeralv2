using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using JSini.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>
/// 비즈니스 콤보박스 설정 엔티티 클래스
/// </summary>
[Table("biz_select_configs", Schema = "scom")]
public class BizSelectConfig : BaseEntity<string>
{
    /// <summary>
    /// BizSelectConfig 클래스의 새 인스턴스를 초기화하고 고유 식별자(GUID)를 생성합니다.
    /// </summary>
    public BizSelectConfig()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// 비즈니스 유형 (예: 계정구분, 권한그룹 등 특정 콤보박스가 사용할 도메인 유형)
    /// </summary>
    [Required]
    [Column("biz_type")]
    public string BizType { get; set; } = string.Empty;

    /// <summary>
    /// 어느 MSA 를 호출할지 (게이트웨이 프리픽스이자 프론트 요청 클라이언트 선택 키).
    /// auth · funeral · helpdesk · projmng · file · ai
    ///
    /// 서비스마다 응답 봉투가 달라서(포털 <c>{ code, data }</c>, 헬프데스크 <c>{ success, data }</c>,
    /// 프로젝트관리 <c>{ code:숫자, cols, data }</c>) 프론트는 이 값으로 봉투를 벗길 클라이언트를 고른다.
    /// 단순한 URL 프리픽스가 아니다.
    /// </summary>
    [Required]
    [Column("service_code")]
    public string ServiceCode { get; set; } = "auth";

    /// <summary>
    /// 서비스 안에서의 API 경로. MSA 프리픽스는 빼고 적는다 (예: <c>/system/companies</c>).
    /// </summary>
    [Required]
    [Column("api_url")]
    public string ApiUrl { get; set; } = string.Empty;

    /// <summary>
    /// 호출할 때 항상 함께 보내는 고정 파라미터 (JSON 객체).
    /// 프로젝트관리처럼 프로시저 이름을 본문에 실어야 하는 경우에 쓴다.
    /// 예: <c>{"ProcName":"sp_projCommon","ProcType":"srch"}</c>
    /// </summary>
    [Column("static_params")]
    public string? StaticParams { get; set; }

    /// <summary>
    /// 화면이 넘기는 런타임 파라미터를 본문의 어느 자리에 넣을지 (점 표기).
    /// 비어 있으면 본문(또는 쿼리스트링) 최상위에 붙인다.
    /// 예: 프로젝트관리는 <c>MainParam</c>.
    /// </summary>
    [Column("param_path")]
    public string? ParamPath { get; set; }

    /// <summary>
    /// HTTP 메서드 (예: GET, POST 등, 기본값: GET)
    /// </summary>
    [Required]
    [Column("http_method")]
    public string HttpMethod { get; set; } = "GET";

    /// <summary>
    /// 화면에 표시할 텍스트에 해당하는 필드명
    /// </summary>
    [Required]
    [Column("label_field")]
    public string LabelField { get; set; } = string.Empty;

    /// <summary>
    /// 실제 서버로 전송할 값에 해당하는 필드명
    /// </summary>
    [Required]
    [Column("value_field")]
    public string ValueField { get; set; } = string.Empty;

    /// <summary>
    /// API 응답 JSON 내에서 실제 배열/목록이 위치하는 경로
    /// </summary>
    [Column("result_path")]
    public string? ResultPath { get; set; }

    /// <summary>
    /// 데이터를 파싱 및 가공할 처리기(Processor) 유형
    /// </summary>
    [Column("processor_type")]
    public string? ProcessorType { get; set; }

    /// <summary>
    /// 비고 및 추가 설명
    /// </summary>
    [Column("remark")]
    public string? Remark { get; set; }
}
