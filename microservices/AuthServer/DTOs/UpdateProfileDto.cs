namespace AuthServer.DTOs;

public class UpdateProfileDto
{
    public string? RealName { get; set; }
    public string? Introduction { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Avatar { get; set; }
    public string? AvatarGroupId { get; set; }
}
