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

    /// <summary>
    /// 이 장비가 돌릴 수 있는 CLI. <b>어댑터가 정본이다</b> — 따로 적지 않는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 한동안 <c>Runner:Kinds</c> 라는 배열이 따로 있었다. <b>그 둘이 어긋나는
    /// 방향에 따라 증상이 갈렸다</b>는 것이 이 속성이 생긴 까닭이다.
    /// </para>
    ///
    /// <list type="bullet">
    ///   <item><description>
    ///     <c>Kinds</c> 에 있는데 어댑터가 없으면 — 집어 가서 <b>「이 장비에는
    ///     'x' 어댑터가 없습니다」로 시끄럽게 실패한다.</b> 사람이 바로 안다.
    ///   </description></item>
    ///   <item><description>
    ///     어댑터가 있는데 <c>Kinds</c> 에 없으면 — 집어가기 질의가
    ///     <c>runner_kind = ANY(kinds)</c> 로 거르므로 <b>그 건을 아무도 집지
    ///     않는다.</b> 오류도 로그도 없이 화면에 「대기」로만 앉아 있다.
    ///   </description></item>
    /// </list>
    ///
    /// <para>
    /// 실제로 뒤엣것을 밟았다 — <c>copilot</c> 어댑터를 넣고 운영 장비에
    /// 올리면서 <c>appsettings.json</c> 을 옛것으로 두어, 코파일럿으로 시킨
    /// 건이 <b>영영 「대기」에 남았다.</b> 화면·서버·DB 는 전부 멀쩡했고
    /// 고장은 장비 설정 파일 한 줄에 있었다.
    /// </para>
    ///
    /// <para>
    /// <b>그래서 적는 자리를 하나로 줄였다.</b> 어댑터가 있으면 돌릴 수 있는
    /// 것이고, 없으면 못 돌리는 것이다. 어긋날 두 곳이 없으면 어긋나지 않는다.
    /// </para>
    ///
    /// <para>
    /// <b>이 장비에서만 하나를 끄려면 그 어댑터의 <c>Executable</c> 을 비운다.</b>
    /// <c>appsettings.Local.json</c> 에 <c>"Adapters": { "copilot": { "Executable": "" } }</c>
    /// 한 줄이면 된다 — <b>사전은 열쇠로 겹치므로</b> 배열처럼 칸 번호로
    /// 뒤섞이지 않는다. 옛 <c>Kinds</c> 배열이 바로 그 함정이었다
    /// (Local 에 두 개를 적으면 기본 파일의 셋째가 남아 중복으로 찍혔다).
    /// </para>
    /// </remarks>
    public IReadOnlyList<string> RunnableKinds =>
    [
        .. Adapters
            .Where(a => !string.IsNullOrWhiteSpace(a.Value.Executable))
            .Select(a => a.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase),
    ];

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

    /// <summary>
    /// 대상의 git 상태를 얼마나 자주 들여다보나(초). <b>0 이면 안 본다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 「대상 git 상태」 화면이 읽는 값을 만드는 주기다(설계 11.7). 서버가 그
    /// 경로를 볼 수 없어서 이 장비가 대신 본다.
    /// </para>
    /// <para>
    /// 짧게 둘 이유가 별로 없다 — 대상이 저 혼자 바뀌는 일은 드물고, 방금
    /// 무언가를 한 사람은 화면에서 <b>「지금 확인」</b>을 누른다(그쪽은 15초
    /// 안에 받는다). 반대로 길게 두면 그 단추를 안 누른 사람이 옛 값을 본다.
    /// </para>
    /// <para>
    /// 저장소가 아주 크거나 대상이 많아 디스크가 아플 때 올린다. 끄면
    /// 화면이 <b>「아직 확인된 적이 없습니다」</b>로만 남는다.
    /// </para>
    /// </remarks>
    public int StatusSeconds { get; set; } = 180;

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

    // ── 사용량 보고 ─────────────────────────────────────────

    /// <summary>
    /// AI CLI 의 <c>/usage</c> 를 읽어 올릴 것인가.
    /// </summary>
    /// <remarks>
    /// 끄면 <b>대시보드의 한도 칸이 통째로 빈다</b> — 서버는 이 값을 다른
    /// 데서 구할 방법이 없다. 개발 장비에서 운영 계정의 한도를 덮어쓰고
    /// 싶지 않을 때 끈다.
    /// </remarks>
    public bool UsageEnabled { get; set; } = true;

    /// <summary>
    /// 사용량을 얼마나 자주 물어보나(분).
    /// </summary>
    /// <remarks>
    /// <b>짧게 두지 않는다.</b> 물어보는 것 자체가 CLI 호출이고, 한도를
    /// 깎는 형태의 호출이라면 <i>사용량을 보려고 사용량을 쓰는</i> 꼴이 된다
    /// (AIAgentServer 가 공급자에게 따로 묻지 않는 것과 같은 이유 —
    /// <c>AiUsageTracker</c> 머리말).
    /// </remarks>
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

    /// <summary>
    /// 건에 제한 시간이 안 실려 왔을 때 쓸 값(분). 서버 기본과 같은 <b>60</b> 이다.
    /// </summary>
    public int TimeoutMinutes { get; set; } = 60;

    /// <summary>
    /// 결과문(<c>result_text</c>)을 어디서 가려내나 — <c>stream-json</c> · <c>tail</c>.
    /// </summary>
    public string ResultFrom { get; set; } = "tail";

    /// <summary>
    /// 이 CLI 에게 사용량을 물어보는 인자.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>비어 있으면 안 물어본다.</b> 사용량을 말해 주지 않는 CLI 가 있고,
    /// 그때 없는 명령을 부르면 15분마다 실패 한 줄이 쌓인다.
    /// </para>
    /// <para>
    /// 코드에 박지 않는 이유는 실행 인자를 설정으로 뺀 것과 같다 — 이 출력은
    /// 사람이 보라고 만든 화면이라 문구도 플래그도 예고 없이 바뀐다.
    /// </para>
    /// </remarks>
    public string[] UsageArgs { get; set; } = [];

    /// <summary>
    /// CLI 로 물을 수 없을 때 대신 두드릴 주소(HTTP GET).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b><c>copilot</c> 때문에 생겼다.</b> 그 CLI 의 <c>/usage</c> 는 대화 중에만
    /// 도는 화면 명령이라 <c>-p</c> 로 주면 <i>슬래시 명령이 아니라 지시문</i>으로
    /// 먹는다 — 실제로 「<c>/usage</c> 가 무엇인지」를 설명하는 답이 돌아오고,
    /// 그 답을 파싱하면 한도가 아니라 <b>모델이 지어낸 글</b>이 화면에 앉는다.
    /// 물어볼 길이 없는 것과 물어봤는데 엉뚱한 것이 오는 것은 다르고, 뒤엣것이
    /// 훨씬 나쁘다.
    /// </para>
    /// <para>
    /// <b>비어 있으면 안 쓴다.</b> <see cref="UsageArgs"/> 와 이것 중 하나만
    /// 있으면 되고, 둘 다 있으면 이쪽이 이긴다.
    /// </para>
    /// </remarks>
    public string UsageUrl { get; set; } = string.Empty;

    /// <summary>
    /// <see cref="UsageUrl"/> 을 부를 때 쓸 토큰이 든 JSON 파일.
    /// </summary>
    /// <remarks>
    /// <b>토큰을 설정 파일에 베껴 적지 않는다.</b> CLI 가 자기 자리에
    /// 갱신해 두는 값이라, 베껴 두면 그 CLI 가 다시 로그인한 날부터 조용히
    /// 401 만 받는다. 맨 앞의 <c>~</c> 는 집 폴더로 편다.
    /// </remarks>
    public string UsageTokenFile { get; set; } = string.Empty;

    /// <summary>
    /// 그 JSON 안에서 토큰이 있는 자리 — <c>authTokens.*.token</c> 처럼 점으로 잇는다.
    /// </summary>
    /// <remarks>
    /// <c>*</c> 는 「이름은 모르겠고 첫 칸」이라는 뜻이다. <c>copilot</c> 의
    /// 설정은 열쇠가 <c>https://github.com:계정</c> 이라 이름을 적을 수 없다.
    /// </remarks>
    public string UsageTokenPath { get; set; } = string.Empty;

    /// <summary>
    /// 받은 것을 무엇으로 읽나 — <c>text</c>(기본) · <c>agy</c> · <c>copilot</c>.
    /// </summary>
    /// <remarks>
    /// <b>형식마다 읽는 법이 아주 다르다.</b> <c>claude</c> 는 사람이 읽는
    /// 문장이고, <c>agy</c> 는 탭으로 끊은 표이며, <c>copilot</c> 은 JSON 이다.
    /// 하나의 정규식으로 셋을 다 받으려 들면 <i>어느 것도 제대로 못 읽는데
    /// 숫자는 나오는</i> 상태가 된다.
    /// </remarks>
    public string UsageFormat { get; set; } = "text";

    /// <summary>사용량 조회의 제한 시간(초). 곁들이는 일이라 오래 기다리지 않는다.</summary>
    public int UsageTimeoutSeconds { get; set; } = 60;
}

