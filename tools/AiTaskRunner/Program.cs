using AiTaskRunner;

using Serilog;

// AI 작업 실행기. 큐를 듣고 서버에 일을 물어 집고, CLI(claude · agy)를 띄워 출력을 서버로 올린다.
// 받는 포트가 없다 — 큐와 서버로 나가기만 한다. 웹 서버와 같은 프로세스에 두지 않는다(셸을 여는 권한을 웹 요청 처리와 떼기 위해).

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Services.AddSerilog();

builder.Services.Configure<RunnerOptions>(builder.Configuration.GetSection("Runner"));

builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RunnerOptions>>().Value);

// 서버로 나가는 통로. 첨부도 이 통로로 내려와 넉넉히 둔다(첨부 한 장 최대 100MB). 30초로 두면 첨부만 가끔 실패해 원인이 안 보인다.
builder.Services.AddHttpClient<ServerClient>((sp, client) =>
{
    var options = sp.GetRequiredService<RunnerOptions>();

    client.BaseAddress = new Uri(options.ServerUrl);
    client.Timeout = TimeSpan.FromMinutes(2);
});

// CLI 로 못 묻는 한도 조회만 여기를 탄다(지금은 copilot — /usage 는 -p 로 주면 지시문으로 먹는다). 곁들이는 일이라 짧게 끊는다.
builder.Services.AddHttpClient("usage", client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);

    // 깃허브 한도 통로는 클라이언트 이름을 대 줘야 거절당하지 않는다.
    client.DefaultRequestHeaders.UserAgent.ParseAdd("JSini-AiTaskRunner/1.0");
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
});

builder.Services.AddSingleton<Workspace>();
builder.Services.AddSingleton<GitProbe>();
builder.Services.AddSingleton<FileProbe>();
builder.Services.AddSingleton<CliRunner>();
builder.Services.AddSingleton<PushGate>();
builder.Services.AddSingleton<QueueListener>();
builder.Services.AddHostedService<RunnerWorker>();

// AI CLI 의 한도(/usage)를 주기적으로 읽어 올린다. 본업(집어가기·하트비트)과 갈라 둔다 — 조회가 매달려도 본업은 돈다.
builder.Services.AddHostedService<UsageReporter>();

// 대상의 git 상태를 주기적으로 적어 둔다. 서버는 호스트 경로를 볼 수 없어 이쪽이 만든다(설계 11.7).
builder.Services.AddHostedService<TargetStatusWorker>();

// 「처리 파일」의 추정 줄이 정말 있는 파일인가를 대 보고 서버에 적는다. 반 마디짜리 경로는 온 경로로 펴진다.
builder.Services.AddHostedService<FileCheckWorker>();

// 복사본 작업공간은 끝나도 안 지운다. 이것이 오래된 것만 치운다.
builder.Services.AddHostedService<WorkspaceSweeper>();

// AI 서버의 무료 공급자가 전부 막혔을 때 한 줄 추천을 대신 받는 자리. 본업과 갈라 둔다.
builder.Services.AddHostedService<QuickAskWorker>();

var host = builder.Build();

await host.RunAsync();
