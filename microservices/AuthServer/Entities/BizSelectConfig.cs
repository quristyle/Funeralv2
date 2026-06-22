using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Funeralv2.Shared.Domain;

namespace AuthServer.Entities;

/// <summary>
/// 비즈니스 콤보박스 설정 엔티티
/// </summary>
[Table("biz_select_configs", Schema = "scom")]
public class BizSelectConfig : BaseEntity<string>
{
    public BizSelectConfig()
    {
        Id = Guid.NewGuid().ToString();
    }

    [Required]
    [Column("biz_type")]
    public string BizType { get; set; } = string.Empty;

    [Required]
    [Column("api_url")]
    public string ApiUrl { get; set; } = string.Empty;

    [Required]
    [Column("http_method")]
    public string HttpMethod { get; set; } = "GET";

    [Required]
    [Column("label_field")]
    public string LabelField { get; set; } = string.Empty;

    [Required]
    [Column("value_field")]
    public string ValueField { get; set; } = string.Empty;

    [Column("result_path")]
    public string? ResultPath { get; set; }

    [Column("processor_type")]
    public string? ProcessorType { get; set; }

    [Column("remark")]
    public string? Remark { get; set; }
}
