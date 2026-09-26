using Microsoft.AspNetCore.Components;

namespace JSini.Web.Components.Layout;

public partial class SizeModeScope
{
    [Inject] private ThemeSize Size { get; set; } = default!;
    [Inject] private PersistentComponentState PersistedState { get; set; } = default!;
    [Inject] private IHttpContextAccessor Http { get; set; } = default!;

    /// <summary>프리렌더가 남기는 값의 열쇠. 이 컴포넌트 안에서만 쓴다.</summary>
    private const string StateKey = "jsini.size";

    private PersistingComponentStateSubscription _persisting;

    [Parameter] public RenderFragment? ChildContent { get; set; }

    protected override void OnInitialized()
    {
        // 회로가 붙는 길이 먼저다. 프리렌더가 남긴 것이 있으면 그게 정답이고,
        // 그때는 HttpContext 가 없어 쿠키를 볼 수도 없다.
        // **단계 이름을 그대로 나른다.** SizeMode 로 바꿔 실으면 다섯 단계가
        // 셋으로 뭉개져서, 아주작게로 새로고침하면 작게로 되돌아간다.
        var seed = PersistedState.TryTakeFromJson<string>(StateKey, out var persisted)
            ? persisted
            : Http.HttpContext?.Request.Cookies[ThemeSize.CookieName];

        Size.Seed(seed);

        _persisting = PersistedState.RegisterOnPersisting(() =>
        {
            PersistedState.PersistAsJson(StateKey, Size.Step);
            return Task.CompletedTask;
        });

        Size.Changed += OnSizeChanged;
    }

    private void OnSizeChanged() => InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        Size.Changed -= OnSizeChanged;
        _persisting.Dispose();
    }
}
