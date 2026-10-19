namespace AiTaskRunner;

/// <summary>실행기 설정. appsettings.json 의 Runner 절.</summary>
public sealed class RunnerOptions
{
    /// <summary>이 장비의 이름. 로그와 서버 기록에 남는다.</summary>
    public string Name { get; set; } = Environment.MachineName;

    /// <summary>서버 주소. 게이트웨이가 아니라 서비스를 직접 부른다 — 실행기 경로는 게이트웨이에 열지 않는다.</summary>
    public string ServerUrl { get; set; } = "http://127.0.0.1:5450";

    /// <summary>장비 토큰. 집어갈 때 쓰며, 없으면 서버가 거절한다.</summary>
    public string RunnerToken { get; set; } = string.Empty;

    /// <summary>이 장비가 돌릴 수 있는 CLI. 어댑터가 정본이라 따로 적는 배열을 두지 않는다 — 두 곳이 어긋나면 아무도 집지 않는 「대기」가 생긴다.
    /// 이 장비에서만 끄려면 그 어댑터의 Executable 을 빈 값으로 둔다(사전은 키로 겹쳐서 배열처럼 뒤섞이지 않는다).</summary>
    public IReadOnlyList<string> RunnableKinds =>
    [
        .. Adapters
            .Where(a => !string.IsNullOrWhiteSpace(a.Value.Executable))
            .Select(a => a.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase),
    ];

    /// <summary>동시에 몇 건까지. 게이트(빌드·테스트)는 이와 별개로 1건이다 — CPU 를 먹는 구간이 거기뿐이다.</summary>
    public int MaxParallel { get; set; } = 5;

    /// <summary>작업공간을 만들 뿌리.</summary>
    public string WorkspaceRoot { get; set; } = "/home/lee/ai-workspaces";

    /// <summary>안전망 조회 주기(초). 큐를 붙여도 빼지 않는다 — 메시지는 잃을 수 있고, 이 조회가 그 실패를 「조금 늦게 돌았다」로 바꾼다.</summary>
    public int PollSeconds { get; set; } = 60;

    /// <summary>하트비트 주기(초). 서버의 임대 기간보다 넉넉히 짧아야 한다.</summary>
    public int HeartbeatSeconds { get; set; } = 15;

    /// <summary>대상의 git 상태를 들여다보는 주기(초). 0 이면 안 본다. 화면의 「지금 확인」이 따로 15초 안에 받으므로 짧게 둘 이유가 없다.</summary>
    public int StatusSeconds { get; set; } = 180;

    /// <summary>처리 파일의 추정 줄을 실제 파일에 대 보는 주기(초). 0 이면 안 본다. 대 볼 것은 작업이 끝난 직후에만 생긴다.</summary>
    public int FileCheckSeconds { get; set; } = 45;

    /// <summary>로그를 모아 보내는 기준 — 줄 수와 시간 중 먼저 오는 쪽.</summary>
    public int FlushLines { get; set; } = 40;

    public int FlushSeconds { get; set; } = 2;

    /// <summary>운영 TAG 를 읽을 파일. push 직전에 그 값을 적어 되돌리기에 쓴다. 비밀값이 아니라 담장에서도 막지 않는다.</summary>
    public string EnvFile { get; set; } = "/srv/jsini/.env";

    /// <summary>push 게이트가 빌드에 쓸 dotnet 의 절대 경로. PATH 로 찾으면 운영 장비의 시스템 dotnet 8.0 이 잡혀 복원이 실패한다.</summary>
    public string DotnetPath { get; set; } = "dotnet";

    /// <summary>작업공간을 며칠 두나. 끝나도 지우지 않은 복사본이 결과의 전부라 넉넉히 둔다.</summary>
    public int KeepWorkspaceDays { get; set; } = 14;

    // ── 사용량 보고 ─────────────────────────────────────────

    /// <summary>AI CLI 의 /usage 를 읽어 올릴 것인가. 끄면 대시보드의 한도 칸이 통째로 빈다 — 서버는 이 값을 다른 데서 구할 수 없다.</summary>
    public bool UsageEnabled { get; set; } = true;

    /// <summary>사용량을 물어보는 주기(분). 짧게 두지 않는다 — 물어보는 것도 한도를 쓰는 CLI 호출이다.</summary>
    public int UsageIntervalMinutes { get; set; } = 15;

    public QueueOptions Queue { get; set; } = new();

    /// <summary>「한 줄 물어보기」 — <see cref="QuickAskWorker"/>.</summary>
    public QuickAskOptions QuickAsk { get; set; } = new();

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

/// <summary>CLI 하나를 띄우는 법. 실행 파일은 절대 경로로 적는다 — systemd 프로세스의 PATH 에는 ~/.local/bin 이 없을 수 있다.</summary>
public sealed class AdapterOptions
{
    public string Executable { get; set; } = string.Empty;

    /// <summary>프롬프트를 뺀 나머지 인자.</summary>
    public string[] Args { get; set; } = [];

    /// <summary>작업 폴더를 알려 주는 인자. {path} 가 작업공간 경로로 바뀐다. cwd 만으로 모자란 agy 는 이 인자가 있어야 파일을 찾는다. claude 는 cwd 를 그대로 쓰므로 비운다.</summary>
    public string[] WorkspaceArgs { get; set; } = [];

