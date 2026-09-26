using Microsoft.AspNetCore.Components;
using JSini.Web.Models;

namespace JSini.Web.Components.Layout;

public partial class NoticePopup
{
    /// <summary>띄울 공지들. 순서대로 하나씩 넘긴다.</summary>
    [Parameter, EditorRequired] public IReadOnlyList<NoticeDto> Notices { get; set; } = [];

    /// <summary>창이 떠 있는지.</summary>
    [Parameter] public bool Visible { get; set; }

    /// <summary>창이 닫히면 알린다.</summary>
    [Parameter] public EventCallback<bool> VisibleChanged { get; set; }

    /// <summary>
    /// 관리 화면의 미리보기인지.
    ///
    /// <para>
    /// 딱지를 하나 더 달고, 「오늘 하루 보지 않기」 대신 안내 줄이 선다.
    /// 본문이 보이는 모양은 실제 공지와 <b>같다</b> — 그것이 이 화면을 함께
    /// 쓰는 이유다.
    /// </para>
    /// </summary>
    [Parameter] public bool Preview { get; set; }

    /// <summary>
    /// 사용자가 「오늘 하루 보지 않기」로 닫은 공지의 아이디.
    ///
    /// <para>
    /// 저장은 <c>NoticeAutoPopup</c> 이 한다 — 이 부품은 <b>무엇을 보여 줄지만</b>
    /// 안다. localStorage 를 여기서 만지면 미리보기가 흔적을 남기게 되고,
    /// 그러면 관리자가 미리보기를 한 번 열었다는 이유로 그 공지를 못 보게 된다.
    /// </para>
    /// </summary>
    [Parameter] public EventCallback<string> OnDismissed { get; set; }

    /// <summary>지금 보고 있는 공지의 자리.</summary>
    private int _index;

    /// <summary>지금 공지의 「오늘 하루 보지 않기」 체크 상태. 공지를 넘길 때마다 푼다.</summary>
    private bool _dismiss;

    /// <summary>목록이 바뀌었는지 보는 표. 같은 값이 다시 들어와도 자리를 잃지 않는다.</summary>
    private object? _seen;

    private NoticeDto? Current =>
        _index >= 0 && _index < Notices.Count ? Notices[_index] : null;

    /// <summary>
    /// 목록이 <b>실제로</b> 바뀔 때만 첫 장으로 되돌린다.
    ///
    /// <para>
    /// 렌더마다 되돌리면 「다음」을 눌러도 제자리다 — 부모가 다시 그려지는
    /// 것만으로 파라미터는 다시 들어오기 때문이다. 값이 아니라
    /// <b>참조</b>로 본다.
    /// </para>
    /// </summary>
    protected override void OnParametersSet()
    {
        if (ReferenceEquals(_seen, Notices))
        {
            return;
        }

        _seen = Notices;
        _index = 0;
        _dismiss = false;
    }

    /// <summary>다른 공지로 넘어간다. 체크는 공지마다 따로다.</summary>
    private void GoTo(int target)
    {
        if (target < 0 || target >= Notices.Count)
        {
            return;
        }

        _index = target;
        _dismiss = false;
    }

    /// <summary>「다음」은 한 장씩 넘기고, 마지막 장에서는 닫는다.</summary>
    private async Task NextAsync()
    {
        await RememberAsync();

        if (_index < Notices.Count - 1)
        {
            GoTo(_index + 1);
            return;
        }

        await SetVisibleAsync(false);
    }

    /// <summary>체크해 둔 공지를 부모에게 알린다.</summary>
    private Task RememberAsync() =>
        _dismiss && Current is { } notice && OnDismissed.HasDelegate
            ? OnDismissed.InvokeAsync(notice.Id)
            : Task.CompletedTask;

    /// <summary>
    /// 창이 닫힌다.
    ///
    /// <para>
    /// 위쪽 × 로 닫는 것도 여기로 온다. 그때도 체크해 둔 것은 기억한다 —
    /// × 는 「더 보지 않겠다」는 뜻이라 체크를 무시하면 다음에 또 뜬다.
    /// </para>
    /// </summary>
    private async Task SetVisibleAsync(bool visible)
    {
        if (!visible)
        {
            await RememberAsync();
        }

        Visible = visible;
        await VisibleChanged.InvokeAsync(visible);
    }

    /// <summary>
    /// 게시일. 시작일이 있으면 그것을, 없으면 등록일을 보여 준다.
    ///
    /// <para>
    /// <c>internal</c> 인 것은 <see cref="PublicNoticePopup"/> 이 같은 서식을
    /// 쓰기 때문이다. 그쪽은 회로 없이 도는 로그인 화면용 골격이라 부품을
    /// 나눠 쓸 수 없는데, <b>사용자가 읽는 글자만큼은 갈라지면 안 된다.</b>
    /// </para>
    /// </summary>
    internal static string PostedOn(NoticeDto notice) =>
        (notice.StartAt ?? notice.CreatedAt).ToString("yyyy. MM. dd.");

    /// <summary>사람이 읽는 크기. <c>internal</c> 인 이유는 위와 같다.</summary>
    internal static string Human(long bytes) => bytes switch
    {
        >= 1024L * 1024 * 1024 => $"{bytes / 1024.0 / 1024 / 1024:0.#} GB",
        >= 1024 * 1024 => $"{bytes / 1024.0 / 1024:0.#} MB",
        >= 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes} B",
    };
}
