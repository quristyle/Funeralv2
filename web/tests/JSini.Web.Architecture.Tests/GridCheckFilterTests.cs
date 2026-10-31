using DevExpress.Blazor;
using DevExpress.Data.Filtering;
using JSini.Web.ProjMng.Api;
using JSini.Web.ProjMng.Components.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Xunit;

namespace JSini.Web.Architecture.Tests;

/// <summary>일정표 요청자·담당자 칸의 체크 목록 필터. 「빈값」을 체크하면 빈 줄만, 이름과 함께 체크하면 둘 다 남아야 한다 —
/// 기준이 틀려도 오류 없이 표가 비거나 그대로라서 실제 DxGrid 에 걸어 남은 줄을 센다.</summary>
public sealed class GridCheckFilterTests
{
    private static readonly WbsItemDto[] Rows =
    [
        new() { ProcNm = "가", ReqUser = "홍길동" },
        new() { ProcNm = "나", ReqUser = "김철수" },
        new() { ProcNm = "다", ReqUser = null },
        new() { ProcNm = "라", ReqUser = "" },
        new() { ProcNm = "마", ReqUser = "  " },
    ];

    [Fact]
    public void 빈값은_맨앞_한줄로_모인다()
    {
        var options = GridCheckFilter.Collect(Rows.Select(r => r.ReqUser), v => v);

        Assert.Equal(GridCheckFilter.BlankValue, options[0].Value);
        Assert.Equal(GridCheckFilter.BlankText, options[0].Text);
        Assert.Equal(3, options.Count);
    }

    [Fact]
    public void 빈값이_없으면_빈값_줄도_없다()
    {
        var options = GridCheckFilter.Collect(["a", "b"], v => v);

        Assert.DoesNotContain(options, o => o.Value == GridCheckFilter.BlankValue);
    }

    [Theory]
    [InlineData(new[] { "\0" }, new[] { "다", "라", "마" })]
    [InlineData(new[] { "홍길동" }, new[] { "가" })]
    [InlineData(new[] { "홍길동", "\0" }, new[] { "가", "다", "라", "마" })]
    [InlineData(new[] { "홍길동", "김철수" }, new[] { "가", "나" })]
    public async Task 체크한_값만_표에_남는다(string[] picked, string[] expected)
    {
        var criteria = GridCheckFilter.BuildCriteria(nameof(WbsItemDto.ReqUser), picked);

        // 표가 기준을 들고 있다가 다시 그려도 체크가 그대로 되살아나야 한다.
        Assert.Equal(picked.OrderBy(v => v), GridCheckFilter.ReadPicked(criteria).OrderBy(v => v));

        var html = await RenderGridAsync(criteria);

        foreach (var row in Rows)
        {
            var mark = $"[row:{row.ProcNm}]";
            if (expected.Contains(row.ProcNm))
            {
                Assert.Contains(mark, html);
            }
            else
            {
                Assert.DoesNotContain(mark, html);
            }
        }
    }

    /// <summary>필터 줄 칸 안에서 던지면 회로가 끊긴다(검증 Expression 따위). 실제 필터 줄에 끼워 한 번 그린다.</summary>
    [Fact]
    public async Task 필터_줄_칸에_체크_목록이_그려진다()
    {
        var html = await RenderGridAsync(GridCheckFilter.BuildCriteria(nameof(WbsItemDto.ReqUser), [GridCheckFilter.BlankValue]), withFilterRow: true);

        Assert.Contains("pm-grid-check-filter", html);
    }

    [Fact]
    public void 고른_것이_없으면_기준을_지운다() =>
        Assert.Null(GridCheckFilter.BuildCriteria("ReqUser", []));

    private static async Task<string> RenderGridAsync(CriteriaOperator? criteria, bool withFilterRow = false)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDevExpressBlazor();
        services.AddSingleton<IJSRuntime, NoJs>();
        services.AddSingleton<NavigationManager, Nav>();

        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());

        // 표는 기준을 파라미터로 받지 않는다 — 화면의 필터 줄처럼 그린 뒤에 건다.
        DxGrid? grid = null;

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var root = await renderer.RenderComponentAsync<Host>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(Host.Captured)] = (Action<DxGrid>)(g => grid = g), [nameof(Host.FilterRow)] = withFilterRow }));
            Assert.NotNull(grid);
            grid!.SetFilterCriteria(criteria);
            await root.QuiescenceTask;
            return System.Net.WebUtility.HtmlDecode(root.ToHtmlString());
        });
    }

    private sealed class Host : ComponentBase
    {
        [Parameter] public Action<DxGrid> Captured { get; set; } = default!;

        [Parameter] public bool FilterRow { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<DxGrid>(0);
            builder.AddAttribute(1, nameof(DxGrid.Data), Rows);
            builder.AddAttribute(3, nameof(DxGrid.PageSize), 100);
            builder.AddAttribute(6, nameof(DxGrid.ShowFilterRow), FilterRow);
            builder.AddAttribute(4, nameof(DxGrid.Columns), (RenderFragment)(cols =>
            {
                cols.OpenComponent<DxGridDataColumn>(0);
                cols.AddAttribute(1, nameof(DxGridDataColumn.FieldName), nameof(WbsItemDto.ProcNm));
                cols.AddAttribute(2, nameof(DxGridDataColumn.CellDisplayTemplate),
                    (RenderFragment<GridDataColumnCellDisplayTemplateContext>)(ctx => b =>
                        b.AddContent(0, $"[row:{((WbsItemDto)ctx.DataItem).ProcNm}]")));
                cols.CloseComponent();
                cols.OpenComponent<DxGridDataColumn>(3);
                cols.AddAttribute(4, nameof(DxGridDataColumn.FieldName), nameof(WbsItemDto.ReqUser));
                cols.AddAttribute(5, nameof(DxGridDataColumn.FilterRowCellTemplate),
                    (RenderFragment<GridDataColumnFilterRowCellTemplateContext>)(f => b =>
                    {
                        b.OpenComponent<GridCheckFilter>(0);
                        b.AddAttribute(1, nameof(GridCheckFilter.Filter), f);
                        b.AddAttribute(2, nameof(GridCheckFilter.Options), GridCheckFilter.Collect(Rows.Select(r => r.ReqUser), v => v));
                        b.CloseComponent();
                    }));
                cols.CloseComponent();
            }));
            builder.AddComponentReferenceCapture(5, r => Captured((DxGrid)r));
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
