namespace AuthServer.DTOs;

// 차단 접근기록 — 프론트의 JSini.Web.Models/BlockedAccess.cs 와 짝이다. 시각은 UTC 로 주고받고, 한국 시각은 보여 주기 직전에 한 번만 만든다.

/// <summary>포털 셸이 「이 주소를 막았다」고 알릴 때 보내는 것. 사람은 담지 않고 게이트웨이의 X-User-Id 로 서버가 정한다 — 몸체에서 받으면 남의 이름으로 기록을 쌓을 수 있다.</summary>
public sealed class BlockedAccessRecordDto
{
    /// <summary>막힌 주소. 이것이 없으면 적지 않는다.</summary>
    public string? Href { get; set; }

    /// <summary>화면이 선언한 열쇠.</summary>
    public string? RouteKey { get; set; }

    /// <summary>라우터가 끼우려던 화면 클래스의 이름.</summary>
    public string? ScreenType { get; set; }

    /// <summary>권한을 따진 메뉴 경로. 메뉴를 못 찾았으면 비어 있다.</summary>
    public string? MenuPath { get; set; }

    /// <summary>왜 막혔나 — <c>NO_PERMISSION</c> · <c>UNMANAGED</c>.</summary>
    public string? Reason { get; set; }

    /// <summary>직전에 보던 화면.</summary>
    public string? FromPath { get; set; }

    /// <summary>접속 주소. 보여 주기 전용이다.</summary>
    public string? ClientIp { get; set; }
}

/// <summary>차단 한 건. 기록 표의 한 줄이다.</summary>
public sealed class BlockedAccessDto
{
    public long Id { get; set; }

    /// <summary>막힌 때. <b>UTC 다.</b></summary>
    public DateTime OccurredAt { get; set; }

    public string UserId { get; set; } = string.Empty;

    /// <summary>이름. <b>계정을 지운 사람은 비어 있다</b> — 그래도 줄은 남는다.</summary>
    public string? UserName { get; set; }

    public string? DepartmentName { get; set; }

    public string Href { get; set; } = string.Empty;

    public string? RouteKey { get; set; }

    public string? ScreenType { get; set; }

    public string? MenuPath { get; set; }

    /// <summary>그 메뉴의 <b>지금</b> 제목. 메뉴를 못 찾았으면 비어 있다.</summary>
    public string? MenuTitle { get; set; }

    public string? Reason { get; set; }

    public string? FromPath { get; set; }

    public string? ClientIp { get; set; }
}

/// <summary>화면 하나의 기간 차단량. 「어떤 화면이 관리 밖인가」에 답하는 표다. 주소로 묶지 않는다 — 상세 화면 주소에는 사람 이름이 들어 있어 사람 수만큼 줄이 갈리므로, 묶는 열쇠는 화면이 선언한 열쇠다.</summary>
public sealed class BlockedAccessByScreenDto
{
    public string ScreenKey { get; set; } = string.Empty;

    public string? RouteKey { get; set; }

    public string? MenuPath { get; set; }

    public string? MenuTitle { get; set; }

    /// <summary>그 화면에서 가장 마지막에 막힌 주소.</summary>
    public string? LastHref { get; set; }

    /// <summary>까닭. 한 화면에 섞여 있으면 많은 쪽이다.</summary>
    public string? Reason { get; set; }

    public int Blocks { get; set; }

    public int Users { get; set; }

    public DateTime LastBlockedAt { get; set; }
}

/// <summary>한 사람의 기간 차단량.</summary>
public sealed class BlockedAccessByUserDto
{
    public string UserId { get; set; } = string.Empty;

    public string? UserName { get; set; }

    public string? DepartmentName { get; set; }

    public int Blocks { get; set; }

    /// <summary>그중 서로 다른 화면의 수.</summary>
    public int Screens { get; set; }

    /// <summary>가장 많이 막힌 화면.</summary>
    public string? TopScreen { get; set; }

    public int TopScreenBlocks { get; set; }

    public DateTime FirstBlockedAt { get; set; }

    public DateTime LastBlockedAt { get; set; }
}
