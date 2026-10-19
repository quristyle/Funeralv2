using Microsoft.AspNetCore.Components;

namespace JSini.Web.Components.Data;

public partial class IconPicker
{
    /// <summary>지금 아이콘 이름 (<c>lucide:calendar-days</c>). 없으면 <c>null</c>.</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>고르거나 적어서 바뀌었을 때.</summary>
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }

    /// <summary>고를 수 있는 이름들. 부르는 화면이 들고 있는 메뉴 목록에서 뽑아 넘긴다 — 새 엔드포인트도 새 생성물도 없다.</summary>
    [Parameter] public IReadOnlyCollection<string> Available { get; set; } = [];

    /// <summary>
    /// 끌 수 있다. 메뉴 유형에 따라 아이콘이 필요 없는 자리에서 끈다 — 값은 그대로 남는다.
    /// </summary>
    [Parameter] public bool Enabled { get; set; } = true;

    private bool _open;
    private string _search = string.Empty;

    /// <summary>격자에 그릴 것. 중복을 빼고 이름 순으로 세운다. 검색은 이름 조각으로 한다 — 접두사 lucide 로 치면 그 묶음만 남는다.</summary>
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

    /// <summary>격자에서 골랐다. 고르면 판을 닫는다 — 닫히는 것이 골라졌다는 표시가 된다.</summary>
    private async Task PickAsync(string? icon)
    {
        _open = false;
        await Commit(icon);
    }

    /// <summary>손으로 적었다. 빈 글자는 null 로 바꾼다 — DB 에 빈 문자와 null 이 섞이면 아이콘 없음이 두 값이 된다.</summary>
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
