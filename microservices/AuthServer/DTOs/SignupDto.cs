namespace AuthServer.DTOs;

/// <summary>
/// 가입 신청 (익명).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="CreateAccountDto"/> 를 쓰지 않는다. 그쪽은 <b>관리자가</b> 계정을
/// 만드는 자리라 부서·역할·상태를 직접 정하고 비밀번호는 정해진 기본값이
/// 들어간다. 신청자가 그 칸들을 채우게 두면 <b>스스로 역할을 고르는</b> 셈이다.
/// </para>
/// <para>
/// 그래서 신청서에는 사람이 자기에 대해 적는 것만 둔다. 소속과 역할은
/// 승인하는 사람이 계정 관리에서 붙인다.
/// </para>
/// </remarks>
public class SignupRequestDto
{
    /// <summary>쓰려는 로그인 아이디. 이미 있으면 거절한다.</summary>
    public string LoginId { get; set; } = string.Empty;

    /// <summary>이름. 승인하는 사람이 누구인지 알아볼 수 있어야 한다.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// 이메일. <b>있어야 한다</b> — 승인 안내와 비밀번호 찾기가 이 값을 쓴다.
    /// </summary>
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
}
