using JSini.Web.Components.Data;
using Microsoft.AspNetCore.Components;

namespace JSini.Web.CargoTrust.Components.Shared;

public partial class NativeSelect
{
    /// <summary>
    /// 「직접 입력」 줄의 값. 빈 글자는 「고르지 않음」과 구별되지 않으니 진짜 값이 될 수 없는 글자를 쓴다.
    /// </summary>
    private const string CustomKey = "\u0001free";

    [Parameter, EditorRequired] public IReadOnlyList<SchOption> Items { get; set; } = [];

    /// <summary>고른 값. 「고르지 않음」은 <c>null</c> 이다.</summary>
    [Parameter] public string? Value { get; set; }

    [Parameter] public EventCallback<string?> ValueChanged { get; set; }

    /// <summary>
    /// 읽는 기계가 이 칸을 무엇을 고르는 것으로 읽게 한다. DxFormLayoutItem 의 제목은 label for= 로 묶이지 않아 이것이 없으면 「목록 상자」라고만 읽힌다.
    /// </summary>
    [Parameter] public string? Label { get; set; }

    [Parameter] public bool Disabled { get; set; }

    /// <summary>목록에 없는 말을 적을 수 있게 할 것인가(「직접 입력」 줄).</summary>
    [Parameter] public bool AllowUserInput { get; set; }

    [Parameter] public string CustomOptionText { get; set; } = "직접 입력…";

    [Parameter] public string? CustomPlaceholder { get; set; }

    /// <summary>글자 칸이 열려 있는가. 사람이 고른 것과 밖에서 들어온 값이 함께 정한다.</summary>
    private bool _custom;

    /// <summary>마지막으로 본 <see cref="Value"/>. 밖에서 바뀐 것만 다시 가르려고 쥔다.</summary>
    private string? _seen;

    private string FreeLabel => string.IsNullOrEmpty(Label) ? "직접 입력" : $"{Label} 직접 입력";

    private string SelectedKey => _custom ? CustomKey : Value ?? string.Empty;

    /// <summary>
    /// 밖에서 값이 바뀌어 들어왔을 때만 글자 칸의 열림을 다시 정한다. 매번 정하면 「직접 입력」을 고르고 아직 적지 않은 사이에 칸이 닫힌다.
    /// </summary>
    protected override void OnParametersSet()
    {
        if (!AllowUserInput)
        {
            _custom = false;
            _seen = Value;
            return;
        }

        if (string.Equals(_seen, Value, StringComparison.Ordinal))
        {
            return;
        }

        _seen = Value;
        _custom = !string.IsNullOrEmpty(Value) && !Items.Any(i => i.Value == Value);
    }

    private Task PickAsync(string? key)
    {
        if (key == CustomKey)
        {
            // 칸만 열고 값은 비운다 — 직전에 고른 「일반」이 남아 있으면
            // 사람이 그 글자를 지우는 일부터 해야 한다.
            _custom = true;
            return SetAsync(null);
        }

        _custom = false;
        return SetAsync(string.IsNullOrEmpty(key) ? null : key);
    }

    private Task TypeAsync(ChangeEventArgs e) =>
        SetAsync(e.Value as string is { Length: > 0 } typed ? typed : null);

    private Task SetAsync(string? value)
    {
        Value = value;

        // 여기서 바꾼 값이 부모를 거쳐 되돌아올 때 「밖에서 바뀐 것」으로
        // 읽히면 안 된다 — 그 길로 글자 칸이 닫힌다.
        _seen = value;

        return ValueChanged.InvokeAsync(value);
    }
}
