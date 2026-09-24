using JSini.Web.Http;
using JSini.Web.Models;

namespace JSini.Web.Components.Settings;

/// <summary>
/// 쪽지 — <b>사람에게서 사람에게로 가는 짧은 글</b>. 보내고, 받은함·보낸함을 읽고,
/// 읽음으로 찍고, 치운다.
/// </summary>
/// <remarks>
/// <para>
/// [왜 업무 모듈이 아니라 여기 있나]
/// </para>
///
/// <para>
/// <b>쪽지 쓰기가 상단 띠에 있다.</b> 헤더는 셸의 레이아웃이고 레이아웃은 업무
/// 모듈을 이름으로 알지 못한다(web/CLAUDE.md 의 의존 규칙 4번). 포털관리에 두면
/// 어느 화면에서나 열리는 그 단추가 이것을 못 쓴다 — <c>NotifySender</c> ·
/// <c>NoticeClient</c> 가 여기 있는 것과 같은 까닭이다.
/// </para>
///
/// <para>
/// [<c>NotifySender</c> 와 무엇이 다른가]
/// </para>
///
/// <para>
/// 그쪽은 <b>이미 아는 사람들</b>(목록에서 고른 줄)에게 두드림 한 번을 보내고
/// 남는 것은 발송 기록뿐이다. 이쪽은 <b>아이디나 이메일을 적어</b> 사람을 정하고,
/// 글이 쪽지함에 남는다 — 두드림이 둘 다 막혀도 남는다. 둘을 한 클라이언트로
/// 묶으면 생일 축하 창이 쪽지함까지 지고 간다.
/// </para>
///
/// <para>
/// [주소에 <c>notification</c> 이 두 번 나온다]
/// </para>
///
/// <para>
/// 앞엣것은 게이트웨이 접두사(어느 서비스냐)고 뒤엣것은 서비스 안의 묶음이다.
/// 하나를 빠뜨리면 404 인데 화면에는 「읽지 못했습니다」로만 보인다.
/// </para>
/// </remarks>
public sealed class NoteClient(GatewayClient gateway)
{
    /// <summary>
    /// 받는 사람을 찾는다 — 아이디 · 이름 · 이메일 어느 것으로 쳐도 걸린다.
    /// </summary>
    /// <remarks>
    /// <b>보내기가 이 결과를 쓰지 않는다.</b> 서버가 사람이 적은 글자에서 다시
    /// 푼다 — 화면이 푼 아이디를 그대로 믿으면 브라우저에서 갈아 끼워 남의
    /// 이름으로 보낼 수 있다. 이 호출은 「이 사람이 맞나」를 눈으로 보는 자리다.
    /// </remarks>
    public Task<IReadOnlyList<NoteRecipientDto>> SearchRecipientsAsync(
        string query, CancellationToken ct = default)
        => gateway.GetListAsync<NoteRecipientDto>(
            $"notification/notes/recipients?q={Uri.EscapeDataString(query)}", ct);

    /// <summary>
    /// 쪽지를 보낸다.
    /// </summary>
    /// <param name="to">
    /// 받는 사람. <b>로그인 아이디와 이메일 주소를 섞어</b> 쉼표로 이어 적는다.
    /// </param>
    /// <param name="title">
    /// 제목. <b>비워도 된다</b> — 서버가 「누가 언제 보냈다」로 지어 넣는다.
    /// </param>
    /// <param name="body">내용. <b>이쪽이 비면 서버가 막는다.</b></param>
    /// <param name="ct">그만두기.</param>
    /// <returns>
    /// 서버가 준 결과. <b><c>PushDevices</c> 가 0 이어도 실패가 아니다</b> —
    /// 쪽지는 이미 상대의 쪽지함에 있다.
    /// </returns>
    /// <remarks>
    /// <b>두드림을 고르는 값이 없다.</b> 앱 푸시는 늘 가고(푸시를 꺼 둔 사람은
    /// 아예 받지 못한다), 메일은 <b>받는 사람이 개인설정에서 켜 두었을 때만</b>
    /// 간다 — 보내는 사람이 정할 일이 아니라서다.
    /// </remarks>
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

    /// <summary>
    /// 안 읽은 쪽지 수. <b>상단 띠의 봉투에 붙는 숫자</b>다.
    /// </summary>
    /// <remarks>
    /// 숫자만 받는 길을 따로 둔 것은, 이것이 <b>화면을 옮길 때마다</b> 불리는
    /// 자리이기 때문이다. 받은함을 통째로 읽어 세면 쪽지 본문 수백 줄이
    /// 숫자 하나 때문에 오간다.
    /// </remarks>
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
