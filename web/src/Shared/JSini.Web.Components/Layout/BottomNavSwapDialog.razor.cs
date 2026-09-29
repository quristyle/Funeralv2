using JSini.Web.Abstractions;
using Microsoft.AspNetCore.Components;

namespace JSini.Web.Components.Layout;

public partial class BottomNavSwapDialog
{
    /// <summary>아이콘을 메뉴에서 찾는다(<see cref="BottomNav.IconClass"/>).</summary>
    [Inject] private IMenuProvider Menus { get; set; } = default!;

    /// <summary>
    /// 답을 기다리는 물음. <see cref="ConfirmDialog"/> 와 같은 꼴이다 —
    /// <c>RunContinuationsAsynchronously</c> 를 켜는 까닭도 거기 적어 두었다.
    /// </summary>
    private TaskCompletionSource<int>? _pending;

    private bool _visible;

    /// <summary>지금 띠에 선 칸들. 창이 떠 있는 동안만 값이 있다.</summary>
    private IReadOnlyList<BottomNavItem> _items = [];

    /// <summary>넣으려는 화면의 이름.</summary>
    private string _incoming = string.Empty;

    /// <summary>
    /// 어느 칸과 바꿀지 묻고 기다린다. 고른 칸의 자리를 돌려주고,
    /// 그만두었으면 <c>-1</c> 이다.
    /// </summary>
    /// <param name="items">지금 띠에 선 칸들(다섯).</param>
    /// <param name="incoming">넣으려는 화면의 이름.</param>
    public Task<int> AskAsync(IReadOnlyList<BottomNavItem> items, string incoming)
    {
        // 앞의 물음이 남아 있으면 「그만두기」로 닫는다. 둘이 겹쳐 뜨면 어느
        // 쪽에 답한 것인지 알 수 없고, 고르지도 않은 칸이 밀려난다.
        _pending?.TrySetResult(-1);

        _items = items;
        _incoming = incoming;

        _pending = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        _visible = true;

        StateHasChanged();
        return _pending.Task;
    }

    /// <summary>바깥에서 닫힌 것(Esc · X)도 「그만두기」다.</summary>
    private void OnVisibleChanged(bool visible)
    {
        if (!visible)
        {
            Answer(-1);
        }
    }

    private void Answer(int index)
    {
        _visible = false;

        var pending = _pending;
        _pending = null;
        pending?.TrySetResult(index);
    }
}
