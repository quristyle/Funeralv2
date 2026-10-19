namespace AuthServer.DTOs;

/// <summary>가입 신청 (익명).</summary>
/// 신청자는 자기에 대해 적는 것만 낸다. 소속·역할·상태는 승인하는 사람이 계정 관리에서 붙이므로 CreateAccountDto 를 쓰지 않는다.
public class SignupRequestDto
{
    /// <summary>쓰려는 로그인 아이디. 이미 있으면 거절한다.</summary>
    public string LoginId { get; set; } = string.Empty;

    /// <summary>이름. 승인하는 사람이 누구인지 알아볼 수 있어야 한다.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>이메일. <b>있어야 한다</b> — 승인 안내와 비밀번호 찾기가 이 값을 쓴다.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>연락처. 없어도 된다.</summary>
    public string? Phone { get; set; }

    /// <summary>본인이 정한 비밀번호. 신청 즉시 해시로 저장한다.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>신청 사유·소속 등 하고 싶은 말. 승인 화면에 그대로 보인다.</summary>
    public string? Note { get; set; }
}

/// <summary>승인 대기 신청 한 줄. 관리자 화면이 읽는다.</summary>
public class SignupPendingDto
{
    public string Id { get; set; } = string.Empty;
    public string LoginId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }

    /// <summary>신청자가 적은 말.</summary>
    public string? Note { get; set; }

    /// <summary>신청 시각.</summary>
    public DateTime RequestedAt { get; set; }

    /// <summary>소셜로 들어온 신청이면 그 공급자 열쇠. 직접 신청이면 null.</summary>
    public string? SocialProvider { get; set; }

    /// <summary>공급자에게서 받아 둔 프로필 사진 주소. 없으면 <c>null</c>.</summary>
    public string? PictureUrl { get; set; }

    /// <summary>우리 FileServer 로 옮겨 둔 계정 대표 사진. 화면은 이것을 먼저 쓰고, 없을 때만 PictureUrl 로 물러난다.</summary>
    public string? Avatar { get; set; }
}
