using Microsoft.AspNetCore.Components;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Shared;

public partial class SourceSelect
{
    [Inject] private SourceInfoClient Sources { get; set; } = default!;

    private List<SourceItem> _items = [];

    /// <summary>마지막으로 읽은 프로젝트. 같은 값이면 다시 읽지 않는다.</summary>
    private string? _loadedFor;

    /// <summary>어느 프로젝트의 소스인가. 비어 있으면 조회하지 않는다.</summary>
    [Parameter, EditorRequired] public string? ProjectCode { get; set; }

    /// <summary>고른 소스 키(<c>src_rid</c>).</summary>
    [Parameter] public string? Value { get; set; }

    [Parameter] public EventCallback<string?> ValueChanged { get; set; }

    /// <summary>고른 소스의 이름. 접힌 조회줄에 쓴다(CodeSelect.Text 와 같은 까닭).</summary>
    [Parameter] public string? Text { get; set; }

    [Parameter] public EventCallback<string?> TextChanged { get; set; }

    /// <summary>고른 항목 전체. 목록을 읽고 첫 항목을 자동으로 골랐을 때도 올라온다 — 그 신호로 최초 조회를 건다.</summary>
    [Parameter] public EventCallback<SourceItem?> OnChanged { get; set; }

    /// <summary>칸 안의 자리표시 글. 기본은 빈 글자다 — 라벨이 이미 하는 말을 칸 안에서 또 하면 글자만 늘어난다.</summary>
    [Parameter] public string Placeholder { get; set; } = string.Empty;

    protected override async Task OnParametersSetAsync()
    {
        var key = ProjectCode ?? string.Empty;

        if (string.Equals(_loadedFor, key, StringComparison.Ordinal))
        {
            return;
        }

        _loadedFor = key;

        if (string.IsNullOrEmpty(key))
        {
            // 프로젝트를 아직 안 골랐다. 남겨 두면 옛 프로젝트의 소스로 엉뚱한 조회가 나가므로 비운다.
            _items = [];
            await SetAsync(null, null);
            return;
        }

        var sources = await Sources.ListAsync(int.TryParse(key, out var rid) ? rid : null);

        _items = [.. sources.Select(ToItem)];

        var first = _items.FirstOrDefault();
        await SetAsync(first?.Rid, first);
    }

    private static SourceItem ToItem(SourceInfoDto source)
    {
        var rid = source.SrcRid.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var nick = source.SrcNick;

        // 이름이 없는 소스는 키라도 보여 준다 — 빈 줄이 늘어선 드롭다운은 고를 수 없다.
        if (string.IsNullOrWhiteSpace(nick))
        {
            nick = string.IsNullOrWhiteSpace(source.SrcLang)
                ? $"소스 {rid}"
                : $"소스 {rid} ({source.SrcLang})";
        }

        return new SourceItem(rid, nick, source);
    }

    private Task PickAsync(string? rid) =>
        SetAsync(rid, _items.FirstOrDefault(i => string.Equals(i.Rid, rid, StringComparison.Ordinal)));

    private async Task SetAsync(string? rid, SourceItem? item)
    {
        Value = rid;
        await ValueChanged.InvokeAsync(rid);

        // 이름이 달라졌을 때만 올린다 — 그냥 올리면 부모가 다시 그리고 여기로 돌아온다.
        if (TextChanged.HasDelegate && !string.Equals(Text, item?.Nick, StringComparison.Ordinal))
        {
            Text = item?.Nick;
            await TextChanged.InvokeAsync(Text);
        }

        await OnChanged.InvokeAsync(item);
    }
}
