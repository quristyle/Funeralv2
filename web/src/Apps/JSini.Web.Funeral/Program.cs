using JSini.Web.Funeral;
using JSini.Web.Funeral.Components;
using JSini.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// 이 앱이 자기를 어떻게 부르는지. 라우트 접두사가 여기서 나온다.
var module = new FuneralModule();

// 셸과 나머지 업무 앱이 똑같이 구성되도록 등록을 한 곳에 모아 두었다.
// 앱마다 다른 것은 아래 base path 와 자기 업무 서비스 등록뿐이다.
builder.AddJSiniWebApp(module.RoutePrefix, typeof(Program).Assembly);
module.ConfigureServices(builder.Services, builder.Configuration);

var app = builder.Build();

// **이 앱은 /funeral 아래에서 산다.**
//
// 셸이 /funeral/... 를 접두사를 떼지 않고 그대로 넘긴다. 여기서 PathBase 를
// 잡아 주어야 라우팅·정적자원·Blazor 회로(_blazor)가 모두 그 아래로 맞춰진다.
// 안 잡으면 화면은 뜨는데 회로가 안 붙어서 "버튼이 안 눌리는" 상태가 된다.
app.UsePathBase(module.RoutePrefix);

app.UseJSiniWebApp();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
