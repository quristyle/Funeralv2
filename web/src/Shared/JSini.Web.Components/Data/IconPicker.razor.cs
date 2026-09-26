using Microsoft.AspNetCore.Components;

namespace JSini.Web.Components.Data;

public partial class IconPicker
{
    /// <summary>지금 아이콘 이름 (<c>lucide:calendar-days</c>). 없으면 <c>null</c>.</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>고르거나 적어서 바뀌었을 때.</summary>
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }

    /// <summary>
    /// 고를 수 있는 이름들. <b>부르는 화면이 자기가 들고 있는 자료에서 뽑아 준다</b> —
    /// 왜 그렇게 하는지는 머리말에 있다.
    /// </summary>
    [Parameter] public IReadOnlyCollection<string> Available { get; set; } = [];

    private bool _open;
    private string _search = string.Empty;

    /// <summary>
    /// 격자에 그릴 것. 중복을 빼고 이름 순으로 세운다.
    /// </summary>
    /// <remarks>
    /// 검색은 <b>이름 조각</b>으로 한다. 접두사(<c>lucide:</c>)도 이름의 일부라
    /// 「lucide」로 치면 그 묶음만 남는다 — 따로 고르개를 둘 필요가 없다.
    /// </remarks>
    private List<string> Shown =>
        [.. Available
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(i => _search.Length == 0
                     || i.Contains(_search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(i => i, StringComparer.OrdinalIgnoreCase)];

    private void Toggle()
    {
        _open = !_open;

        // 열 때 검색어를 비운다. 남겨 두면 지난번에 치고 닫은 글자 때문에
        // 격자가 비어서 「아이콘이 없다」로 읽힌다.
        if (_open)
        {
            _search = string.Empty;
        }
    }

    /// <summary>
    /// 격자에서 골랐다. <b>고르면 판을 닫는다</b> — 하나만 고르는 자리라
    /// 열어 둘 이유가 없고, 닫히는 것이 「골라졌다」는 표시가 된다.
    /// </summary>
    private async Task PickAsync(string? icon)
    {
        _open = false;
        await Commit(icon);
    }

    /// <summary>
    /// 손으로 적었다. <b>빈 글자는 <c>null</c> 로 바꾼다</b> — DB 에 빈 문자열과
    /// <c>null</c> 이 섞이면 「아이콘 없음」이 두 값이 된다.
    /// </summary>
    private Task OnTyped(string? text) =>
        Commit(string.IsNullOrWhiteSpace(text) ? null : text.Trim());

    private Task Commit(string? icon)
    {
        if (string.Equals(icon, Value, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        Value = icon;
        return ValueChanged.InvokeAsync(icon);
    }
}
