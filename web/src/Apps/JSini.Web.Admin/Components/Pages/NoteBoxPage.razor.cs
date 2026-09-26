using Microsoft.AspNetCore.Components;
using JSini.Web.Http;
using JSini.Web.Models;
using JSini.Web.Components.Data;
using JSini.Web.Components.Settings;

namespace JSini.Web.Admin.Components.Pages;

public partial class NoteBoxPage
{
    [Inject] private NoteClient Notes { get; set; } = default!;

    /// <summary>받은함 · 보낸함. 값이 주소의 갈래가 된다.</summary>
    private static readonly SchOption[] Boxes =
    [
        new("inbox", "받은 쪽지"),
        new("sent", "보낸 쪽지"),
    ];

    private string _box = "inbox";

    /// <summary>지금 받은함을 보고 있나. 머릿글 셋이 이 값으로 갈린다.</summary>
    private bool Inbox => _box == "inbox";

    private string WhoCaption => Inbox ? "보낸 사람" : "받는 사람";

    private string TimeCaption => Inbox ? "받은 때" : "보낸 때";

    /// <summary>
    /// 읽음 칸의 머릿글. 보낸함에서는 <b>상대가</b> 읽었는지다 — 같은 값인데
    /// 묻는 사람이 달라서, 글자를 안 바꾸면 자기가 읽은 것으로 읽힌다.
    /// </summary>
    private string ReadCaption => Inbox ? "상태" : "상대가";

    private string EmptyText => Inbox ? "받은 쪽지가 없습니다." : "보낸 쪽지가 없습니다.";

    /// <summary>
    /// 조회 기간. <b>기본이 최근 한 달이다.</b> 쪽지는 지우지 않으면 자라기만
    /// 하고, 자기 쪽지함은 드물게 열어 보므로 이레로는 짧다.
    /// </summary>
    private DateTime? _from = DateTime.Today.AddMonths(-1);
    private DateTime? _to = DateTime.Today;

    private bool _unreadOnly;

    private IReadOnlyList<NoteDto> _all = [];

    /// <summary>지금 열어 보고 있는 쪽지. <c>null</c> 이면 창이 닫혀 있다.</summary>
    private NoteDto? _opened;

    private bool _writing;
    private IReadOnlyList<string>? _writeTo;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        SchSummary.NameOf(Boxes, o => o.Value, o => o.Text, _box),
        SchSummary.Period(_from, _to),
        SchSummary.On(_unreadOnly, "안 읽은 것만"));

    private IReadOnlyList<NoteDto> Shown =>
        _unreadOnly ? [.. _all.Where(n => !n.IsRead)] : _all;

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _all = Inbox
            ? await Notes.GetInboxAsync(_from, _to)
            : await Notes.GetSentAsync(_from, _to);

        return _all.Count;
    }, EmptyText, "쪽지를 읽지 못했습니다");

    /// <summary>
    /// 쪽지를 연다. <b>받은함에서 열면 그 자리에서 읽음으로 찍는다.</b>
    /// </summary>
    /// <remarks>
    /// 찍기가 막혀도 창은 연다 — 읽는 것이 사람의 목적이고, 표시는 다음에
    /// 다시 열 때 또 시도된다.
    /// </remarks>
    private async Task OpenAsync(NoteDto note)
    {
        _opened = note;

        if (!Inbox || note.IsRead)
        {
            return;
        }

        try
        {
            await Notes.MarkReadAsync(note.Id);
            note.IsRead = true;
            note.ReadAt = DateTime.UtcNow;
        }
        catch (ApiException)
        {
            // 표시만 못 남겼다. 글은 이미 화면에 있다.
        }
    }

    /// <summary>쓰는 창을 연다. 답장이면 상대가 미리 들어간다.</summary>
    private void OpenWrite(string? to)
    {
        _writeTo = to is { Length: > 0 } ? [to] : null;
        _opened = null;
        _writing = true;
    }

    /// <summary>
    /// 내 쪽지함에서 치운다. <b>상대의 것은 남는다</b>(서버가 쪽을 가른다).
    /// </summary>
    private async Task DeleteAsync(NoteDto note)
    {
        if (await RunAsync(() => Notes.DeleteAsync(note.Id),
                "쪽지를 치웠습니다.", "치우지 못했습니다"))
        {
            await ReloadAsync();
        }
    }

    /// <summary>「누구」 칸에 적을 글. 받은함이면 보낸 사람이다.</summary>
    private string Who(NoteDto note) => Inbox
        ? Name(note.SenderName, note.SenderKey)
        : Name(note.ReceiverName, note.ReceiverKey);

    /// <summary>이름이 있으면 이름, 없으면 아이디.</summary>
    private static string Name(string? name, string loginId) =>
        string.IsNullOrWhiteSpace(name) ? loginId : $"{name} ({loginId})";
}
