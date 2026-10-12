using JSini.Web.Http;

namespace JSini.Web.Admin.Api;

/// <summary>
/// 「메일 보내기」 화면이 쓰는 통로 — 보내기 · 보낸 메일함 · 최근 주소 · AI 요약.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="AdminClient"/> 에 얹지 않았다.</b> 저쪽은 이미 메서드가
/// 백 개가 넘는 포털관리의 만능 통로이고, 거기 더하면 이 화면 하나를 고치려고
/// 그 파일을 여는 일이 는다. 메일은 서비스도 하나(NotificationServer)이고
/// 부르는 화면도 하나라 통로를 따로 둔다.
/// </para>
/// <para>
/// 다만 <b>계정 목록은 여기 없다.</b> 「시스템 사용자 중에서 고르기」가 읽는
/// 것은 <c>auth/system/account/list</c> 이고 그것은 포털관리의 여러 화면이
/// 이미 쓰고 있다 — 같은 조회를 통로 둘에 두면 어느 날 한쪽에만 조건이 붙는다.
/// 화면이 <see cref="AdminClient.GetAccountsAsync"/> 를 그대로 부른다.
/// </para>
/// </remarks>
public sealed class MailClient(GatewayClient gateway)
{
    // ── 보내기 ──────────────────────────────────────────────

    /// <summary>
    /// 메일 한 통을 보내고 <b>보낸함에 남긴다</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>실패하면 예외가 난다</b>(<see cref="ApiException"/>). 서버가 502 로
    /// 답하기 때문인데, 그때도 <b>보낸함에는 이미 들어가 있다</b> — 사람이
    /// 쓴 글을 잃지 않으려는 것이다. 그래서 실패 안내에 「보낸 메일함에서
    /// 다시 보낼 수 있습니다」를 적는다.
    /// </para>
    /// <para>
    /// 「메시지 발송」의 메일 탭(<see cref="AdminClient.SendEmailAsync"/>)과
    /// 달리 <b>참·거짓이 아니라 결과 객체</b>가 온다. 저쪽은 받는 사람이
    /// 수신을 꺼 둬서 안 나가는 자리가 있어 거짓이 필요했는데, 이쪽은
    /// 주소를 사람이 직접 적는 편지라 그런 자리가 없다 — 대신 몇 곳으로
    /// 갔는지를 받아 토스트에 적는다.
    /// </para>
    /// </remarks>
    public Task<MailSendResult?> SendAsync(MailComposeRequest request, CancellationToken ct = default)
        => gateway.PostAsync<MailSendResult>("notification/mails", request, ct);

    // ── 보낸 메일함 ────────────────────────────────────────

    /// <summary>
    /// <b>내가</b> 보낸 메일 목록. 서버가 신원으로 거르므로 누구인지 안 넘긴다.
    /// </summary>
    public Task<IReadOnlyList<MailListItem>> GetSentAsync(
        DateTime? from = null, DateTime? to = null, string? keyword = null,
        CancellationToken ct = default)
        => gateway.GetListAsync<MailListItem>(
            "notification/mails" + Query(
                ("from", from?.ToString("yyyy-MM-dd")),
                ("to", to?.ToString("yyyy-MM-dd")),
                ("keyword", keyword)), ct);

    /// <summary>보낸 메일 한 통을 펼친다. 본문 전문이 여기서 온다.</summary>
    public Task<MailDetail?> GetAsync(string id, CancellationToken ct = default)
        => gateway.GetOneAsync<MailDetail>(
            $"notification/mails/{Uri.EscapeDataString(id)}", ct);

    /// <summary>
    /// 내 보낸함에서 치운다. <b>발송 기록은 그대로 남는다</b> — 보관함을
    /// 비우는 것과 「무엇이 나갔나」의 기록을 지우는 것은 다른 일이다.
    /// </summary>
    public Task DeleteAsync(string id, CancellationToken ct = default)
        => gateway.DeleteAsync($"notification/mails/{Uri.EscapeDataString(id)}", ct);

    /// <summary>
    /// 내가 최근에 쓴 주소들. <b>계정 목록과 갈래가 다르다</b> — 거래처처럼
    /// 계정이 없는 상대는 여기에만 있고, 그 주소가 다시 칠 일이 가장 많다.
    /// </summary>
    public Task<IReadOnlyList<MailAddressUse>> GetRecentAddressesAsync(
        int take = 20, CancellationToken ct = default)
        => gateway.GetListAsync<MailAddressUse>(
            "notification/mails/recent-addresses" + Query(("take", take)), ct);

    // ── AI 요약 ────────────────────────────────────────────

    /// <summary>
    /// 글을 몇 줄로 추린다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>GET 이 아니라 POST 다.</b> 다른 AI 추천(<c>ai/suggest-code</c>)은
    /// 낱말 하나를 쿼리로 보내지만, 여기 올라가는 것은 메일 본문 전체라
    /// URL 에 실으면 414 가 난다 — 그 오류는 화면에 「AI 가 안 된다」로만
    /// 보인다.
    /// </para>
    /// <para>
    /// <b>한도가 드는 호출이다.</b> 사람이 단추를 누를 때만 나가게 한다 —
    /// 본문을 칠 때마다 부르면 한 통 쓰는 데 수십 번이 나간다.
    /// </para>
    /// </remarks>
    public Task<string?> SummarizeAsync(string text, int maxLines = 3, CancellationToken ct = default)
        => gateway.PostAsync<string>("ai/summarize", new { text, maxLines }, ct);

    /// <summary>값이 있는 것만 쿼리스트링으로 잇는다 (<c>AdminClient</c> 의 것과 같은 규칙).</summary>
    private static string Query(params (string Key, object? Value)[] parameters)
    {
        var parts = new List<string>();

        foreach (var (key, value) in parameters)
        {
            var text = value switch
            {
                null => null,
                string s => string.IsNullOrWhiteSpace(s) ? null : s,
                _ => value.ToString(),
            };

            if (text is not null)
            {
                parts.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(text)}");
            }
        }

        return parts.Count == 0 ? string.Empty : "?" + string.Join('&', parts);
    }
}
