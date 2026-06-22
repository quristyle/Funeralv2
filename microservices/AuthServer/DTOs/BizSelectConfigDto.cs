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

    public string? Remark { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class BizSelectConfigCreateDto
{
    [Required]
    public string BizType { get; set; } = string.Empty;

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

    public string? Remark { get; set; }
}
