using DevExpress.Blazor;

namespace JSini.Web.Components.Layout;

public partial class ConfirmDialog
{
    private TaskCompletionSource<bool>? _pending;

    private bool _visible;
    private string _message = string.Empty;
    private string _title = "확인";
    private string _confirmText = "삭제";
    private ButtonRenderStyle _confirmStyle = ButtonRenderStyle.Danger;

    /// <summary>
    /// 묻고 답을 기다린다. 확인이면 <c>true</c>.
    ///
    /// <para>
    /// <paramref name="message"/> 에는 <b>무엇이 사라지는지</b>를 적는다.
    /// "정말 삭제하시겠습니까?" 만으로는 어느 줄을 눌렀는지 확인할 수 없어
    /// 묻는 의미가 절반쯤 없어진다.
    /// </para>
    /// </summary>
    public Task<bool> AskAsync(
        string message,
        string title = "확인",
        string confirmText = "삭제",
        ButtonRenderStyle confirmStyle = ButtonRenderStyle.Danger)
    {
        // 앞의 물음이 남아 있으면 「아니오」로 닫는다. 둘이 겹쳐 뜨면 사람이
        // 어느 쪽에 답한 것인지 알 수 없고, 안 물어본 것이 지워질 수 있다.
        _pending?.TrySetResult(false);

        _message = message;
        _title = title;
        _confirmText = confirmText;
        _confirmStyle = confirmStyle;

        _pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _visible = true;

        StateHasChanged();
        return _pending.Task;
    }

    /// <summary>바깥에서 닫힌 것(Esc · X)도 「아니오」다.</summary>
    private void OnVisibleChanged(bool visible)
    {
        if (!visible)
        {
            Answer(false);
        }
    }

    private void Answer(bool confirmed)
    {
        _visible = false;

        var pending = _pending;
        _pending = null;
        pending?.TrySetResult(confirmed);
    }
}
