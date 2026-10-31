using DevExpress.Blazor;
using JSini.Web.Components.Data;
using JSini.Web.ProjMng.Api;
using JSini.Web.ProjMng.Components.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>WBS 편집 창은 표의 편집 창(EditForm) 안에서 그려진다. 거기서 DevExpress 편집기는 *Expression 이 없으면 던지고 회로가 끊긴다 —
/// 빌드도 글자 검사도 못 보고 화면에는 아래쪽 노란 띠만 뜬다. 편집 창과 같은 EditContext 를 깔고 실제로 한 번 그린다.</summary>
public sealed class WbsEditFormRenderTests
{
    [Fact]
    public async Task 편집_창_안에서_WBS_폼이_그려진다()
    {
        var html = await RenderAsync(compact: false);

        Assert.Contains("dxbl-date-edit", html);
        Assert.DoesNotContain("pm-editform--float", html);
    }

    private static async Task<string> RenderAsync(bool compact)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDevExpressBlazor();
        services.AddSingleton<IJSRuntime, NoJs>();
        services.AddSingleton<NavigationManager, Nav>();
        services.AddSingleton(await EmptyBizOptionsAsync());
        services.AddSingleton(await EmptyCommonCodesAsync());

        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());

        return await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<InEditContext>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(InEditContext.Compact)] = compact }))).ToHtmlString());
    }

    /// <summary>일정표의 편집 창은 앞에 얹은 칸까지 한 격자에 두고 넓은 창에서 네 칸씩 늘어놓는다 — 폼이 둘로 갈리면 줄이 맞지 않는다.</summary>
    [Fact]
    public async Task 촘촘한_폼은_앞칸과_같은_격자에_떠_있는_라벨로_그려진다()
    {
        var html = await RenderAsync(compact: true);

        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "<dxbl-form-layout "));
        Assert.Contains("pm-editform--float", html);
        Assert.Contains("data-lead", html);
        Assert.Contains("dxbl-col-lg-3", html);
    }

    /// <summary>계정 고르개가 목록을 읽지 않게 메타데이터를 빈 채로 통에 미리 담는다 — 그 고르개는 설정이 없으면 빈 목록으로 지나간다.</summary>
    private static async Task<BizOptions> EmptyBizOptionsAsync()
    {
        var store = new ReferenceDataStore(
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<ReferenceDataStore>.Instance);
        var data = new ReferenceData(store, null!);

        await data.SharedAsync<IReadOnlyList<BizSelectConfig>>(
            ReferenceData.Groups.BizSelectConfigs, "all",
            () => Task.FromResult<IReadOnlyList<BizSelectConfig>?>([]));

        return new BizOptions(null!, null!, data, NullLogger<BizOptions>.Instance);
    }

    /// <summary>공통코드 고르개(추가대상)가 게이트웨이를 부르지 않게 그 목록을 빈 채로 통에 미리 담는다.</summary>
    private static async Task<CommonCodes> EmptyCommonCodesAsync()
    {
        var store = new ReferenceDataStore(
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<ReferenceDataStore>.Instance);
        var data = new ReferenceData(store, new FixedToken());

        await data.PerUserAsync<IReadOnlyList<CommonCodeItem>>(
            CommonCodes.Group, $"{AddTargets.CodeId} ",
            () => Task.FromResult<IReadOnlyList<CommonCodeItem>?>([]));

        return new CommonCodes(null!, data);
    }

    private sealed class FixedToken : JSini.Web.Http.ITokenStore
    {
        public void Initialize(System.Security.Claims.ClaimsPrincipal user) { }

        public ValueTask<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default) => new("test");

        public ValueTask<string?> GetRefreshCookieAsync(CancellationToken cancellationToken = default) => new((string?)null);

        public void UpdateAccessToken(string accessToken) { }

        public void Clear() { }
    }

    private sealed class InEditContext : ComponentBase
    {
        [Parameter] public bool Compact { get; set; }

        private readonly WbsItemDto _item = new() { ProcNm = "일감", PlanSdt = new DateTime(2026, 1, 5, 9, 0, 0) };

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<CascadingValue<EditContext>>(0);
            builder.AddAttribute(1, "Value", new EditContext(_item));
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(inner =>
            {
                inner.OpenComponent<WbsEditForm>(0);
                inner.AddAttribute(1, nameof(WbsEditForm.Item), _item);
                if (Compact)
                {
                    inner.AddAttribute(2, nameof(WbsEditForm.Compact), true);
                    inner.AddAttribute(3, nameof(WbsEditForm.Leading), (RenderFragment)(lead =>
                    {
                        lead.OpenElement(0, "span");
                        lead.AddAttribute(1, "data-lead", "1");
                        lead.CloseElement();
                    }));
                }
                inner.CloseComponent();
            }));
            builder.CloseComponent();
        }
    }

    private sealed class NoJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => default;

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => default;
    }

    private sealed class Nav : NavigationManager
    {
        public Nav() => Initialize("http://localhost/", "http://localhost/");
    }
}
