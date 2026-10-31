namespace NotificationServer.DTOs;

/// <summary>알림관리 화면의 한 이벤트. 이벤트와 정책을 한 덩어리로 내려보낸다 — 고를 때마다 왕복이 붙지 않게.</summary>
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

    /// <summary>메일을 푸시 경로가 함께 내나. 참이면 앱 푸시와 같은 내용이 메일로 가고, 거짓이면 보내는 쪽이 제 틀로 따로 낸다.</summary>
    public bool EmailFromPush { get; set; }

    /// <summary>거짓이면 설정을 받아 두되 발송에는 아직 안 걸린다.</summary>
    public bool Governed { get; set; }

    /// <summary>받는 사람의 정본이 이 표인가. 참이면 역할을 하나도 안 걸었을 때 아무에게도 안 간다(제한 없음이 아니다).</summary>
    public bool PolicyRequired { get; set; }

    /// <summary>끄면 이 이벤트의 알림이 아무에게도 안 간다.</summary>
    public bool IsActive { get; set; }

    public int OrderNo { get; set; }

    /// <summary>이 이벤트에 매달린 역할 줄들.</summary>
    public List<NotificationPolicyDto> Policies { get; set; } = [];

    /// <summary>지금 제한이 없는가. 켜진 줄이 하나도 없으면 참이고 지금까지처럼 나간다. 역할 0개를 「아무도 못 받는다」로 읽는 것이 가장 흔한 오해라 서버가 셈해 내려보낸다.</summary>
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

    /// <summary>그 역할에 걸린 사람 수. 골랐는데 아무에게도 안 간다를 저장 전에 알아채게 함께 보낸다.</summary>
    public int AccountCount { get; set; }
}

/// <summary>이벤트 하나의 정책을 통째로 바꾼다. 이벤트 단위로 받아야 중간에 끊겨도 반쯤 적용된 정책이 안 남는다.
/// Policies 에 없는 역할 줄은 지운다(체크를 푼 역할을 남기면 화면과 표가 어긋난다).</summary>
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

/// <summary>「지금 이 설정이면 누구에게 가나」. 역할을 골랐는데 아무에게도 안 가는 자리가 셋이고 고치는 곳이 다르다(역할에 사람 없음, 이메일 없음, 본인이 끔).</summary>
public class NotificationPolicyPreviewDto
{
    public string EventCode { get; set; } = string.Empty;

    /// <summary>제한이 없으면 참. 그때 <see cref="Recipients"/> 는 비어 있다.</summary>
    public bool Unrestricted { get; set; }

    /// <summary>이벤트가 꺼져 있으면 참. 그때는 아무에게도 안 간다.</summary>
    public bool EventDisabled { get; set; }

    /// <summary>받는 사람의 정본이 이 표인가. Unrestricted 와 같이 참이면 지금처럼 나가는 것이 아니라 한 통도 안 나간다 — 창에 띄우는 말이 정반대라 갈라 보낸다.</summary>
    public bool PolicyRequired { get; set; }

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

/// <summary>미리받아보기 요청. 거짓이면 누른 사람 본인에게만, 참이면 저장된 정책의 대상자 모두에게 보낸다.</summary>
public class NotificationPolicyPreviewSendDto
{
    public bool ToTargets { get; set; }
}

/// <summary>미리받아보기 결과. 「보냈다」를 길마다 갈라 센다 — 푸시는 기기 수, 메일은 주소 수다.</summary>
public class NotificationPolicyPreviewSendResultDto
{
    /// <summary>앱 푸시를 보내려 한 사람 수.</summary>
    public int PushTargets { get; set; }

    /// <summary>푸시 서비스가 받아 준 기기 수.</summary>
    public int PushSent { get; set; }

    /// <summary>푸시 쪽에서 덧붙일 말(구독 없음 · 본인이 끔 · VAPID 없음).</summary>
    public string? PushNote { get; set; }

    /// <summary>메일을 보내려 한 주소 수.</summary>
    public int MailTargets { get; set; }

    public int Mailed { get; set; }

    public int MailFailed { get; set; }

    /// <summary>화면에 띄울 한 줄.</summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>내가 받을 수 있는 알림 하나와 그것을 길마다 받을지(본인 설정). 「내 알림 설정」 화면이 줄 하나로 그린다.</summary>
public class MyNotificationEventDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>ROLE(역할로 오는 업무 알림) · USER(나를 짚어 오는 알림).</summary>
    public string TargetKind { get; set; } = string.Empty;

    /// <summary>알림관리가 이 사람에게 앱 푸시를 허락했나. 거짓이면 스위치를 켜도 안 오므로 화면이 잠근다.</summary>
    public bool PushAllowed { get; set; }

    public bool EmailAllowed { get; set; }

    /// <summary>본인 설정. 줄이 없으면 참이다(쪽지 메일만 기본이 꺼짐 — 그 스위치를 그대로 옮긴다).</summary>
    public bool PushEnabled { get; set; }

    public bool EmailEnabled { get; set; }
}

/// <summary>이벤트 하나의 본인 설정을 바꾼다. 비운 길은 건드리지 않는다.</summary>
public class SaveMyNotificationEventDto
{
    public bool? PushEnabled { get; set; }
    public bool? EmailEnabled { get; set; }
}
