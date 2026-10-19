using JSini.Web.Http;
using JSini.Web.Models;

namespace JSini.Web.Components.Settings;

/// <summary>쪽지 — 사람에게서 사람에게로 가는 짧은 글. 보내고, 받은함·보낸함을 읽고, 읽음으로 찍고, 치운다. 쪽지 쓰기는 상단 띠에 있어 셸 레이아웃이 써야 하므로 공용 자리에 둔다(업무 모듈은 서로 참조하지 않는다 — NotifySender · NoticeClient 와 같은 까닭).
/// NotifySender 와 다르다 — 그쪽은 이미 아는 사람에게 두드림 한 통이고 남는 것은 발송 기록뿐이다. 이쪽은 아이디나 이메일로 사람을 정하고 글이 쪽지함에 남는다. 주소의 notification 이 두 번 나오는 것은 앞엣것이 게이트웨이 접두사, 뒤엣것이 서비스 안의 묶음이라서 하나를 빠뜨리면 404 다.</summary>
public sealed class NoteClient(GatewayClient gateway)
{
    /// <summary>받는 사람을 찾는다 — 아이디 · 이름 · 이메일 어느 것으로 쳐도 걸린다. 보내기가 이 결과를 쓰지 않고 서버가 사람이 적은 글자에서 다시 푼다 — 화면이 푼 아이디를 믿으면 남의 이름으로 보낼 수 있다.</summary>
    public Task<IReadOnlyList<NoteRecipientDto>> SearchRecipientsAsync(
        string query, CancellationToken ct = default)
        => gateway.GetListAsync<NoteRecipientDto>(
            $"notification/notes/recipients?q={Uri.EscapeDataString(query)}", ct);

    /// <summary>빠른 선택 — 아무것도 치지 않은 순간에 내놓을 사람들. 찾기는 두 글자를 쳐야 걸리는데 가장 자주 하는 일은 어제 보낸 사람에게 또 보내는 것이라 이름을 다시 치는 것은 낭비다. 이 결과도 보내기가 믿지 않는다 — 화면은 딱지에 아이디를 담을 뿐이다.</summary>
    public Task<NoteQuickPickDto?> GetQuickPickAsync(int take = 8, CancellationToken ct = default)
        => gateway.GetOneAsync<NoteQuickPickDto>(
            $"notification/notes/recipients/quick?take={take}", ct);

    /// <summary>쪽지를 보낸다.</summary>
    /// <param name="to">받는 사람. 로그인 아이디와 이메일 주소를 섞어 쉼표로 잇는다.</param>
    /// <param name="title">제목. 비워도 된다 — 서버가 「누가 언제 보냈다」로 지어 넣는다.</param>
    /// <param name="body">내용. 이쪽이 비면 서버가 막는다.</param>
    /// <param name="ct">그만두기.</param>
    /// <returns>서버가 준 결과. PushDevices 가 0 이어도 실패가 아니다 — 쪽지는 이미 상대의 쪽지함에 있다.</returns>
    /// <remarks>두드림을 고르는 값이 없다 — 앱 푸시는 늘 가고 메일은 받는 사람이 개인설정에서 켜 두었을 때만 간다. 보내는 사람이 정할 일이 아니다.</remarks>
    public Task<NoteSendResultDto?> SendAsync(
        string to, string? title, string? body,
        CancellationToken ct = default)
        => gateway.PostAsync<NoteSendResultDto>(
            "notification/notes",
            new { to, title, body },
            ct);

    /// <summary>받은 쪽지함.</summary>
    public Task<IReadOnlyList<NoteDto>> GetInboxAsync(
        DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
        => gateway.GetListAsync<NoteDto>("notification/notes/inbox" + Period(from, to), ct);

    /// <summary>보낸 쪽지함.</summary>
    public Task<IReadOnlyList<NoteDto>> GetSentAsync(
        DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
        => gateway.GetListAsync<NoteDto>("notification/notes/sent" + Period(from, to), ct);

    /// <summary>안 읽은 쪽지 수. 상단 띠의 봉투에 붙는 숫자다. 숫자만 받는 길을 따로 둔 것은 화면을 옮길 때마다 불리는 자리라서다 — 받은함을 통째로 읽어 세면 쪽지 본문 수백 줄이 숫자 하나 때문에 오간다.</summary>
    public Task<NoteUnreadDto?> GetUnreadCountAsync(CancellationToken ct = default)
        => gateway.GetOneAsync<NoteUnreadDto>("notification/notes/unread-count", ct);

    /// <summary>읽음으로 찍는다. <b>받은 사람만 찍을 수 있다</b>(서버가 막는다).</summary>
    public Task MarkReadAsync(string id, CancellationToken ct = default)
        => gateway.PostAsync(
            $"notification/notes/{Uri.EscapeDataString(id)}/read", new { }, ct);

    /// <summary>
    /// 내 쪽지함에서 치운다. <b>보낸 쪽·받은 쪽이 따로</b>라 상대의 것은 남는다.
    /// </summary>
    public Task DeleteAsync(string id, CancellationToken ct = default)
        => gateway.DeleteAsync($"notification/notes/{Uri.EscapeDataString(id)}", ct);

    /// <summary>기간 조건을 주소에 붙인다. 없으면 빈 글자다.</summary>
    private static string Period(DateTime? from, DateTime? to)
    {
        var parts = new List<string>(2);

        if (from is { } f) parts.Add($"startDate={f:yyyy-MM-dd}");
        if (to is { } t) parts.Add($"endDate={t:yyyy-MM-dd}");

        return parts.Count == 0 ? string.Empty : "?" + string.Join("&", parts);
    }
}
