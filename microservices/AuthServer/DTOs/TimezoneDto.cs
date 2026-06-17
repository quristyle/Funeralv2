namespace AuthServer.DTOs;

/// <summary>
/// 타임존 선택 리스트를 위한 옵션 DTO
/// </summary>
public class TimezoneOptionDto
{
    /// <summary>
    /// 사용자에게 표시될 타임존 이름
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// 실제 타임존 값 (ID)
    /// </summary>
    public string Value { get; set; } = string.Empty;
}
