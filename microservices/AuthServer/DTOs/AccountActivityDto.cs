namespace AuthServer.DTOs;

/// <summary>접속 기록 한 줄</summary>
public class LoginLogDto
{
    public DateTime? At { get; set; }
    public bool Success { get; set; }

    /// <summary>실패 이유. 성공이면 null</summary>
    public string? FailReason { get; set; }

    public string? Ip { get; set; }

    /// <summary>브라우저·기기 원문</summary>
    public string? UserAgent { get; set; }

    /// <summary>브라우저·기기를 사람이 읽을 수 있게 줄인 것. 화면에는 이것을 쓴다(원문은 길고 잡음이 많다).</summary>
    public string? Device { get; set; }
}

/// <summary>계정 활동 정보. 계정 정보 화면이 한 번에 받는다.</summary>
/// 가입일·비밀번호 같은 계정 값은 user/info 가 주고, 여기는 기록에서 계산하는 값만 담는다.
public class AccountActivityDto
{
    /// <summary>로그인 성공 횟수 (기록이 쌓인 뒤부터)</summary>
    public int LoginCount { get; set; }

    /// <summary>지난번 접속. 지금 이 접속의 바로 앞이다 — 낯선 시각·주소가 보이면 남이 들어온 것이다.</summary>
    public LoginLogDto? PreviousLogin { get; set; }

    /// <summary>최근 30일 안의 로그인 실패 횟수</summary>
    public int RecentFailCount { get; set; }

    /// <summary>가장 최근 실패. 없으면 null</summary>
    public LoginLogDto? LastFail { get; set; }

    /// <summary>이 계정을 써 온 일수 (가입일부터 오늘까지)</summary>
    public int AccountAgeDays { get; set; }

    /// <summary>최근 접속 기록. 최신 순.</summary>
    public List<LoginLogDto> Recent { get; set; } = new();
}
