namespace AuthServer.DTOs;

/// <summary>사람 하나의 이름과 얼굴. 화면이 「누가 한 일인가」를 그릴 때 쓴다. AccountDto 는 메일·전화·역할·소속까지 실려 있어 목록 카드에는 무겁다.</summary>
public class UserFaceDto
{
    /// <summary>포털 로그인 아이디(<c>scom.accounts.user_id</c>). 물어본 그 값이다.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>사람이 읽는 이름. 없으면 로그인 아이디다.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>프로필 사진 주소. 올린 적이 없으면 <c>null</c>.</summary>
    public string? Avatar { get; set; }
}
