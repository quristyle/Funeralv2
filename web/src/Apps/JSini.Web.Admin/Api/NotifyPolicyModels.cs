namespace JSini.Web.Admin.Api;

/// <summary>
/// 알림 이벤트 하나 — 무엇이 언제 나가는 알림이고, 지금 어느 역할이 어느
/// 길로 받게 돼 있나.
/// </summary>
/// <remarks>
/// 서버(<c>NotificationServer</c>)의 <c>NotificationEventDto</c> 와 짝이다.
/// 이벤트와 정책을 한 덩어리로 받는다 — 왼쪽에서 이벤트를 고르면 오른쪽이
/// 바로 서야 하는데 나눠 받으면 고를 때마다 왕복이 하나 붙는다.
/// </remarks>
public sealed class NotifyEventDto
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>알림구분 코드값. 없을 수 있다(보고서 메일·문의 접수).</summary>
    public string? Category { get; set; }

    /// <summary>알림구분의 사람이 읽는 이름. 서버가 공통코드에서 풀어 준다.</summary>
    public string? CategoryName { get; set; }

    /// <summary>어느 서비스가 보내나.</summary>
    public string? Source { get; set; }

    /// <summary><c>ROLE</c> · <c>USER</c>.</summary>
    public string TargetKind { get; set; } = NotifyTargetKinds.Role;

    public bool SupportsPush { get; set; }

    public bool SupportsEmail { get; set; }

    /// <summary>거짓이면 설정을 받아 두되 발송에는 아직 안 걸린다.</summary>
    public bool Governed { get; set; }

    /// <summary>끄면 이 이벤트의 알림이 아무에게도 안 간다.</summary>
    public bool IsActive { get; set; }

    public int OrderNo { get; set; }

    public List<NotifyPolicyDto> Policies { get; set; } = [];

    /// <summary>
    /// 지금 <b>제한이 없는가</b>. <b>서버가 셈해 준다</b> — 「역할 0 개 =
    /// 아무도 못 받는다」가 가장 흔한 오해라 화면이 다시 세지 않는다.
    /// </summary>
    public bool Unrestricted { get; set; }

    /// <summary>마지막으로 정책을 고친 때(UTC). 줄이 없으면 <c>null</c>.</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>목록 표에 한 칸으로 적는 요약 — 「앱푸시 2 · 이메일 1」.</summary>
    public string ChannelSummary
    {
        get
        {
            if (!Governed) return "정책 적용 안 됨";
            if (!IsActive) return "꺼짐";
            if (Unrestricted) return "제한 없음";

            var push = Policies.Count(p => p.IsActive && p.PushEnabled);
            var mail = Policies.Count(p => p.IsActive && p.EmailEnabled);

            var parts = new List<string>();
            if (push > 0) parts.Add($"앱푸시 {push}");
            if (mail > 0) parts.Add($"이메일 {mail}");

            return parts.Count == 0 ? "제한 없음" : string.Join(" · ", parts);
        }
    }

    /// <summary>대상 성격을 사람이 읽는 글자로.</summary>
    public string TargetText => NotifyTargetKinds.Describe(TargetKind);
}

/// <summary>이벤트 하나에 매달린 역할 한 줄(서버가 준 것).</summary>
public sealed class NotifyPolicyDto
{
    public string RoleId { get; set; } = string.Empty;

    public string? RoleName { get; set; }

    public bool PushEnabled { get; set; }

    public bool EmailEnabled { get; set; }

    public bool IsActive { get; set; } = true;

    public int AccountCount { get; set; }
}

/// <summary>이벤트 하나의 정책을 통째로 보낸다.</summary>
public sealed class SaveNotifyPolicyDto
{
    public bool IsActive { get; set; } = true;

    public List<SaveNotifyPolicyRowDto> Policies { get; set; } = [];
}

/// <summary>저장할 역할 한 줄.</summary>
public sealed class SaveNotifyPolicyRowDto
{
    public string RoleId { get; set; } = string.Empty;

    public bool PushEnabled { get; set; }

