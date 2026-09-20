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
