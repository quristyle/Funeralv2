using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using JSini.Web.Http;
using JSini.Web.Models;
using JSini.Web.Components.Layout;

namespace JSini.Web.Components.Settings;

public partial class NoteWritePanel
{
    [Inject] private NoteClient Notes { get; set; } = default!;
    [Inject] private Toasts Toasts { get; set; } = default!;

    /// <summary>찾기를 시작하는 글자 수. 한 글자로 훑으면 전 직원이 끌려 온다.</summary>
    private const int MinQuery = 2;

    /// <summary>내용 칸의 줄 수. 팝업은 좁으므로 부르는 쪽이 줄인다.</summary>
    [Parameter] public int BodyRows { get; set; } = 8;

    /// <summary>「닫기」 단추를 보일까. 팝업 안에서만 참이다.</summary>
    [Parameter] public bool ShowCancel { get; set; }

    /// <summary>「닫기」를 눌렀다.</summary>
    [Parameter] public EventCallback OnCancel { get; set; }

    /// <summary>
    /// 보내고 난 뒤. <b>보낸 통이 하나라도 있을 때만 온다</b> — 창을 닫거나
    /// 쪽지함을 다시 읽는 쪽이 받는다.
    /// </summary>
    [Parameter] public EventCallback<NoteSendResultDto> OnSent { get; set; }

    /// <summary>창을 열 때 받는 사람 칸에 미리 넣어 둘 아이디들.</summary>
    [Parameter] public IReadOnlyList<string>? DefaultTo { get; set; }

    /// <summary>받는 사람 딱지. 적은 글자 그대로 담는다 — 푸는 것은 서버다.</summary>
    private readonly List<string> _picked = [];

    private string? _entry;
    private string? _title;
    private string? _body;

    /// <summary>제목 칸을 폈나. <b>한 번 펴면 보내고 나서도 접지 않는다.</b></summary>
    /// <remarks>
    /// 제목을 쓰는 사람은 이어 보낼 때도 쓴다. 보낼 때마다 도로 접으면 매번
    /// 같은 단추를 다시 눌러야 한다 — 값만 비운다.
    /// </remarks>
    private bool _showTitle;

    private bool _sending;

    /// <summary>찾기 결과. 비어 있어도 「없다」와 「아직 안 찾았다」는 다르다.</summary>
    private IReadOnlyList<NoteRecipientDto> _matches = [];

    private bool _searched;

    /// <summary>
    /// 지금 치고 있는 글자. <b>답이 늦게 와서 앞 글자의 결과가 덮는 것</b>을 막는다.
    /// </summary>
    /// <remarks>
    /// 글자마다 조회가 나가므로 「김」의 결과가 「김병」보다 늦게 올 수 있다.
    /// 돌아온 답이 지금 친 글자의 것이 아니면 버린다 — 안 그러면 목록이
    /// 치는 대로 앞뒤로 튄다.
    /// </remarks>
    private string? _inFlight;

    /// <summary>
    /// 보낼 수 있는가 — <b>사람과 내용</b>이 있어야 한다.
    /// </summary>
    /// <remarks>
    /// 한동안 제목을 봤는데, 제목은 이제 비워도 서버가 지어 넣는다. 대신 내용이
    /// 비면 막는다 — 지어 줄 것이 없고, 빈 쪽지는 받는 사람에게 알림 한 번일 뿐
    /// 아무 말도 아니다.
    /// </remarks>
    private bool CanSend =>
        !_sending && _picked.Count > 0 && !string.IsNullOrWhiteSpace(_body);

    protected override void OnInitialized()
    {
        foreach (var who in DefaultTo ?? [])
        {
            Add(who);
        }
    }

    /// <summary>제목 칸을 편다. 한 번 펴면 이 판이 살아 있는 동안 그대로 있다.</summary>
    private void ShowTitle() => _showTitle = true;

    /// <summary>
    /// 받는 사람 칸에서 Enter · 쉼표를 누르면 딱지가 된다.
    /// </summary>
    /// <remarks>
    /// 쉼표까지 받는 것은, 메일 주소를 여럿 붙여 넣는 사람이 그대로 치기
    /// 때문이다. <c>DxTextBox</c> 는 값을 <c>OnInput</c> 으로 올리므로
    /// 그 시점의 <c>_entry</c> 에는 쉼표가 아직 없을 수 있다 — 그래서
    /// 글자가 아니라 <b>키</b>를 본다.
    /// </remarks>
    private async Task OnEntryKeyAsync(KeyboardEventArgs e)
    {
        if (e.Key is "Enter" or "NumpadEnter" or ",")
        {
            AddEntry();
            return;
        }

        await SearchAsync();
    }

    /// <summary>적은 글자를 딱지로 옮긴다. 쉼표로 여럿 붙여 넣은 것도 가른다.</summary>
    private void AddEntry()
    {
        foreach (var piece in (_entry ?? string.Empty)
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            Add(piece);
        }

        _entry = null;
        _matches = [];
        _searched = false;
    }