/// <summary>
/// 「한 줄 물어보기」 설정. 설명은 <see cref="QuickAskWorker"/> 머리말.
/// </summary>
public sealed class QuickAskOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 듣는 큐. <b>AIAgentServer 의 <c>AI:CliRelay:QueueName</c> 과 같아야 한다.</b>
    /// 브로커 주소는 <see cref="QueueOptions.Host"/> 를 같이 쓴다.
    /// </summary>
    public string QueueName { get; set; } = "ai_quick";

    /// <summary>어느 어댑터의 실행 파일을 쓸지. 실행 파일 경로만 빌려 오고 인자는 아래 것을 쓴다.</summary>
    public string Adapter { get; set; } = "antigravity";

    /// <summary>
    /// CLI 에 줄 인자. <b>작업 실행용 <c>Args</c> 를 쓰지 않는다</b> — 그쪽에는
    /// <c>--dangerously-skip-permissions</c> 가 들어 있다.
    /// </summary>
    public string[] Args { get; set; } = [];

    public int TimeoutSeconds { get; set; } = 60;

    public int MaxParallel { get; set; } = 2;

    /// <summary>들어오는 질문의 길이 상한. 한 줄 추천에는 수백 자면 넉넉하다.</summary>
    public int MaxPromptChars { get; set; } = 2000;
}
