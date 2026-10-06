using JSini.Web.Components.Data;
using Microsoft.AspNetCore.Components;

namespace JSini.Web.CargoTrust.Components.Shared;

public partial class ChipGroup
{
    [Parameter, EditorRequired] public IReadOnlyList<SchOption> Items { get; set; } = [];

    /// <summary>고른 값. <c>null</c> 도 고른 것일 수 있다(「고르지 않음」 칩).</summary>
    [Parameter] public string? Value { get; set; }

    [Parameter] public EventCallback<string?> ValueChanged { get; set; }

    /// <summary>읽는 기계가 이 묶음이 무엇을 고르는 것인지 알게 한다.</summary>
    [Parameter] public string? Label { get; set; }

    [Parameter] public bool Disabled { get; set; }

    /// <summary>
    /// 같은 칩을 다시 눌러도 알린다 — 부르는 쪽이 그때 다시 셈할지 정한다.
    /// 여기서 막아 버리면 「왜 안 바뀌지」가 된다.
    /// </summary>
    private Task PickAsync(string? value)
    {
        Value = value;
        return ValueChanged.InvokeAsync(value);
    }
}
