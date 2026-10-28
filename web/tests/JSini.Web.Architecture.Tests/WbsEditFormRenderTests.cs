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
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDevExpressBlazor();
        services.AddSingleton<IJSRuntime, NoJs>();
        services.AddSingleton<NavigationManager, Nav>();
        services.AddSingleton(await EmptyBizOptionsAsync());

        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<InEditContext>()).ToHtmlString());

        Assert.Contains("dxbl-date-edit", html);
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

    private sealed class InEditContext : ComponentBase
    {
        private readonly WbsItemDto _item = new() { ProcNm = "일감", PlanSdt = new DateTime(2026, 1, 5, 9, 0, 0) };

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<CascadingValue<EditContext>>(0);
            builder.AddAttribute(1, "Value", new EditContext(_item));
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(inner =>
            {
                inner.OpenComponent<WbsEditForm>(0);
                inner.AddAttribute(1, nameof(WbsEditForm.Item), _item);
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