    /// <summary>지시문을 주는 길 — stdin 또는 arg. claude 는 stdin, agy 는 -p 가 값을 요구해 인자로만 받는다.</summary>
    public string PromptVia { get; set; } = "stdin";

    /// <summary><c>arg</c> 일 때 프롬프트 앞에 붙일 것 — 예: <c>-p=</c>.</summary>
    public string PromptArgPrefix { get; set; } = string.Empty;

    /// <summary>인자로 줄 수 있는 지시문 길이 상한(바이트). 리눅스는 인자 하나가 128KB 를 못 넘어 전체 argv 2MB 보다 먼저 걸린다. 절반에서 갈라 여유를 둔다.</summary>
    public int PromptMaxBytes { get; set; } = 61440;

    /// <summary>
    /// 지시문이 너무 길 때 대신 보낼 한 줄. <c>{path}</c> 가 파일 경로로 바뀐다.
    /// </summary>
    public string PromptFileFallback { get; set; } = "{path} 를 읽고 그대로 수행하라.";

    /// <summary>
    /// 건에 제한 시간이 안 실려 왔을 때 쓸 값(분). 서버 기본과 같은 <b>120</b> 이다.
    /// </summary>
    public int TimeoutMinutes { get; set; } = 120;

    /// <summary>
    /// 결과문(<c>result_text</c>)을 어디서 가려내나 — <c>stream-json</c> · <c>tail</c>.
    /// </summary>
    public string ResultFrom { get; set; } = "tail";

    /// <summary>이 CLI 에게 사용량을 물어보는 인자. 비면 안 물어본다 — 사용량을 말하지 않는 CLI 에 없는 명령을 부르면 15분마다 실패 줄이 쌓인다.</summary>
    public string[] UsageArgs { get; set; } = [];

    /// <summary>CLI 로 물을 수 없을 때 두드릴 HTTP GET 주소. copilot 의 /usage 는 대화 중에만 도는 명령이라 -p 로 주면 지시문으로 먹혀 모델이 지어낸 글이 한도로 앉는다. 둘 다 있으면 이쪽이 이긴다.</summary>
    public string UsageUrl { get; set; } = string.Empty;

    /// <summary>UsageUrl 을 부를 때 쓸 토큰이 든 JSON 파일. 토큰을 베껴 적지 않는다 — CLI 가 갱신하는 자리라 베껴 두면 재로그인 뒤 조용히 401 이 난다.</summary>
    public string UsageTokenFile { get; set; } = string.Empty;

    /// <summary>그 JSON 안에서 토큰이 있는 자리(점으로 잇는다). * 는 이름을 모르는 첫 칸이다(copilot 의 열쇠는 호스트:계정이라 이름을 적을 수 없다).</summary>
    public string UsageTokenPath { get; set; } = string.Empty;

    /// <summary>로그인 정보가 든 JSON(claude 형식). 토큰이 아니라 요금제 이름과 갱신 토큰의 만료 시각만 읽는다. 비면 안 읽는다.</summary>
    public string UsageAuthFile { get; set; } = string.Empty;

    /// <summary>세션 기록 폴더(claude 형식). 한도의 정본은 화면 글자가 아니라 여기 있다. 비면 ~/.claude/projects.</summary>
    public string UsageTranscriptRoot { get; set; } = string.Empty;

    /// <summary>받은 것을 읽는 형식 — text(기본) · claude · agy · copilot. 형식마다 읽는 법이 달라 하나의 정규식으로 받으면 숫자만 나오고 제대로는 못 읽는다.</summary>
    public string UsageFormat { get; set; } = "text";

    /// <summary>사용량 조회의 제한 시간(초). 곁들이는 일이라 오래 기다리지 않는다.</summary>
    public int UsageTimeoutSeconds { get; set; } = 60;
}

/// <summary>「한 줄 물어보기」 설정. 설명은 QuickAskWorker 머리말.</summary>
public sealed class QuickAskOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>듣는 큐. AIAgentServer 의 AI:CliRelay:QueueName 과 같아야 한다. 브로커 주소는 QueueOptions.Host 를 같이 쓴다.</summary>
    public string QueueName { get; set; } = "ai_quick";

    /// <summary>한 줄 응답용 CLI 설정. 작업 실행 어댑터 인자와 분리해 위험 권한 옵션이 섞이지 않게 한다.</summary>
    public Dictionary<string, QuickAskAdapterOptions> Adapters { get; set; } = [];

    public int TimeoutSeconds { get; set; } = 60;

    public int MaxParallel { get; set; } = 2;

    /// <summary>들어오는 대화 문맥의 길이 상한.</summary>
    public int MaxPromptChars { get; set; } = 12000;

    /// <summary>CLI 응답의 길이 상한.</summary>
    public int MaxResponseChars { get; set; } = 12000;
}

public sealed class QuickAskAdapterOptions
{
    public string[] Args { get; set; } = [];

    public string[] WorkspaceArgs { get; set; } = [];

    /// <summary><c>arg</c> 또는 <c>stdin</c>.</summary>
    public string PromptVia { get; set; } = "arg";

    public string PromptArgPrefix { get; set; } = string.Empty;
}
