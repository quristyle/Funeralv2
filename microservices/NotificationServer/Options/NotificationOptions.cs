namespace NotificationServer.Options;

/// <summary>
/// VAPID (Web Push) 키 설정.
/// </summary>
/// <remarks>
/// <b>이 키가 한 곳에 모이는 것이 이 서비스를 만든 이유 중 하나다</b> (결정 D8-A).
/// 예전에는 <c>funeralv2Api</c> 와 <c>HelpDeskServer</c> 두 곳(파일로는 셋)에 같은 값이
/// 평문으로 박혀 있었다. 두 서비스가 각각 푸시를 보내야 했기 때문이다.
///
/// <para>
/// <b>공개 키는 비밀이 아니다.</b> 브라우저가 구독을 만들 때 쓰는 값이라 화면에 내려간다.
/// 개인 키만 <c>appsettings.Local.json</c> (git 제외) 에 둔다.
/// </para>
///
/// <para>
/// <b>키를 바꾸면 기존 구독이 전부 끊긴다.</b> 브라우저는 구독을 만들 때의 공개 키에
/// 묶여 있어서, 키를 갈면 모든 사용자가 다시 구독해야 한다. 그래서 이 작업에서는
/// 값을 옮기기만 하고 **교체하지 않았다.**
/// </para>
/// </remarks>
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

/// <summary>
/// 이메일 발송 설정.
/// </summary>
/// <remarks>
/// <b>이 시스템은 C# 에서 SMTP 로 직접 보내지 않는다.</b> 저장소 어디에도 SMTP 설정이 없다.
/// 헬프데스크가 하던 방식은 이렇다.
///
/// <list type="number">
///   <item><description>메일 내용을 JSON 파일로 떨어뜨린다 (<see cref="SpoolPath"/>)</description></item>
///   <item><description>"이 스크립트를 돌려 달라" 를 큐에 넣는다 (<see cref="QueueName"/>)</description></item>
///   <item><description>배포 장비의 소비자가 그 스크립트를 실행해 실제로 보낸다</description></item>
/// </list>
///
/// <para>
/// 그 방식을 그대로 옮겼다. SMTP 로 바꾸는 것은 계정·자격증명이 필요한 별개의 결정이다.
/// 큐는 배포 도구가 쓰는 것과 같은 <c>run_script</c> 다(28-release-tool.md 의 D-R1 참고).
/// </para>
/// </remarks>
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

/// <summary>
/// 배포 알림 설정 — 배포 파이프라인이 「반영 끝」을 알려 올 때 쓴다.
/// </summary>
/// <remarks>
/// <para>
/// 부르는 쪽은 GitHub Actions 의 <c>deploy</c> 잡이다. 그 러너에게는 <b>계정이 없다</b> —
/// 사람이 로그인해 받는 토큰을 워크플로에 넣어 둘 수는 없으므로, 배포 보고
/// (<c>X-Release-Token</c>, AuthServer 의 ReleaseEndpoints)와 같은 방식으로
/// <b>공유 비밀 하나</b>로 인증한다.
/// </para>
/// <para>
/// <b>값이 없으면 엔드포인트가 통째로 닫힌다.</b> 비어 있을 때 "인증을 건너뛴다" 로
/// 동작하면 설정을 잊은 장비에서 누구나 슈퍼관리자에게 알림을 보낼 수 있게 된다 —
/// 그 실수는 조용하고, 그래서 위험하다.
/// </para>
/// </remarks>
public sealed class DeployNotifyOptions
{
    public const string SectionName = "DeployNotify";

    /// <summary>
    /// 배포 파이프라인이 <c>X-Deploy-Token</c> 헤더에 담아 보내는 공유 비밀.
    /// <c>appsettings.Local.json</c> 이나 환경변수(<c>DeployNotify__Token</c>)에만 둔다.
    /// </summary>
    public string? Token { get; set; }

    /// <summary>알림을 눌렀을 때 열 화면. 배포 현황(포털관리 &gt; 상태관리)이다.</summary>
    public string ClickUrl { get; set; } = "/admin/status/deploy";

    /// <summary>
    /// 알림을 받을 역할. 기본은 슈퍼관리자다.
    /// </summary>
    /// <remarks>
    /// 역할 이름을 코드에 박지 않는 이유: 운영이 「배포 알림은 일반 관리자도 받게 하자」로
    /// 바뀌는 날 재배포가 필요해진다. 그런데 그 재배포야말로 이 알림의 대상이다.
    /// </remarks>
    public string RoleId { get; set; } = "SYSTEM_ADMINISTRATOR";

