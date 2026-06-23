namespace AuthServer.DTOs;

public class UpdateSettingDto
{
    public string FieldName { get; set; } = string.Empty;
    public bool Value { get; set; }
}