    /// <summary>딱지 하나를 더한다. <b>같은 사람을 두 번 넣지 않는다.</b></summary>
    private void Add(string who)
    {
        var text = who.Trim();

        if (text.Length == 0 || _picked.Contains(text, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        _picked.Add(text);

        // 골랐으면 찾기 칸을 비운다 — 안 비우면 방금 고른 사람이 목록에 남아
        // 한 번 더 누르게 된다.
        _entry = null;
        _matches = [];
        _searched = false;
    }

    private void Remove(string who) => _picked.Remove(who);

    /// <summary>
    /// 두 글자부터 찾는다. <b>못 찾아도 오류로 말하지 않는다</b> — 찾기는
    /// 거들 뿐이고, 사람은 아이디를 그대로 적어도 보낼 수 있다.
    /// </summary>
    private async Task SearchAsync()
    {
        var q = (_entry ?? string.Empty).Trim();

        if (q.Length < MinQuery)
        {
            _matches = [];
            _searched = false;
            return;
        }

        _inFlight = q;

        try
        {
            var hits = await Notes.SearchRecipientsAsync(q);

            // 늦게 온 답이 지금 친 글자의 것이 아니면 버린다.
            if (!string.Equals(_inFlight, q, StringComparison.Ordinal))
            {
                return;
            }

            // 서버가 이미 걸러 주지만 한 번 더 거른다 — 받을 길이 없는 사람이
            // 고를 수 있는 줄로 서면, 보내고 나서야 「닿지 않는다」를 듣는다.
            _matches = [.. hits.Where(h => h.CanReceive)];
            _searched = true;
        }
        catch (ApiException)
        {
            // 찾기가 막혀도 보내기는 막지 않는다.
            _matches = [];
            _searched = true;
        }
    }

    /// <summary>
    /// 이 사람에게 쪽지가 무엇으로 닿는지. 메일로 닿으면 주소를 함께 적는다 —
    /// 같은 이름이 둘일 때 가르는 값이기도 하다.
    /// </summary>
    private static string Channels(NoteRecipientDto hit) => (hit.PushReachable, hit.EmailReachable) switch
    {
        (true, true) => $"앱 푸시 · 메일 {hit.Email}",
        (true, false) => "앱 푸시",
        (false, true) => $"메일 {hit.Email}",
        _ => string.Empty,
    };

    private async Task SendAsync()
    {
        if (!CanSend)
        {
            return;
        }

        // 칸에 적다 만 글자가 있으면 그것도 받는 사람이다 — 「추가」를 안 누르고
        // 바로 보내기를 누르는 것이 사람의 흔한 순서다.
        AddEntry();

        _sending = true;

        // **첫 `await` 앞에서 한 번 그린다.** 안 그리면 보내는 동안 단추가 눌린
        // 채로 남아 사람이 한 번 더 누른다 — 그러면 같은 쪽지가 두 통 간다.
        StateHasChanged();

        NoteSendResultDto? result = null;
        string? error = null;

        try
        {
            result = await Notes.SendAsync(
                string.Join(",", _picked), _title?.Trim(), (_body ?? string.Empty).Trim());
        }
        catch (ApiException ex)
        {
            // 서버가 사람에게 그대로 보여 줄 수 있는 문장을 준다
            // (「그런 아이디·이메일이 없습니다: …」).
            error = ex.Message;
        }
        finally
        {
            _sending = false;
        }

        if (error is not null || result is null or { Sent: 0 })
        {
            Toasts.Show(error ?? "쪽지를 보내지 못했습니다.", NoticeTone.Error);
            return;
        }

        Toasts.Show(
            Describe(result),
            result.Unknown.Count > 0 || result.Blocked.Count > 0 || result.NotifyNote is not null
                ? NoticeTone.Warning
                : NoticeTone.Info);

        // 보냈으면 칸을 비운다. 받는 사람은 남긴다 — 같은 사람에게 이어 보내는
        // 일이 흔하고, 딱지는 한 번 더 지우면 그만이다.
        _title = null;
        _body = null;

        await OnSent.InvokeAsync(result);
    }

    private Task CancelAsync() => OnCancel.InvokeAsync();

    /// <summary>
    /// 결과를 사람이 읽는 한 줄로.
    /// </summary>
    /// <remarks>
    /// <b>쪽지와 두드림을 갈라 적는다.</b> 「보냈습니다」로만 말하면 알림이
    /// 안 간 것을 아무도 모르고, 「실패했습니다」로 말하면 이미 도착한 쪽지를
    /// 한 번 더 보낸다.
    /// </remarks>
    private static string Describe(NoteSendResultDto r)
    {
        var parts = new List<string> { $"쪽지 {r.Sent}통을 보냈습니다" };

        if (r.PushDevices > 0) parts.Add($"앱 알림 {r.PushDevices}대");
        if (r.EmailSent > 0) parts.Add($"메일 {r.EmailSent}명");
        if (r.NotifyNote is { Length: > 0 } why) parts.Add(why);

        if (r.Unknown.Count > 0)
        {
            parts.Add($"찾지 못한 값: {string.Join(", ", r.Unknown)}");
        }

        // **못 찾은 것과 갈라 적는다.** 아이디가 틀린 것은 다시 칠 일이고,
        // 푸시를 꺼 둔 것은 다른 길로 연락할 일이다.
        if (r.Blocked.Count > 0)
        {
            parts.Add($"푸시를 꺼 두어 못 받는 사람: {string.Join(", ", r.Blocked)}");
        }

        return string.Join(" · ", parts) + ".";
    }
}
