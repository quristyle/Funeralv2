namespace AiTaskRunner;

/// <summary>
/// 실행기 설정. <c>appsettings.json</c> 의 <c>Runner</c> 절.
/// </summary>
public sealed class RunnerOptions
{
    /// <summary>이 장비의 이름. 로그와 서버 기록에 남는다.</summary>
    public string Name { get; set; } = Environment.MachineName;

    /// <summary>
    /// 서버 주소. <b>게이트웨이가 아니라 서비스를 직접 부른다</b> —
    /// 실행기 경로는 게이트웨이에 열지 않는다(설계 9.8).
    /// </summary>
    public string ServerUrl { get; set; } = "http://127.0.0.1:5450";

    /// <summary>
    /// 장비 토큰. 집어갈 때 한 번 쓴다. <b>없으면 서버가 거절한다.</b>
    /// </summary>
    public string RunnerToken { get; set; } = string.Empty;

    /// <summary>이 장비가 돌릴 수 있는 CLI.</summary>
    public string[] Kinds { get; set; } = ["claude"];

    /// <summary>
    /// 동시에 몇 건까지. <b>게이트(빌드·테스트)는 이와 별개로 1건이다</b> —
    /// CPU 를 실제로 먹는 구간이 거기뿐이라(설계 9.4).
    /// </summary>
    public int MaxParallel { get; set; } = 5;

    /// <summary>작업공간을 만들 뿌리.</summary>
    public string WorkspaceRoot { get; set; } = "/home/lee/ai-workspaces";

    /// <summary>
    /// 안전망 조회 주기(초).
    /// </summary>
    /// <remarks>
    /// <b>큐를 붙였다고 이것을 빼지 않는다.</b> 이 기능에서 가장 나쁜 실패는
    /// 「요청」인 채로 아무도 모르게 남는 것이고, 메시지는 잃을 수 있다
    /// (브로커 재시작 · ack 직후 죽음 · 넣기 실패). 1분에 한 번 도는 조회
    /// 하나로 그 실패가 전부 「조금 늦게 돌았다」가 된다.
    /// </remarks>
    public int PollSeconds { get; set; } = 60;

    /// <summary>하트비트 주기(초). 서버의 임대 기간보다 넉넉히 짧아야 한다.</summary>
    public int HeartbeatSeconds { get; set; } = 15;

    /// <summary>로그를 모아 보내는 기준 — 줄 수와 시간 중 먼저 오는 쪽.</summary>
    public int FlushLines { get; set; } = 40;

    public int FlushSeconds { get; set; } = 2;

    /// <summary>
    /// 운영 <c>TAG</c> 를 읽을 파일. push 직전에 그 값을 적어 둔다.
    /// </summary>
    /// <remarks>
    /// <b>되돌리기는 이 값을 이전 것으로 돌리는 일</b>이라, 아무도 안 적어
    /// 두면 되돌릴 수가 없다(설계 9.3). 안에 <c>TAG</c> 한 줄뿐이라
    /// 비밀값이 아니고, 그래서 담장에서도 막지 않는다.
    /// </remarks>
    public string EnvFile { get; set; } = "/srv/jsini/.env";

    /// <summary>
    /// push 게이트가 빌드에 쓸 <c>dotnet</c>.
    /// </summary>
    /// <remarks>
    /// <b>이름만 적으면 안 되는 장비가 있다.</b> 운영 서버에는 시스템 dotnet 이
    /// 8.0 으로 깔려 있고 이 저장소는 net10.0 이라, <c>PATH</c> 로 찾으면
    /// 8.0 이 잡혀 복원부터 실패한다. CLI 실행 파일을 절대 경로로 적는 것과
    /// 같은 이유다.
    /// </remarks>
    public string DotnetPath { get; set; } = "dotnet";

    /// <summary>
    /// 작업공간을 며칠 두나.
    /// </summary>
    /// <remarks>
    /// 복사본은 끝나도 지우지 않으므로(그 폴더가 결과의 전부다) 쌓인다.
    /// <b>고칠 것이 남아 있는 자리를 지우는 일</b>이라 넉넉히 둔다.
    /// </remarks>
    public int KeepWorkspaceDays { get; set; } = 14;

