using Microsoft.AspNetCore.Components;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Shared;

public partial class BizSelect
{
    [Inject] private BizOptions Options { get; set; } = default!;

    private List<BizOption> _options = [];

    /// <summary>마지막으로 읽은 조합. 같은 조합이면 다시 읽지 않는다 — Vue 의 deep watch 대응.</summary>
    private (string Type, string ParamsKey)? _loadedFor;

    /// <summary>비즈니스 타입 — 예: <c>portal_account</c> · <c>company</c>. 메타데이터의 키다.</summary>
    [Parameter, EditorRequired] public string Type { get; set; } = string.Empty;

    /// <summary>조회 시 함께 보내는 런타임 파라미터.</summary>
    [Parameter] public IReadOnlyDictionary<string, object?>? Params { get; set; }

    /// <summary>고른 값 (단일 선택).</summary>
    [Parameter] public string? Value { get; set; }

    [Parameter] public EventCallback<string?> ValueChanged { get; set; }

    /// <summary>고른 값들 (다중 선택). <see cref="Multiple"/> 일 때만 쓴다.</summary>
    [Parameter] public IEnumerable<string>? Values { get; set; }

    [Parameter] public EventCallback<IEnumerable<string>?> ValuesChanged { get; set; }

    /// <summary>다중선택. Vue 의 <c>mode="multiple"</c> 대응.</summary>
    [Parameter] public bool Multiple { get; set; }

    /// <summary>
    /// 고른 항목 전체. 라벨·값 말고 다른 컬럼이 필요한 화면이 받는다.
    /// Vue 의 <c>change(value, item)</c> 두 번째 인자 대응.
    /// </summary>
    [Parameter] public EventCallback<BizOption?> OnChanged { get; set; }

    /// <summary>목록을 읽은 뒤 원본 행들을 올려 준다. Vue 의 <c>loaded</c> 대응.</summary>
    [Parameter] public EventCallback<IReadOnlyList<BizOption>> OnLoaded { get; set; }

    /// <summary>맨 앞에 "전체" 항목을 넣는다.</summary>
    [Parameter] public bool ShowAll { get; set; }

    /// <summary>"전체" 항목의 값. 기본은 빈 문자열.</summary>
    [Parameter] public string AllValue { get; set; } = string.Empty;

    /// <summary>목록을 읽은 뒤 값이 비어 있으면 첫 항목을 자동으로 고른다.</summary>
    [Parameter] public bool AutoSelectFirst { get; set; }

    /// <summary>
    /// 이 파라미터들이 채워지기 전에는 조회하지 않는다.
    /// 상위 선택에 딸린 셀렉트가 빈 목록을 받아 엉뚱한 첫 항목을
    /// 자동 선택하는 것을 막는다 (Vue 의 <c>requiredParams</c>).
    /// </summary>
    [Parameter] public IReadOnlyList<string>? RequiredParams { get; set; }

    /// <summary>목록 안에서 타자로 거른다. Vue 의 <c>show-search</c> 대응. 기본 켜짐.</summary>
    [Parameter] public bool ShowSearch { get; set; } = true;

    /// <summary>
    /// 칸 안에 뜨는 자리표시 글. <b>기본은 빈 글자다</b> — 이 부품이 놓이는
    /// 자리는 거의 다 라벨(<c>CommSchItem</c> · <c>DxFormLayoutItem</c>)이 붙어
    /// 있고, 라벨이 이미 하는 말을 칸 안에서 또 하면 회색 글자만 늘어난다.
    /// 라벨 없이 쓰는 드문 자리에서만 준다.
    /// </summary>
    [Parameter] public string Placeholder { get; set; } = string.Empty;

    [Parameter] public bool AllowClear { get; set; }

    [Parameter] public bool Disabled { get; set; }

    /// <summary>
    /// 예전부터 이름으로 걸려 있던 상위 조건 세 쌍. Vue 의 <c>LEGACY_REQUIRED</c> —
    /// 그 화면들이 프로퍼티를 따로 주지 않고 이 동작에 기대고 있어 그대로 지킨다.
    /// </summary>
    private static readonly Dictionary<string, string> LegacyRequired = new(StringComparer.Ordinal)
    {
        ["dept"] = "companyId",
        ["building"] = "companyId",
        ["floor"] = "buildingId",
    };

    protected override async Task OnParametersSetAsync()
    {
        var key = (Type, ParamsKey());
        if (_loadedFor == key)
        {
            return;
        }

        _loadedFor = key;
        await LoadAsync();
    }

    /// <summary>목록을 다시 읽는다. 화면이 저장 뒤 새로고침할 때 부른다 (Vue 의 <c>reload</c>).</summary>
    public Task ReloadAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        if (string.IsNullOrWhiteSpace(Type))
        {
            return;
        }

        if (ShouldWait())
        {
            _options = [];
            return;
        }

        var loaded = await Options.GetAsync(Type, Params);

        _options = ShowAll
            ? [new BizOption(AllValue, "전체", new Dictionary<string, string>()), .. loaded]
            : [.. loaded];

        await OnLoaded.InvokeAsync(loaded);

        // 값이 비어 있을 때만 첫 항목을 자동 선택한다 — 이미 고른 값은 지키고,
        // 자동 선택도 값 변경이므로 화면이 그 신호로 최초 조회를 걸 수 있게 올린다.
        if (AutoSelectFirst && _options.Count > 0 && string.IsNullOrEmpty(Value) && !Multiple)
        {
            await SetValueAsync(_options[0].Value, _options[0]);
        }
    }

    /// <summary>상위 선택을 기다려야 하는가 — 넘겼는데 비어 있는 필수 키가 있으면 참.</summary>
    private bool ShouldWait()
    {
        if (Params is null)
        {
            return false;
        }

        var keys = new List<string>(RequiredParams ?? []);
        if (LegacyRequired.TryGetValue(Type, out var legacy))
        {
            keys.Add(legacy);
        }

        return keys.Any(key =>
            Params.TryGetValue(key, out var value)
            && (value is null || value is string text && text.Length == 0));
    }

    /// <summary>파라미터 사전을 비교 가능한 문자열로. Vue 의 <c>JSON.stringify</c> 비교 대응.</summary>
    private string ParamsKey() =>
        Params is null
            ? string.Empty
            : string.Join('|', Params.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => $"{p.Key}={p.Value}"));

    private Task OnPickedAsync(string? value) =>
        SetValueAsync(value, _options.FirstOrDefault(o =>
            string.Equals(o.Value, value, StringComparison.Ordinal)));

    private async Task SetValueAsync(string? value, BizOption? item)
    {
        Value = value;
        await ValueChanged.InvokeAsync(value);
        await OnChanged.InvokeAsync(item);
    }

    private async Task OnPickedManyAsync(IEnumerable<string>? values)
    {
        Values = values;
        await ValuesChanged.InvokeAsync(values);
        await OnChanged.InvokeAsync(null);
    }
}
