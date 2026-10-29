namespace JSini.Shared.DTOs;

/// <summary>
/// 내부 서버 Ping 응답 결과 DTO.
/// </summary>
public class ServerPingResultDto
{
    /// <summary>검사 대상 IP 주소.</summary>
    public string TargetIp { get; set; } = "192.168.219.229";

    /// <summary>검사 대상 장비명 (예: 그리드 중계 장비, jin sm 장비).</summary>
    public string? DeviceName { get; set; }

    /// <summary>서버 생존(응답 수신) 여부.</summary>
    public bool IsAlive { get; set; }

    /// <summary>왕복 시간(밀리초).</summary>
    public long RoundtripTimeMs { get; set; }

    /// <summary>응답 상태 문자열 (Success, TimedOut, DestinationHostUnreachable, Error 등).</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>상세 안내 또는 오류 메시지.</summary>
    public string? Message { get; set; }

    /// <summary>확인 시각 (UTC).</summary>
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 내부 서버 Ping 감시 대상 장비 DTO.
/// </summary>
public class ServerPingTargetDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Ip { get; set; } = string.Empty;
    public int TimeoutMs { get; set; } = 2000;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// 장비 등록 요청 DTO.
/// </summary>
public class CreateServerPingTargetDto
{
    public string Name { get; set; } = string.Empty;
    public string Ip { get; set; } = string.Empty;
    public int TimeoutMs { get; set; } = 2000;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// 장비 수정 요청 DTO.
/// </summary>
public class UpdateServerPingTargetDto
{
    public string Name { get; set; } = string.Empty;
    public string Ip { get; set; } = string.Empty;
    public int TimeoutMs { get; set; } = 2000;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// 장비 정보 및 실시간 Ping 상태를 함께 담는 DTO.
/// </summary>
public class ServerPingDeviceStatusDto
{
    public ServerPingTargetDto Target { get; set; } = new();
    public ServerPingResultDto PingResult { get; set; } = new();
}