    /// <summary>토큰이 실제 값으로 채워져 있나. 자리표시자(<c>__SET_IN_…</c>)는 없는 것으로 본다.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Token) && !Token.StartsWith("__", StringComparison.Ordinal);
}

/// <summary>
/// 푸시를 <b>언제까지 배달할 것인가</b>를 정하는 설정.
/// </summary>
/// <remarks>
/// <para>
/// [왜 필요한가 — 오래 안 쓰다 켜면 알림이 한꺼번에 쏟아진다]
/// </para>
/// <para>
/// 웹푸시는 서버가 브라우저로 바로 꽂는 것이 아니다. 우리가 보내는 곳은 브라우저
/// 제조사의 푸시 서비스(크롬이면 FCM, 파이어폭스면 Mozilla autopush)이고, 그쪽이
/// <b>브라우저와의 연결이 살아날 때까지 들고 기다린다.</b> 브라우저를 며칠 닫아 두면
/// 그동안의 알림이 거기 줄을 서 있다가 다시 켜는 순간 <b>한 번에 전부</b> 내려온다.
/// </para>
/// <para>
/// 줄을 못 서게 하는 손잡이가 규격(RFC 8030)에 둘 있다.
/// <list type="bullet">
///   <item><description>
///     <b>TTL</b> — 이 시간이 지나면 푸시 서비스가 <b>버린다</b>. 받는 사람에게
///     닿지 않고 사라지므로, 지나고 나면 의미 없는 알림(날씨·배포)은 짧을수록 좋다.
///     라이브러리 기본값은 <b>28일</b>이라 사실상 「영원히 들고 있어라」에 가깝다 —
///     지금 겪는 홍수의 직접적인 원인이다.
///   </description></item>
///   <item><description>
///     <b>Topic</b> — 같은 값으로 보낸 알림은 <b>줄 안에서 앞의 것을 밀어낸다.</b>
///     같은 종류가 스무 건 밀려 있어도 최신 한 건만 남는다.
///   </description></item>
/// </list>
/// </para>
/// <para>
/// <b>버려도 되는 까닭</b>은 이 포털에 「내 알림함」(<c>/admin/push/history</c>)이
/// 있기 때문이다. 푸시는 <b>지금 알려 주는 길</b>일 뿐이고 기록은 알림함에 남는다 —
/// 반나절 지난 알림을 굳이 배달해 봐야 알림함에 이미 있는 것을 창으로 한 번 더
/// 보는 것이고, 그것이 스무 개면 그냥 소음이다.
/// </para>
/// </remarks>
public sealed class PushDeliveryOptions
{
    public const string SectionName = "Push";

    /// <summary>
    /// 보내는 쪽이 따로 정하지 않았을 때 쓰는 수명(초). 기본 6시간.
    /// </summary>
    /// <remarks>
    /// 반나절을 고른 까닭: 아침에 온 쪽지를 점심에 열어도 받아야 하지만, 어제 것까지
    /// 받을 필요는 없다(알림함에 있다). 값을 0 이하로 두면 <see cref="FallbackTtlSeconds"/>
    /// 로 되돌린다 — 0 은 「닿지 않으면 즉시 버려라」라서 설정 실수로 그 값이 들어가면
    /// 알림이 통째로 사라진다.
    /// </remarks>
    public int DefaultTtlSeconds { get; set; } = FallbackTtlSeconds;

    /// <summary>설정이 비었거나 말이 안 될 때 쓰는 값(6시간).</summary>
    public const int FallbackTtlSeconds = 21600;

    /// <summary>보내는 쪽이 아무리 길게 잡아도 이보다는 짧게 자른다(기본 1일).</summary>
    public int MaxTtlSeconds { get; set; } = 86400;

    /// <summary>
    /// 긴급도(RFC 8030 <c>Urgency</c>). <c>very-low · low · normal · high</c>.
    /// </summary>
    /// <remarks>
    /// 안드로이드는 화면이 꺼진 기기를 절전(Doze)에 넣고 그 안에서는 낮은 긴급도의
    /// 푸시를 <b>모아 두었다가 기기가 깰 때</b> 함께 내보낸다. 이것도 「한꺼번에」의
    /// 한 갈래다. 우리 알림은 사람이 바로 봐야 하는 것들이라 <c>high</c> 로 보낸다.
    /// </remarks>
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
