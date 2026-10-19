namespace NotificationServer.Options;

/// <summary>VAPID(Web Push) 키 설정. 키를 바꾸면 기존 구독이 전부 끊기므로 교체하지 않는다. 개인 키는 appsettings.Local.json(git 제외)에만 둔다.</summary>
public sealed class VapidOptions
{
    /// <summary>보통 <c>mailto:</c> 주소나 도메인 URL. 푸시 서비스가 문제 시 연락할 곳이다.</summary>
    public string? Subject { get; set; }

    /// <summary>브라우저가 구독을 만들 때 쓰는 값. 비밀이 아니다.</summary>
    public string? PublicKey { get; set; }

    /// <summary>발송 요청에 서명하는 키. 비밀이다.</summary>
    public string? PrivateKey { get; set; }

    /// <summary>셋이 모두 채워져 있나. 하나라도 비면 푸시를 보낼 수 없다.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Subject) &&
        !string.IsNullOrWhiteSpace(PublicKey) &&
        !string.IsNullOrWhiteSpace(PrivateKey);
}

/// <summary>이메일 발송 설정. SMTP 를 직접 쓰지 않는다 — 메일을 JSON 으로 떨어뜨리고 run_script 큐에 넣으면 배포 장비의 소비자가 보낸다.</summary>
public sealed class EmailQueueOptions
{
    /// <summary>메시지 큐 호스트. 큐 소비자가 도는 장비다.</summary>
    public string HostName { get; set; } = "localhost";

    /// <summary>스크립트 실행 요청을 넣을 큐 이름.</summary>
    public string QueueName { get; set; } = "run_script";

    /// <summary>메일 내용 JSON 을 떨어뜨릴 디렉터리.</summary>
    public string SpoolPath { get; set; } = "/home/lee/projects/msgQ";

    /// <summary>큐 소비자가 실행할 메일 발송 스크립트의 절대 경로.</summary>
    public string ScriptPath { get; set; } = "/home/lee/projects/wrkScripts/wrkReceptMail.sh";

    /// <summary>설정이 갖춰졌나.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(SpoolPath) &&
        !string.IsNullOrWhiteSpace(ScriptPath) &&
        !string.IsNullOrWhiteSpace(QueueName);
}

/// <summary>배포 알림 설정. 부르는 쪽은 계정 없는 GitHub Actions 라 공유 비밀로 인증한다. 값이 없으면 엔드포인트를 닫는다(열면 누구나 슈퍼관리자에게 알림을 보낼 수 있다).</summary>
public sealed class DeployNotifyOptions
{
    public const string SectionName = "DeployNotify";

    /// <summary>배포 파이프라인이 X-Deploy-Token 헤더로 보내는 공유 비밀. appsettings.Local.json 이나 환경변수(DeployNotify__Token)에만 둔다.</summary>
    public string? Token { get; set; }

    /// <summary>알림을 눌렀을 때 열 화면. 배포 현황(포털관리 &gt; 상태관리)이다.</summary>
    public string ClickUrl { get; set; } = "/admin/status/deploy";

    /// <summary>알림을 받을 역할. 기본은 슈퍼관리자다. 역할 이름은 코드에 박지 않는다.</summary>
    public string RoleId { get; set; } = "SYSTEM_ADMINISTRATOR";

    /// <summary>토큰이 실제 값으로 채워져 있나. 자리표시자(<c>__SET_IN_…</c>)는 없는 것으로 본다.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Token) && !Token.StartsWith("__", StringComparison.Ordinal);
}

/// <summary>푸시를 언제까지 배달할지 정하는 설정. 푸시 서비스가 기기 연결을 기다리며 쌓아 두므로 TTL 과 Topic 으로 줄을 막는다.</summary>
public sealed class PushDeliveryOptions
{
    public const string SectionName = "Push";

    /// <summary>보내는 쪽이 정하지 않았을 때 쓰는 수명(초). 기본 6시간. 0 이하면 FallbackTtlSeconds 로 되돌린다 — 0 은 즉시 버리라는 뜻이라 알림이 사라진다.</summary>
    public int DefaultTtlSeconds { get; set; } = FallbackTtlSeconds;

    /// <summary>설정이 비었거나 말이 안 될 때 쓰는 값(6시간).</summary>
    public const int FallbackTtlSeconds = 21600;

    /// <summary>보내는 쪽이 아무리 길게 잡아도 이보다는 짧게 자른다(기본 1일).</summary>
    public int MaxTtlSeconds { get; set; } = 86400;

    /// <summary>긴급도(RFC 8030 Urgency). 안드로이드 절전이 낮은 긴급도의 푸시를 모아 두므로 우리 알림은 high 로 보낸다.</summary>
    public string Urgency { get; set; } = "high";

    /// <summary>실제로 쓸 기본 수명. 설정이 비거나 범위를 벗어나면 되돌린다.</summary>
    public int ResolveDefaultTtl() =>
        DefaultTtlSeconds > 0 && DefaultTtlSeconds <= MaxTtl()
            ? DefaultTtlSeconds
            : Math.Min(FallbackTtlSeconds, MaxTtl());

    /// <summary>실제로 쓸 상한. 설정이 말이 안 되면 하루로 본다.</summary>
    public int MaxTtl() => MaxTtlSeconds > 0 ? MaxTtlSeconds : 86400;

    /// <summary>보내는 쪽이 준 값을 상한 안으로 자른다. 안 줬으면 기본값.</summary>
    public int ClampTtl(int? requested) =>
        requested is null or <= 0
            ? ResolveDefaultTtl()
            : Math.Min(requested.Value, MaxTtl());

    /// <summary>규격에 있는 값만 통과시킨다. 아무거나 보내면 푸시 서비스가 400 을 준다.</summary>
    public string ResolveUrgency() => Urgency?.Trim().ToLowerInvariant() switch
    {
        "very-low" => "very-low",
        "low" => "low",
        "normal" => "normal",
        _ => "high",
    };
}

/// <summary>쪽지 전환 메일 설정(NoteFallbackMail). 푸시는 기기가 꺼져 있으면 조용히 버려지므로, 닿지 않고 안 읽힌 쪽지를 메일로 돌린다. 메일 길은 잘못 열리면 되돌릴 수 없어 끄는 손잡이를 둔다.</summary>
public sealed class NoteFallbackMailOptions
{
    /// <summary>켜 둘까. 꺼 두면 배치가 아예 깨어나지 않는다.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>보낸 뒤 이만큼 기기에 안 닿고 안 읽었으면 메일로 돌린다(분, 기본 120). 푸시 수명이 지나면 영영 안 닿으므로 그보다 오래 기다릴 값어치가 없다.</summary>
    public int AfterMinutes { get; set; } = 120;

    /// <summary>이보다 오래된 쪽지는 건드리지 않는다(시간, 기본 72시간). 배치를 처음 켜거나 멈췄다 켤 때 쌓인 쪽지가 한꺼번에 메일로 나가는 것을 막는다.</summary>
    public int LookbackHours { get; set; } = 72;

    /// <summary>얼마나 자주 훑을까(분). 기본 5분.</summary>
    public int IntervalMinutes { get; set; } = 5;

    /// <summary>한 번에 보낼 상한(기본 50통). 넘치면 남은 것은 다음 차례가 집는다 — 떨어뜨리지 않는다.</summary>
    public int MaxPerRun { get; set; } = 50;
}
