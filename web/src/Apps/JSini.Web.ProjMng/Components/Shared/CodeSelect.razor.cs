using Microsoft.AspNetCore.Components;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Shared;

public partial class CodeSelect
{
    [Inject] private CommonCodes Codes { get; set; } = default!;

    /// <summary>읽어 온 목록 <b>그대로</b>. 모르는 값 한 줄을 여기에 섞지 않는다.</summary>
    private List<CommonCodeItem> _loaded = [];

    /// <summary>화면에 내보내는 목록. <see cref="_loaded"/> + 모르는 값 한 줄.</summary>
    private List<CommonCodeItem> _items = [];

    /// <summary>마지막으로 읽은 조합. 같은 조합이면 다시 읽지 않는다.</summary>
    private (string CodeId, string CodeKey)? _loadedFor;

    /// <summary>마지막으로 <b>맞춰 본</b> 값. 같은 값이면 다시 맞추지 않는다.</summary>
    private string? _settledValue;

    /// <summary>한 번이라도 맞춰 보았는가. 목록을 새로 읽으면 거짓으로 되돌린다.</summary>
    private bool _settled;

    /// <summary>공통코드 ID — 예: <c>db</c> · <c>CODE_TYPE</c> · <c>projlist</c></summary>
    [Parameter, EditorRequired] public string CodeId { get; set; } = string.Empty;

    /// <summary>코드 조회 시 함께 넘기는 보조 키 (<c>etc0</c>).</summary>
    [Parameter] public string CodeKey { get; set; } = string.Empty;

    /// <summary>고른 코드값. 단일 선택일 때만 쓴다.</summary>
    [Parameter] public string? Value { get; set; }

    [Parameter] public EventCallback<string?> ValueChanged { get; set; }

    /// <summary>
    /// 체크로 고른 코드값들. <see cref="Multiple"/> 일 때만 쓴다.
    /// <b>비어 있으면 전체</b>다 — 부르는 쪽이 조건을 안 싣는다.
    /// </summary>
    [Parameter] public IEnumerable<string>? Values { get; set; }

    [Parameter] public EventCallback<IEnumerable<string>?> ValuesChanged { get; set; }

    /// <summary>펼친 목록에서 체크로 여럿 고른다(머리말).</summary>
    [Parameter] public bool Multiple { get; set; }

    /// <summary>
    /// 고른 항목의 <b>이름</b>. 화면이 <c>@bind-Text</c> 로 받아 둔다.
    ///
    /// <para>
    /// 쓰는 자리는 하나다 — 휴대폰에서 접힌 조회줄에 적을 글자
    /// (<c>CommSch.MobileSummary</c>). 화면이 들고 있는 것은 코드(<c>17</c>)뿐이고
    /// <b>목록은 이 부품 안에 있어서</b> 화면이 코드로 이름을 되짚을 수 없다.
    /// </para>
    ///
    /// <para>
    /// <see cref="OnChanged"/> 로도 같은 것을 받을 수 있지만, 그 자리는 이미
    /// 조회를 거는 화면이 많아 <b>한 자리에 두 가지 일</b>이 된다.
    /// 여럿 고르는 칸(<see cref="Multiple"/>)에서는 고른 이름을 이어 붙인다.
    /// </para>
    /// </summary>
    [Parameter] public string? Text { get; set; }

    [Parameter] public EventCallback<string?> TextChanged { get; set; }

    /// <summary>
    /// 고른 항목 전체. 부가 컬럼이 필요한 화면이 받는다.
    /// 목록을 읽고 첫 항목을 자동으로 골랐을 때도 올라온다 — 그래야 화면이
    /// 그 신호로 최초 조회를 걸 수 있다.
    /// </summary>
    [Parameter] public EventCallback<CommonCodeItem?> OnChanged { get; set; }

    /// <summary>
    /// 칸 안에 뜨는 자리표시 글. <b>기본은 빈 글자다</b> — 이 부품이 놓이는
    /// 자리는 거의 다 라벨(<c>CommSchItem</c> · <c>DxFormLayoutItem</c>)이 붙어
    /// 있고, 라벨이 이미 하는 말을 칸 안에서 또 하면 회색 글자만 늘어난다.
    /// 라벨 없이 쓰는 드문 자리에서만 준다.
    /// </summary>
    [Parameter] public string Placeholder { get; set; } = string.Empty;

