namespace AuthServer.DTOs;

/// <summary>
/// 다국어 자원 DTO
/// </summary>
public class I18nResourceDto
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Locale { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Category { get; set; }
}

public class CreateI18nResourceDto
{
    public string Key { get; set; } = string.Empty;
    public string Locale { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Category { get; set; }
}



public class SearchI18nParams
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Locale { get; set; }
    public string? Key { get; set; }
    public string? Value { get; set; }
    public string? Category { get; set; }
}
