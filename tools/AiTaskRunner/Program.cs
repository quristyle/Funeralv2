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

builder.Services.AddSingleton<Workspace>();
builder.Services.AddSingleton<CliRunner>();
builder.Services.AddSingleton<PushGate>();
builder.Services.AddSingleton<QueueListener>();
builder.Services.AddHostedService<RunnerWorker>();

// 복사본 작업공간은 끝나도 안 지운다(결과가 거기 있다). 그래서 쌓이고,
// 이것이 오래된 것만 치운다.
builder.Services.AddHostedService<WorkspaceSweeper>();

var host = builder.Build();

await host.RunAsync();