    /// <summary>목록을 읽은 뒤 첫 항목을 자동으로 고른다.</summary>
    [Parameter] public bool AutoSelectFirst { get; set; } = true;

    /// <summary>
    /// 들고 온 값이 목록에 없어도 <b>버리지 않는다.</b> 그 값을 그대로 항목
    /// 하나로 세워 보여 준다. 저장된 줄을 고치는 편집 창에서 켠다(머리말).
    /// </summary>
    [Parameter] public bool KeepUnknownValue { get; set; }

    /// <summary>맨 앞에 "전체"(빈 코드) 항목을 넣는다. 옛 이름은 <c>IsAll</c>.</summary>
    [Parameter] public bool ShowAll { get; set; }

    /// <summary>
    /// <see cref="CodeKey"/> 가 비어 있으면 조회하지 않는다. 옛 이름은 <c>IsEtcFix</c>.
    /// 상위 선택에 딸린 코드에 쓴다 — 프로젝트를 고르기 전에는 그 프로젝트의
    /// DB 목록을 읽어 봐야 의미가 없다.
    /// </summary>
    [Parameter] public bool EtcFix { get; set; }

    [Parameter] public bool AllowClear { get; set; }

    [Parameter] public bool Disabled { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        var key = (CodeId, CodeKey);

        if (_loadedFor != key)
        {
            _loadedFor = key;

            // 목록이 갈렸다. 값이 글자로는 그대로여도 다시 맞춰 봐야 한다.
            _settled = false;

            if (EtcFix && string.IsNullOrEmpty(CodeKey))
            {
                // 상위를 아직 안 골랐다. 목록을 비우고 고른 값도 되돌린다 —
                // 안 그러면 옛 상위에 딸린 값이 남아 엉뚱한 조회가 나간다.
                _loaded = [];
                _items = [];
                await SetValueAsync(null, null);
                return;
            }

            var loaded = await Codes.GetAsync(CodeId, CodeKey);

            // 다중에서는 「전체」를 넣지 않는다 — 아무것도 안 고른 것이 이미
            // 전체이고, 넣으면 「전체 + 공유일정」이 표현 가능해진다(머리말).
            _loaded = ShowAll && !Multiple
                ? [new CommonCodeItem(string.Empty, "전체", new Dictionary<string, string>()), .. loaded]
                : [.. loaded];

            _items = _loaded;
        }

        // 다중은 여기까지다. 아래는 「고른 값 하나를 목록에 맞추는」 일이고,
        // 그 규칙(모르는 값 세우기 · 첫 항목 자동 선택)은 전부 단일 전용이다.
        if (Multiple)
        {
            await ReportTextAsync(SelectedCodes.Count == 0 ? null : MultiText);
            return;
        }

        // **목록이 그대로여도 들고 온 값이 바뀌면 다시 맞춘다.**
        //
        // 이 줄이 없으면 `KeepUnknownValue` 가 편집 창에서 안 듣는다. 팝업의
        // 고르개는 창을 닫아도 살아 있어서 **다음 줄을 고칠 때 같은 부품이
        // 다시 쓰인다.** 그때 아래 「모르는 값」 자리가 다시 돌지 않으면
        // 코드표에 없는 값을 든 줄이 **빈 칸으로 열리고**, 다른 칸만 고쳐
        // 저장해도 그 칸이 지워진다 — 막으려던 바로 그 일이다.
        // (등록 창을 먼저 열었다가 수정 창을 열면 실제로 그랬다.)
        if (_settled && string.Equals(_settledValue, Value, StringComparison.Ordinal))
        {
            return;
        }

        _settled = true;
        _settledValue = Value;

        // 이미 고른 값이 목록에 있으면 그대로 둔다. 없으면 다시 고른다.
        var current = _loaded.FirstOrDefault(i =>
            string.Equals(i.Code, Value, StringComparison.Ordinal));

        if (current is not null)
        {
            // 앞서 세워 둔 「모르는 값」이 남아 있으면 걷어낸다.
            _items = _loaded;
            await ReportTextAsync(current.Name);
            return;
        }

        // 목록에 없는 값이다. 켜 두었으면 그대로 세운다 — 고르개가 비어
        // 보이지 않고, 다른 칸만 고쳐 저장해도 이 칸이 지워지지 않는다.
        //
        // **읽어 온 목록에 덧붙이지 않고 새로 만든다.** 덧붙이면 줄을 바꿔
        // 열 때마다 옛 값이 항목으로 쌓인다.
        if (KeepUnknownValue && !string.IsNullOrEmpty(Value))
        {
            _items = [.. _loaded, new CommonCodeItem(Value, Value, new Dictionary<string, string>())];
            await ReportTextAsync(Value);
            return;
        }

        _items = _loaded;

        var first = AutoSelectFirst ? _loaded.FirstOrDefault() : null;
        await SetValueAsync(first?.Code, first);
    }

