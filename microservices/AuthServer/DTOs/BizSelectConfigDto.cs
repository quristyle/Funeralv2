using System.ComponentModel.DataAnnotations;

namespace AuthServer.DTOs;

/// <summary>
/// BizSelect 메타데이터 구성을 위한 DTO
/// </summary>
public class BizSelectConfigDto
{
    public string Id { get; set; } = string.Empty;

    [Required]
    public string BizType { get; set; } = string.Empty;

    /// <summary>호출 대상 MSA (auth · funeral · helpdesk · projmng · file · ai)</summary>
    [Required]
    public string ServiceCode { get; set; } = "auth";

    /// <summary>MSA 프리픽스를 뺀 서비스 내부 경로</summary>
    [Required]
    public string ApiUrl { get; set; } = string.Empty;

    [Required]
    public string HttpMethod { get; set; } = "GET";

    [Required]
    public string LabelField { get; set; } = string.Empty;

    [Required]
    public string ValueField { get; set; } = string.Empty;

    public string? ResultPath { get; set; }

    public string? ProcessorType { get; set; }

    /// <summary>항상 함께 보내는 고정 파라미터 (JSON 객체 문자열)</summary>
    public string? StaticParams { get; set; }

    /// <summary>런타임 파라미터를 넣을 본문 내 경로 (점 표기)</summary>
    public string? ParamPath { get; set; }

    public string? Remark { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class BizSelectConfigCreateDto
{
    [Required]
    public string BizType { get; set; } = string.Empty;

    /// <summary>호출 대상 MSA (auth · funeral · helpdesk · projmng · file · ai)</summary>
    [Required]
    public string ServiceCode { get; set; } = "auth";

    /// <summary>MSA 프리픽스를 뺀 서비스 내부 경로</summary>
    [Required]
    public string ApiUrl { get; set; } = string.Empty;

    [Required]
    public string HttpMethod { get; set; } = "GET";

    [Required]
    public string LabelField { get; set; } = string.Empty;

    [Required]
    public string ValueField { get; set; } = string.Empty;

    public string? ResultPath { get; set; }

    public string? ProcessorType { get; set; }

    /// <summary>항상 함께 보내는 고정 파라미터 (JSON 객체 문자열)</summary>
    public string? StaticParams { get; set; }

    /// <summary>런타임 파라미터를 넣을 본문 내 경로 (점 표기)</summary>
    public string? ParamPath { get; set; }

    public string? Remark { get; set; }
}
