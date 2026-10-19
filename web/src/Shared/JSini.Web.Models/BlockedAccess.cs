namespace JSini.Web.Models;

// 차단 접근기록. 권한 밖의 화면을 누가 어떤 주소로 열려 했나. 쌓는 것은 라우터 앞의 문(공용)이고 보는 것은 포털관리 화면이라 셸에 둔다(MenuUsage 와 같은 까닭).
// 시각은 UTC 로 온다. 한국 시각은 보여 주기 직전에 만든다(docs/utc-time.md).

/// <summary>막힌 까닭. 둘을 가르는 것이 이 표의 요점이다. 권한을 더 주면 되는 것과 메뉴를 만들어야 하는 것은 할 일이 다르다(후자는 코드가 고쳐지기 전에는 권한을 줘도 안 열린다).</summary>
public static class BlockedAccessReason
{
    /// <summary>메뉴는 찾았는데 그 사람에게 열람 권한이 없다.</summary>
    public const string NoPermission = "NO_PERMISSION";

    /// <summary>
    /// 가리킬 메뉴가 없다 — <b>권한관리 범위 밖의 화면</b>이다.
    /// 메뉴로 등록하거나 화면이 <c>[ScreenAccess]</c> 로 부모를 가리켜야 한다.
    /// </summary>
    public const string Unmanaged = "UNMANAGED";

    /// <summary>사람이 읽을 이름.</summary>
    public static string Text(string? reason) => reason switch
    {
        NoPermission => "권한 없음",
        Unmanaged => "관리 밖",
        _ => reason ?? "-",
    };
}

/// <summary>「이 주소를 막았다」고 알릴 때 보내는 것. 사람은 담지 않는다. 서버가 X-User-Id 로 정한다(몸체에서 받으면 남의 이름으로 쌓을 수 있다).</summary>
public sealed class BlockedAccessRecordDto
{
    /// <summary>막힌 주소. <b>사람이 실제로 연 그 값</b>이고, 이것이 없으면 서버가 버린다.</summary>
    public string? Href { get; set; }

    /// <summary>화면이 선언한 열쇠(<c>RouteKey</c>). 신고가 없는 화면은 비어 있다.</summary>
    public string? RouteKey { get; set; }

    /// <summary>라우터가 끼우려던 화면 클래스의 이름. 「어느 파일이냐」의 답이다.</summary>
    public string? ScreenType { get; set; }

    /// <summary>권한을 따진 메뉴 경로. 메뉴를 못 찾았으면 비어 있다.</summary>
    public string? MenuPath { get; set; }

    /// <summary>왜 막혔나(<see cref="BlockedAccessReason"/>).</summary>
    public string? Reason { get; set; }

    /// <summary>직전에 보던 화면. 「어디서 눌러 들어왔나」다.</summary>
    public string? FromPath { get; set; }

    /// <summary>접속 주소. 위조할 수 있는 값이라 보여 주기 전용이다(ClientAddress 머리말).</summary>
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

    /// <summary>표에 그릴 이름. 이름을 모르면 아이디만 적는다.</summary>
    public string Who => string.IsNullOrWhiteSpace(UserName) ? UserId : $"{UserName} ({UserId})";

    /// <summary>표에 그릴 까닭.</summary>
    public string ReasonText => BlockedAccessReason.Text(Reason);

    /// <summary>표에 그릴 화면 이름. 메뉴를 못 찾았으면 열쇠, 그것도 없으면 주소다.</summary>
    public string Screen =>
        !string.IsNullOrWhiteSpace(MenuTitle) ? MenuTitle!
        : !string.IsNullOrWhiteSpace(RouteKey) ? RouteKey!
        : Href;
}

/// <summary>화면 하나의 기간 차단량. 「어떤 화면이 관리 밖인가」에 답하는 표다.</summary>
public sealed class BlockedAccessByScreenDto
{
    /// <summary>묶는 열쇠 — 화면의 <c>RouteKey</c>, 없으면 주소.</summary>
    public string ScreenKey { get; set; } = string.Empty;

    public string? RouteKey { get; set; }

    public string? MenuPath { get; set; }

    public string? MenuTitle { get; set; }

    /// <summary>그 화면에서 가장 마지막에 막힌 주소. 매개변수가 든 실제 값이다.</summary>
    public string? LastHref { get; set; }

    /// <summary>까닭. 한 화면에 섞여 있으면 많은 쪽이다.</summary>
    public string? Reason { get; set; }

    public int Blocks { get; set; }

    /// <summary>막힌 사람의 수.</summary>
    public int Users { get; set; }

    public DateTime LastBlockedAt { get; set; }

    public string ReasonText => BlockedAccessReason.Text(Reason);

    public string Screen =>
        !string.IsNullOrWhiteSpace(MenuTitle) ? MenuTitle! : ScreenKey;
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

    public string Who => string.IsNullOrWhiteSpace(UserName) ? UserId : $"{UserName} ({UserId})";
}
