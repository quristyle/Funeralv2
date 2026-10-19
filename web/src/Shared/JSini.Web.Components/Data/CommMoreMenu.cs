namespace JSini.Web.Components.Data;

/// <summary>관리 칸 「⋮」가 펴는 판의 상태. CommGrd 와 CommTree 가 같은 판을 쓴다 — 누른 줄, 열림, 붙을 자리를 여기서만 정한다.</summary>
public sealed class CommMoreMenu
{
    private readonly Action _changed;
    private readonly string _uid = "comm-" + Guid.NewGuid().ToString("N")[..8];
    private object? _data;

    /// <param name="changed">상태가 바뀌었을 때 표가 다시 그려지도록 부르는 것.</param>
    public CommMoreMenu(Action changed) => _changed = changed;

    public bool IsOpen { get; private set; }

    /// <summary>판이 붙은 줄의 차례 번호.</summary>
    public int Index { get; private set; } = -1;

    /// <summary>판이 붙은 줄의 자료.</summary>
    public object? Row { get; private set; }

    /// <summary>판이 붙을 「⋮」 의 선택자.</summary>
    public string? Target { get; private set; }

    /// <summary>「⋮」 칸에 붙이는 아이디.</summary>
    public string AnchorId(int visibleIndex) => $"{_uid}-act-{visibleIndex}";

    /// <summary>이 줄의 「⋮」 를 강조할지.</summary>
    public bool IsOpenAt(int visibleIndex) => IsOpen && Index == visibleIndex;

    /// <summary>같은 줄을 다시 누르면 닫고, 다른 줄이면 그 줄로 옮겨 연다. 열린 채 붙을 자리만 바꾸면 판이 옛 줄 곁에 남으므로 닫았다가 한 그림 뒤에 연다.</summary>
    public async Task ToggleAsync(int visibleIndex, object? row)
    {
        if (IsOpen && Index == visibleIndex)
        {
            Close();
            return;
        }

        if (IsOpen)
        {
            IsOpen = false;
            _changed();
            await Task.Delay(50);
        }

        Index = visibleIndex;
        Row = row;
        Target = "#" + AnchorId(visibleIndex);
        IsOpen = true;
    }

    public void Close() => IsOpen = false;

    public void SetOpen(bool open) => IsOpen = open;

    /// <summary>자료가 갈리면 판을 닫는다. 판은 줄을 차례 번호로 가리키므로 열린 채 다시 읽히면 「수정」이 다른 줄을 연다(자동 갱신 화면).</summary>
    public void ResetIfDataChanged(object? data)
    {
        if (!ReferenceEquals(_data, data))
        {
            _data = data;
            IsOpen = false;
        }
    }
}

/// <summary>판의 내용을 그릴 때 넘기는 줄 하나.</summary>
public readonly record struct CommMoreRow<TItem>(TItem Item, int Index);
