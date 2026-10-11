namespace NotificationServer.DTOs;

/// <summary>
/// 「알림관리」 화면의 <b>한 이벤트</b> — 무엇이 언제 나가는 알림이고, 지금
/// 어느 역할이 어느 길로 받게 돼 있나.
/// </summary>
/// <remarks>
/// 이벤트와 정책을 한 덩어리로 내려보낸다. 화면이 왼쪽에서 이벤트를 고르면
/// 오른쪽에 역할 줄이 바로 서야 하는데, 나눠 주면 고를 때마다 왕복이 하나
/// 붙는다 — 이벤트가 열셋이고 역할이 열여섯이라 전부 합쳐도 작다.
/// </remarks>
public class NotificationEventDto
{
    /// <summary>이벤트 코드. 보내는 쪽이 적어 보내는 글자다.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>알림구분 코드값. 없을 수 있다(보고서 메일·문의 접수).</summary>
    public string? Category { get; set; }

    /// <summary>알림구분의 사람이 읽는 이름. 공통코드에서 풀어 준다.</summary>
    public string? CategoryName { get; set; }

    /// <summary>어느 서비스가 보내나.</summary>
    public string? Source { get; set; }

    /// <summary><c>ROLE</c> · <c>USER</c>. 정책이 하는 일이 이 값에 따라 갈린다.</summary>
    public string TargetKind { get; set; } = "ROLE";

    public bool SupportsPush { get; set; }

    public bool SupportsEmail { get; set; }

    /// <summary>거짓이면 설정을 받아 두되 발송에는 아직 안 걸린다.</summary>
    public bool Governed { get; set; }

    /// <summary>끄면 이 이벤트의 알림이 아무에게도 안 간다.</summary>
    public bool IsActive { get; set; }

    public int OrderNo { get; set; }

    /// <summary>이 이벤트에 매달린 역할 줄들.</summary>
    public List<NotificationPolicyDto> Policies { get; set; } = [];

    /// <summary>
    /// 지금 <b>제한이 없는가</b>. 켜진 줄이 하나도 없으면 참이고, 그때는
    /// 알림이 지금까지와 똑같이 나간다.
    /// </summary>
    /// <remarks>
    /// 화면이 이 값으로 「제한 없음」 띠를 띄운다. <b>역할 0 개를 「아무도 못
    /// 받는다」로 읽는 것이 가장 흔한 오해</b>라, 서버가 셈해서 내려보낸다.
    /// </remarks>
    public bool Unrestricted { get; set; }

    /// <summary>마지막으로 정책을 고친 때(UTC). 줄이 없으면 <c>null</c>.</summary>
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>이벤트 하나에 매달린 역할 한 줄.</summary>
public class NotificationPolicyDto
{
    public string RoleId { get; set; } = string.Empty;

    /// <summary>역할 이름. 지워진 역할이면 비어 있을 수 있다.</summary>
    public string? RoleName { get; set; }

    public bool PushEnabled { get; set; }

    public bool EmailEnabled { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 그 역할에 걸린 <b>사람 수</b>. 「골랐는데 아무에게도 안 간다」를 저장하기
    /// 전에 알아채라고 함께 보낸다 — 보고서 메일의 「받는 사람」 창과 같은 뜻이다.
    /// </summary>
    public int AccountCount { get; set; }
}

/// <summary>이벤트 하나의 정책을 통째로 바꾼다.</summary>
/// <remarks>
/// <para>
/// <b>줄 단위가 아니라 이벤트 단위로 받는다.</b> 화면이 체크 칸을 여럿 만지고
/// 한 번에 저장하는 꼴이라, 줄마다 보내면 중간에 끊겼을 때 반쯤 적용된 정책이
/// 남는다. 통째로 받으면 「보낸 것이 곧 전부」다.
/// </para>
/// <para>
/// <see cref="Policies"/> 에 없는 역할 줄은 <b>지운다</b>. 체크를 푼 역할을
/// 남겨 두면 화면과 표가 어긋난다.
/// </para>
/// </remarks>
public class SaveNotificationPolicyDto
{
    /// <summary>이벤트를 쓰나. 끄면 그 이벤트의 알림이 아무에게도 안 간다.</summary>
    public bool IsActive { get; set; } = true;

    public List<SaveNotificationPolicyRowDto> Policies { get; set; } = [];
}

/// <summary>저장할 역할 한 줄.</summary>
public class SaveNotificationPolicyRowDto
{
    public string RoleId { get; set; } = string.Empty;

    public bool PushEnabled { get; set; }

    public bool EmailEnabled { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// 「지금 이 설정이면 누구에게 가나」 — 저장 전에 눌러 보는 자리.
/// </summary>
/// <remarks>
/// 「역할을 골랐는데 아무에게도 안 간다」가 조용히 일어나는 자리가 셋이고
/// <b>고치는 자리가 서로 다르다</b> — 역할에 사람이 없거나, 계정에 이메일이
/// 없거나, 본인이 알림을 꺼 두었거나. 셋을 갈라서 보여 준다.
/// </remarks>
public class NotificationPolicyPreviewDto
{
    public string EventCode { get; set; } = string.Empty;

    /// <summary>제한이 없으면 참. 그때 <see cref="Recipients"/> 는 비어 있다.</summary>
    public bool Unrestricted { get; set; }

    /// <summary>이벤트가 꺼져 있으면 참. 그때는 아무에게도 안 간다.</summary>
    public bool EventDisabled { get; set; }

    public List<NotificationPolicyRecipientDto> Recipients { get; set; } = [];

    /// <summary>앱 푸시가 실제로 닿을 사람 수(구독한 기기가 있고 안 꺼 둔 사람).</summary>
    public int PushReachable { get; set; }

    /// <summary>이메일이 실제로 나갈 사람 수(주소가 있고 안 꺼 둔 사람).</summary>
    public int EmailReachable { get; set; }
}

/// <summary>미리보기 한 줄 — 사람 하나.</summary>
public class NotificationPolicyRecipientDto
{
    public string UserId { get; set; } = string.Empty;

    public string? UserName { get; set; }

    /// <summary>이 사람이 이 이벤트에 걸린 역할들. 쉼표로 이어 보여 준다.</summary>
    public string RoleText { get; set; } = string.Empty;

    public string? Email { get; set; }

    /// <summary>구독한 기기가 있나. 없으면 푸시를 보내도 닿지 않는다.</summary>
    public bool HasDevice { get; set; }

    /// <summary>정책이 이 사람에게 앱 푸시를 허용하나.</summary>
    public bool PushAllowed { get; set; }

    /// <summary>정책이 이 사람에게 이메일을 허용하나.</summary>
    public bool EmailAllowed { get; set; }

    /// <summary>본인이 앱 푸시를 꺼 두었나(사용자 환경설정).</summary>
    public bool PushOptedOut { get; set; }

    /// <summary>본인이 업무 메일을 꺼 두었나(사용자 환경설정).</summary>
    public bool EmailOptedOut { get; set; }
}

/// <summary>고를 수 있는 역할 한 줄.</summary>
public class NotificationRoleDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>그 역할에 걸린 사람 수.</summary>
    public int AccountCount { get; set; }
}
