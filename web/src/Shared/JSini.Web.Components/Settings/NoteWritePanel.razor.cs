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

    /// <summary>보낸 뒤 호출된다. 보낸 통이 하나라도 있을 때만 온다 — 창을 닫거나 쪽지함을 다시 읽는 쪽이 받는다.</summary>
    [Parameter] public EventCallback<NoteSendResultDto> OnSent { get; set; }

    /// <summary>창을 열 때 받는 사람 칸에 미리 넣어 둘 아이디들.</summary>
    [Parameter] public IReadOnlyList<string>? DefaultTo { get; set; }

    /// <summary>화면을 채우는 모습인가. 휴대폰 전체화면이 참이다. 마크업은 하나고 이 값만 갈린다 — 창 모습 그대로면 자판에 보내기 단추가 가려 글을 다 쓰고도 보낼 수 없다.</summary>
    [Parameter] public bool Fill { get; set; }

    /// <summary>최근에 쪽지를 보낸 사람들. 딱지 한 번으로 담는다 — 어제 보낸 사람에게 또 보낼 때 이름을 다시 칠 필요가 없다.</summary>
    private IReadOnlyList<NoteRecipientDto> _recent = [];

    /// <summary>쪽지를 받을 수 있는 사람 전부. 「전체에서 고르기」가 편다.</summary>
    private IReadOnlyList<NoteRecipientDto> _all = [];

    /// <summary><see cref="_all"/> 이 상한에서 잘렸나.</summary>
    private bool _allTruncated;

    /// <summary>「전체에서 고르기」를 폈나. <b>한 번 펴면 보내고 나서도 접지 않는다.</b></summary>
    private bool _showAll;

    /// <summary>아이디 → 사람. 딱지에 이름을 적으려고 든다. 보내는 값은 바꾸지 않고 보여 줄 때만 이름으로 바꾼다.</summary>
    private readonly Dictionary<string, NoteRecipientDto> _known = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>이메일 → 아이디. 같은 사람이 주소로 담기와 아이디로 담기 두 딱지로 갈리는 것을 막는다 — 글자로는 다른 값이라 그대로 두면 「2명」이라 세는데 서버는 한 통만 보낸다.</summary>
    private readonly Dictionary<string, string> _knownByEmail = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>받는 사람 딱지. 적은 글자 그대로 담는다 — 푸는 것은 서버다.</summary>
    private readonly List<string> _picked = [];

    private string? _entry;
    private string? _title;
    private string? _body;

    /// <summary>제목 칸을 폈나. 한 번 펴면 보내고 나서도 접지 않는다 — 이어 보낼 때도 쓰는 사람이 많다.</summary>
    private bool _showTitle;

    private bool _sending;

    /// <summary>찾기 결과. 비어 있어도 「없다」와 「아직 안 찾았다」는 다르다.</summary>
    private IReadOnlyList<NoteRecipientDto> _matches = [];

    private bool _searched;

    /// <summary>지금 치고 있는 글자. 글자마다 조회가 나가 늦게 온 앞 글자의 답은 버린다 — 안 그러면 목록이 치는 대로 앞뒤로 튄다.</summary>
    private string? _inFlight;

    /// <summary>보낼 수 있는가 — 사람과 내용이 있어야 한다. 제목은 비워도 서버가 지어 넣는다. 빈 쪽지는 알림 한 번일 뿐 아무 말도 아니다.</summary>
    private bool CanSend =>
        !_sending && _picked.Count > 0 && !string.IsNullOrWhiteSpace(_body);

    protected override async Task OnInitializedAsync()
    {
        foreach (var who in DefaultTo ?? [])
        {
            Add(who);
        }

        await LoadQuickAsync();
    }

    /// <summary>빠른 선택 목록을 읽는다. 못 읽어도 조용하다 — 여기서 토스트를 띄우면 창을 열 때마다 빨간 줄이 뜬다.</summary>
    private async Task LoadQuickAsync()
    {
        try
        {
            var quick = await Notes.GetQuickPickAsync();

            if (quick is null)
            {
                return;
            }

            _recent = quick.Recent;
            _all = quick.All;
            _allTruncated = quick.AllTruncated;

            foreach (var who in _recent.Concat(_all))
            {
                Remember(who);
            }
        }
        catch (ApiException)
        {
        }
    }

    /// <summary>지금 찾는 중인가 — 칸에 두 글자 이상 쳐 두었다. 빠른 선택은 이것이 거짓일 때만 보인다.</summary>
    private bool Searching => (_entry ?? string.Empty).Trim().Length >= MinQuery;

    /// <summary>찾은 사람 중 <b>아직 안 담은</b> 사람들.</summary>
    private IReadOnlyList<NoteRecipientDto> Remaining =>
        [.. _matches.Where(h => !Has(h.LoginId))];

    /// <summary>이 사람을 이미 담았나. 아이디로 접어 본다(_knownByEmail).</summary>
    private bool Has(string token)
    {
        var key = Canon(token);

        return _picked.Any(p => string.Equals(Canon(p), key, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>적힌 글자를 아는 사람이면 아이디로 바꾼다. 견주기에만 쓴다 — 보내는 값은 그대로다(서버가 사람이 적은 글자에서 다시 푼다).</summary>
    private string Canon(string token)
    {
        var text = token.Trim();

        if (_known.ContainsKey(text)) return text;

        return _knownByEmail.TryGetValue(text, out var byMail) ? byMail : text;
    }

    /// <summary>찾기·빠른 선택으로 알게 된 사람을 적어 둔다.</summary>
    private void Remember(NoteRecipientDto who)
    {
        _known[who.LoginId] = who;

        if (who.Email is { Length: > 0 } mail)
        {
            _knownByEmail[mail] = who.LoginId;
        }
    }

    /// <summary>뿌리 칸의 클래스. 채우는 모습에서만 하나가 더 붙는다.</summary>
    private string RootClass => Fill ? "jsini-note jsini-note--fill" : "jsini-note";

    /// <summary>최근 딱지의 클래스. 담긴 사람은 눌린 모습이다.</summary>
    private string QuickClass(NoteRecipientDto who) => Has(who.LoginId)
        ? "jsini-note__quick-chip jsini-note__quick-chip--on"
        : "jsini-note__quick-chip";

    /// <summary>「전체에서 고르기」 단추의 글자.</summary>
    private string AllToggleText => _showAll
        ? "전체 접기"
        : $"전체에서 고르기 ({_all.Count}명)";

    /// <summary>보내기 단추의 글자. 몇 명에게 가는지를 단추가 말한다 — 딱지를 여럿 담으면 줄이 접혀 다 안 보인다.</summary>
    private string SendText => _picked.Count > 1
        ? $"쪽지 보내기 ({_picked.Count}명)"
        : "쪽지 보내기";

    /// <summary>딱지에 적을 글자. 아는 사람이면 이름으로 적는다 — 담기는 값은 그대로다.</summary>
    private string Label(string who) =>
        _known.TryGetValue(Canon(who), out var hit) ? hit.Display : who;

    /// <summary>최근 딱지를 눌렀다 — 담겨 있으면 빼고 아니면 담는다. 잘못 누른 것을 그 자리에서 되돌린다.</summary>
    private void Toggle(NoteRecipientDto who)
    {
        // 글자가 아니라 아이디로 찾는다 — 주소로 쳐 담은 사람을 이 딱지로 빼려면 같은 사람임을 알아야 한다.
        var had = _picked.FirstOrDefault(
            p => string.Equals(Canon(p), who.LoginId, StringComparison.OrdinalIgnoreCase));

        if (had is not null)
        {
            Remove(had);
            return;
        }

        Add(who.LoginId, who);
    }

    /// <summary>제목 칸을 편다. 한 번 펴면 이 판이 살아 있는 동안 그대로 있다.</summary>
    private void ShowTitle() => _showTitle = true;

    /// <summary>받는 사람 칸에서 Enter 나 쉼표를 누르면 딱지가 된다. 쉼표까지 받는 것은 주소를 여럿 붙여 넣는 사람 때문이다 — OnInput 시점에는 쉼표가 아직 없을 수 있어 글자가 아니라 키를 본다.</summary>
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

        // 손으로 친 글자는 딱지로 옮겨 간 것이라 칸을 비운다. 목록도 비우는 것은 그 글자가 사라져 찾던 결과가 뜻을 잃어서다.
        _entry = null;
        _matches = [];
        _searched = false;
    }

    /// <summary>딱지 하나를 더한다. 같은 사람을 두 번 넣지 않는다.</summary>
    /// <param name="who">담을 글자. 아이디거나 사람이 적은 이메일이다.</param>
    /// <param name="hit">아는 사람이면 함께 준다 — 딱지에 이름을 적으려는 것뿐이고 보내는 값은 바뀌지 않는다.</param>
    private void Add(string who, NoteRecipientDto? hit = null)
    {
        var text = who.Trim();

        if (text.Length == 0 || Has(text))
        {
            return;
        }

        _picked.Add(text);

        if (hit is not null)
        {
            Remember(hit);
        }
    }

    private void Remove(string who) => _picked.Remove(who);

    /// <summary>두 글자부터 찾는다. 못 찾아도 오류로 말하지 않는다 — 사람은 아이디를 그대로 적어도 보낼 수 있다.</summary>
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

            // 서버가 걸러 주지만 한 번 더 거른다 — 받을 길이 없는 사람이 고를 수 있는 줄로 서면 보내고 나서야 닿지 않는다는 것을 듣는다.
            _matches = [.. hits.Where(h => h.CanReceive)];
            _searched = true;

            // 찾아서 알게 된 사람도 적어 둔다 — 주소로 친 딱지와 같은 사람인지 가르는 데 쓰인다.
            foreach (var hit in _matches)
            {
                Remember(hit);
            }
        }
        catch (ApiException)
        {
            // 찾기가 막혀도 보내기는 막지 않는다.
            _matches = [];
            _searched = true;
        }
    }

    /// <summary>이 사람에게 쪽지가 무엇으로 닿는지. 메일로 닿으면 주소를 함께 적는다 — 같은 이름이 둘일 때 가르는 값이기도 하다.</summary>
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

        // 칸에 적다 만 글자도 받는 사람이다 — 추가를 안 누르고 바로 보내기를 누르는 것이 흔한 순서다.
        AddEntry();

        _sending = true;

        // 첫 await 앞에서 한 번 그린다 — 안 그리면 보내는 동안 단추가 눌린 채 남아 같은 쪽지가 두 통 간다.
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
            // 서버가 사람에게 그대로 보여 줄 문장을 준다(「그런 아이디·이메일이 없습니다: …」).
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

        // 보냈으면 칸을 비운다. 받는 사람은 남긴다 — 같은 사람에게 이어 보내는 일이 흔하다.
        _title = null;
        _body = null;

        await OnSent.InvokeAsync(result);
    }

    private Task CancelAsync() => OnCancel.InvokeAsync();

    /// <summary>결과를 사람이 읽는 한 줄로. 쪽지와 두드림을 갈라 적는다 — 「보냈습니다」만 말하면 알림이 안 간 것을 아무도 모르고, 「실패했습니다」라 하면 도착한 쪽지를 한 번 더 보낸다.</summary>
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

        // 못 찾은 것과 갈라 적는다 — 아이디가 틀린 것은 다시 칠 일이고, 푸시를 꺼 둔 것은 다른 길로 연락할 일이다.
        if (r.Blocked.Count > 0)
        {
            parts.Add($"푸시를 꺼 두어 못 받는 사람: {string.Join(", ", r.Blocked)}");
        }

        return string.Join(" · ", parts) + ".";
    }
}