    public bool EmailEnabled { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>「지금 이 설정이면 누구에게 가나」.</summary>
public sealed class NotifyPolicyPreviewDto
{
    public string EventCode { get; set; } = string.Empty;

    public bool Unrestricted { get; set; }

    public bool EventDisabled { get; set; }

    public List<NotifyRecipientDto> Recipients { get; set; } = [];

    public int PushReachable { get; set; }

    public int EmailReachable { get; set; }
}

/// <summary>미리보기 한 줄 — 사람 하나.</summary>
public sealed class NotifyRecipientDto
{
    public string UserId { get; set; } = string.Empty;

    public string? UserName { get; set; }

    public string RoleText { get; set; } = string.Empty;

    public string? Email { get; set; }

    public bool HasDevice { get; set; }

    public bool PushAllowed { get; set; }

    public bool EmailAllowed { get; set; }

    public bool PushOptedOut { get; set; }

    public bool EmailOptedOut { get; set; }

    /// <summary>
    /// 앱 푸시가 <b>실제로 닿나</b>. 정책 · 본인 설정 · 받을 자리 셋이 다 서야 한다.
    /// </summary>
    public bool PushReaches => PushAllowed && !PushOptedOut && HasDevice;

    /// <summary>이메일이 <b>실제로 나가나</b>.</summary>
    public bool EmailReaches => EmailAllowed && !EmailOptedOut && !string.IsNullOrWhiteSpace(Email);

    /// <summary>
    /// 못 닿는 까닭 한 줄. <b>고칠 자리가 다른 셋을 갈라서 적는다</b> —
    /// 알림관리 · 그 사람의 환경설정 · 계정 관리.
    /// </summary>
    public string Why
    {
        get
        {
            if (PushReaches || EmailReaches) return string.Empty;

            if (!PushAllowed && !EmailAllowed) return "정책에서 이 역할에 길이 안 열려 있습니다.";

            var parts = new List<string>();

            if (PushAllowed && PushOptedOut) parts.Add("본인이 앱 푸시를 껐습니다");
            else if (PushAllowed && !HasDevice) parts.Add("구독한 기기가 없습니다");

            if (EmailAllowed && EmailOptedOut) parts.Add("본인이 업무 메일을 껐습니다");
            else if (EmailAllowed && string.IsNullOrWhiteSpace(Email)) parts.Add("계정에 이메일이 없습니다");

            return string.Join(" · ", parts);
        }
    }
}

/// <summary>고를 수 있는 역할 한 줄.</summary>
public sealed class NotifyRoleDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int AccountCount { get; set; }
}

/// <summary>
/// 이벤트의 대상 성격. <b>정책이 하는 일이 이 값에 따라 갈린다.</b>
/// </summary>
/// <remarks>
/// 글자를 코드에 적는 것이 아니라 <b>옮기기만</b> 한다 — 정본은 DB 의
/// <c>scom.notification_events.target_kind</c> 다.
/// </remarks>
public static class NotifyTargetKinds
{
    public const string Role = "ROLE";

    public const string User = "USER";

    /// <summary>사람이 읽는 글자. 모르는 값은 그대로 보여 준다.</summary>
    public static string Describe(string? kind) => kind switch
    {
        Role => "역할로 감",
        User => "당사자에게 감",
        _ => kind ?? string.Empty,
    };

    /// <summary>
    /// 정책이 그 이벤트에서 무슨 일을 하는지 한 줄. <b>화면이 이 말을
    /// 반드시 해야 한다</b> — 같은 체크 칸이 이벤트에 따라 「받는 사람을
    /// 정한다」와 「거른다」로 뜻이 갈린다.
    /// </summary>
    public static string Effect(string? kind) => kind switch
    {
        Role => "고른 역할에 걸린 사람에게 보냅니다. 설정 파일에 적힌 역할 대신 이 목록이 쓰입니다.",
        User => "알림을 받을 당사자가 고른 역할 중 하나라도 가지고 있어야 보냅니다.",
        _ => string.Empty,
    };
}
