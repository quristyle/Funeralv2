using JSini.Web.Http;

namespace JSini.Web.Admin.Api;

/// <summary>「메일 보내기」 화면이 쓰는 통로 — 보내기 · 보낸 메일함 · 최근 주소 · AI 요약.</summary>
public sealed class MailClient(GatewayClient gateway)
{
    // ── 보내기 ──────────────────────────────────────────────

    /// <summary>메일 한 통을 보내고 보낸함에 남긴다.</summary>
    public Task<MailSendResult?> SendAsync(MailComposeRequest request, CancellationToken ct = default)
        => gateway.PostAsync<MailSendResult>("notification/mails", request, ct);

    // ── 보낸 메일함 ────────────────────────────────────────

    /// <summary>내가 보낸 메일 목록. 서버가 신원으로 거르므로 누구인지 안 넘긴다.</summary>
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

    /// <summary>내 보낸함에서 치운다. 발송 기록은 그대로 남는다 — 보관함을 비우는 것과 「무엇이 나갔나」의 기록을 지우는 것은 다른 일이다.</summary>
    public Task DeleteAsync(string id, CancellationToken ct = default)
        => gateway.DeleteAsync($"notification/mails/{Uri.EscapeDataString(id)}", ct);

    /// <summary>내가 최근에 쓴 주소들. 계정 목록과 갈래가 다르다 — 거래처처럼 계정이 없는 상대는 여기에만 있고, 그 주소가 다시 칠 일이 가장 많다.</summary>
    public Task<IReadOnlyList<MailAddressUse>> GetRecentAddressesAsync(
        int take = 20, CancellationToken ct = default)
        => gateway.GetListAsync<MailAddressUse>(
            "notification/mails/recent-addresses" + Query(("take", take)), ct);

    // ── AI 요약 ────────────────────────────────────────────

    /// <summary>글을 몇 줄로 추린다.</summary>
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