    /// <summary>체크된 코드들. 화면이 준 것을 그대로 본다.</summary>
    private IReadOnlyList<string> SelectedCodes => Values is null ? [] : [.. Values];

    /// <summary>
    /// 칸이 들고 있는 값. <b>고른 것이 없으면 <c>null</c></b> 이라야 한다 —
    /// 그래야 칸이 <see cref="Placeholder"/> 로 비고 지우기 단추가 숨는다.
    /// 글자 자체는 <see cref="MultiText"/> 가 만들므로 여기 담기는 값은
    /// 「비었나 아닌가」만 뜻한다.
    /// </summary>
    private object? MultiValue =>
        SelectedCodes.Count == 0 ? null : string.Join('\u001f', SelectedCodes);

    /// <summary>칸에 적히는 글자. <b>코드가 아니라 이름</b>을 잇는다.</summary>
    private string MultiText =>
        string.Join(", ", SelectedCodes.Select(code =>
            _items.FirstOrDefault(i => string.Equals(i.Code, code, StringComparison.Ordinal))?.Name
            ?? code));

    /// <summary>
    /// 칸 값이 바뀌는 길은 <b>지우기 단추 하나뿐</b>이다(고르기는 목록이 맡는다).
    /// 그래서 <c>null</c> 이 올 때만 체크를 전부 푼다 — 다른 값이 오면 우리가
    /// 만든 값이 되돌아온 것이라 할 일이 없다.
    /// </summary>
    private Task OnDropDownValueChangedAsync(object? value) =>
        value is null ? OnPickedManyAsync([]) : Task.CompletedTask;

    /// <summary>
    /// 체크가 바뀌었다. <b>값을 우리가 들고 있지 않는다</b> — 화면이 들고 있고
    /// 우리는 올리기만 한다. 여기서 <c>Values</c> 에 적어 두면 화면이 되돌린
    /// 값(조건 초기화 따위)이 다음 렌더에 묻힌다.
    /// </summary>
    private Task OnPickedManyAsync(IEnumerable<string>? values) =>
        ValuesChanged.InvokeAsync(values is null ? [] : [.. values]);

    private Task OnPickedAsync(string? code) =>
        SetValueAsync(code, _items.FirstOrDefault(i =>
            string.Equals(i.Code, code, StringComparison.Ordinal)));

    private async Task SetValueAsync(string? code, CommonCodeItem? item)
    {
        Value = code;

        // 우리가 정한 값이다. 이것으로 이미 맞춘 셈이라 적어 둔다 —
        // 안 적으면 바로 다음 파라미터에서 위 판정이 다시 걸린다.
        _settled = true;
        _settledValue = code;

        await ValueChanged.InvokeAsync(code);
        await ReportTextAsync(item?.Name);
        await OnChanged.InvokeAsync(item);
    }

    /// <summary>
    /// 고른 이름을 화면에 올린다. <b>달라졌을 때만</b> 올린다 — 그냥 올리면
    /// 부모가 다시 그리고, 다시 그리면 여기로 돌아와 끝이 없다.
    /// </summary>
    private async Task ReportTextAsync(string? text)
    {
        if (!TextChanged.HasDelegate || string.Equals(Text, text, StringComparison.Ordinal))
        {
            return;
        }

        Text = text;
        await TextChanged.InvokeAsync(text);
    }
}
