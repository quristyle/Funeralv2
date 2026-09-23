using AiTaskRunner;

using Serilog;

// ============================================================
// AI 작업 실행기
// ============================================================
//
// 설계: docs/ai-task-runner.md
//
// 큐를 듣고, 서버에 「줄 일 있나」를 물어 집고, CLI(claude · agy)를 띄우고,
// 출력을 서버로 밀어 올리고, 끝나면 보고한다.
//
// **받는 포트가 없다.** 큐와 서버로 나가기만 한다 — 그래서 방화벽에 구멍이
// 필요 없고, 이 프로그램을 밖에서 부를 방법도 없다.
//
// **웹 서버와 같은 프로세스에 두지 않는 이유**는 배포 편의가 아니다.
// 셸을 여는 권한이 웹 요청을 받는 프로세스와 같은 자리에 있으면 안 된다.

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Services.AddSerilog();

builder.Services.Configure<RunnerOptions>(builder.Configuration.GetSection("Runner"));

builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RunnerOptions>>().Value);

// 서버로 나가는 통로. 타임아웃을 넉넉히 둔다 — 로그 묶음이 클 수 있다.
builder.Services.AddHttpClient<ServerClient>((sp, client) =>
{
    var options = sp.GetRequiredService<RunnerOptions>();

    client.BaseAddress = new Uri(options.ServerUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// 한도 주소를 두드릴 통로. **CLI 로 못 묻는 것만 여기를 탄다**(지금은
// copilot — 그 CLI 의 `/usage` 는 대화 화면 안에서만 도는 명령이라 `-p` 로
// 주면 슬래시 명령이 아니라 지시문으로 먹는다). 곁들이는 일이라 짧게 끊는다.
builder.Services.AddHttpClient("usage", client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);

    // 깃허브의 한도 통로는 편집기 클라이언트만 부르던 자리라, 제 이름을
    // 대 주지 않으면 거절당할 수 있다.
    client.DefaultRequestHeaders.UserAgent.ParseAdd("JSini-AiTaskRunner/1.0");
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
});

builder.Services.AddSingleton<Workspace>();
builder.Services.AddSingleton<GitProbe>();
builder.Services.AddSingleton<CliRunner>();
builder.Services.AddSingleton<PushGate>();
builder.Services.AddSingleton<QueueListener>();
builder.Services.AddHostedService<RunnerWorker>();

// AI CLI 의 한도(`/usage`)를 주기적으로 읽어 서버로 올린다. **본업과 갈라
// 둔다** — 사용량 조회가 매달려도 집어가기와 하트비트는 그대로 돌아야 한다.
builder.Services.AddHostedService<UsageReporter>();

// 대상이 지금 어떤 상태인가를 주기적으로 적어 둔다. **서버는 그 경로를 볼 수
// 없다**(컨테이너 안이고 경로는 호스트의 것) — 「대상 git 상태」 화면이 읽는
// 값을 이쪽이 만든다(설계 11.7).
builder.Services.AddHostedService<TargetStatusWorker>();

// 복사본 작업공간은 끝나도 안 지운다(결과가 거기 있다). 그래서 쌓이고,
// 이것이 오래된 것만 치운다.
builder.Services.AddHostedService<WorkspaceSweeper>();

var host = builder.Build();

await host.RunAsync();
