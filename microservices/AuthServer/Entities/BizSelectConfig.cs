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
    /// 데이터를 조회할 API 주소
    /// </summary>
    [Required]
    [Column("api_url")]
    public string ApiUrl { get; set; } = string.Empty;

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
