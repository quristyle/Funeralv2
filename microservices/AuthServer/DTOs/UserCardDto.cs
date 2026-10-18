namespace AuthServer.DTOs;

/// <summary>
/// 아바타를 눌렀을 때 뜨는 사람 카드 한 장(<c>GET /user/card</c>).
/// </summary>
/// <remarks>
/// 얼굴 목록(<see cref="UserFaceDto"/>)과 따로 둔 것은 연락처 때문이다 — 목록 화면이
/// 열릴 때마다 줄 수만큼의 메일·전화가 브라우저로 가지 않도록, 사람이 누른 한 명만 준다.
/// </remarks>
public class UserCardDto
{
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Avatar { get; set; }
    public string? Company { get; set; }
    public string? Department { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
}