    public QueueOptions Queue { get; set; } = new();

    /// <summary>CLI 어댑터. <b>플래그는 코드가 아니라 여기에 적는다.</b></summary>
    public Dictionary<string, AdapterOptions> Adapters { get; set; } = [];
}

public sealed class QueueOptions
{
    public string Host { get; set; } = "localhost";

    public string Name { get; set; } = "ai_task";

    /// <summary>
    /// <b>넣는 쪽과 같아야 한다.</b> 다르면 브로커가 <c>PRECONDITION_FAILED</c> 를 낸다.
    /// </summary>
    public bool Durable { get; set; } = true;

    /// <summary>큐를 못 붙어도 실행기는 돈다 — 안전망 조회가 있다.</summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// CLI 하나를 어떻게 띄우나.
/// </summary>
/// <remarks>
/// <para>
/// <b>실행 파일은 절대 경로로 적는다.</b> systemd 로 도는 프로세스의
/// <c>PATH</c> 에는 <c>~/.local/bin</c> 이 없을 수 있다 — 로그인 셸에서만
/// 보이던 자리다(실제로 한 번 빈손이었다).
/// </para>
/// </remarks>
public sealed class AdapterOptions
{
    public string Executable { get; set; } = string.Empty;

    /// <summary>프롬프트를 뺀 나머지 인자.</summary>
    public string[] Args { get; set; } = [];

    /// <summary>
    /// 작업 폴더를 알려 주는 인자. <c>{path}</c> 가 작업공간 경로로 바뀐다.
    /// </summary>
    /// <remarks>
    /// <b>작업 디렉터리(cwd)만으로는 모자란 CLI 가 있다.</b> <c>agy</c> 는
    /// 자기 「워크스페이스」 개념을 따로 갖고 있어서, cwd 를 맞춰 줘도
    /// <i>「현재 활성 워크스페이스가 없어서 파일을 찾지 못했습니다」</i> 로
    /// 끝난다 — 실제로 그렇게 한 번 헛돌았다. 그 CLI 에는
    /// <c>["--add-dir", "{path}"]</c> 를 준다.
    /// <para><c>claude</c> 는 cwd 를 그대로 쓰므로 비워 둔다.</para>
    /// </remarks>
    public string[] WorkspaceArgs { get; set; } = [];

    /// <summary>
    /// 지시문을 어떻게 주나 — <c>stdin</c> · <c>arg</c>.
    /// </summary>
    /// <remarks>
    /// 확인한 것: <c>claude</c> 는 stdin 으로 들어가고, <c>agy</c> 는
    /// <c>-p</c> 가 값을 요구해서 인자로만 들어간다(설계 7.4).
    /// </remarks>
    public string PromptVia { get; set; } = "stdin";

    /// <summary><c>arg</c> 일 때 프롬프트 앞에 붙일 것 — 예: <c>-p=</c>.</summary>
    public string PromptArgPrefix { get; set; } = string.Empty;

    /// <summary>
    /// 인자로 줄 수 있는 지시문의 길이 상한(바이트).
    /// </summary>
    /// <remarks>
    /// 리눅스는 <b>인자 하나</b>가 128KB(<c>MAX_ARG_STRLEN</c>)를 못 넘는다.
    /// 전체 argv 2MB 와는 다른 값이고, 긴 지시문 하나면 이쪽에 먼저 걸린다.
    /// 절반에서 갈라 여유를 둔다.
    /// </remarks>
    public int PromptMaxBytes { get; set; } = 61440;

    /// <summary>
    /// 지시문이 너무 길 때 대신 보낼 한 줄. <c>{path}</c> 가 파일 경로로 바뀐다.
    /// </summary>
    public string PromptFileFallback { get; set; } = "{path} 를 읽고 그대로 수행하라.";

    public int TimeoutMinutes { get; set; } = 30;

    /// <summary>
    /// 결과문(<c>result_text</c>)을 어디서 가려내나 — <c>stream-json</c> · <c>tail</c>.
    /// </summary>
    public string ResultFrom { get; set; } = "tail";
}
