using JSini.Web.Http;

namespace JSini.Web.Admin.Api;

public sealed class AdminClient(GatewayClient gateway)
{
    public Task<IReadOnlyList<NoticeDto>> GetNoticesAsync(string? keyword = null, CancellationToken ct = default)
    {
        var url = string.IsNullOrEmpty(keyword)
            ? "auth/notices"
            : $"auth/notices?keyword={Uri.EscapeDataString(keyword)}";
        return gateway.GetListAsync<NoticeDto>(url, ct);
    }

    public Task<NoticeDto?> GetNoticeAsync(string id, CancellationToken ct = default)
        => gateway.GetOneAsync<NoticeDto>($"auth/notices/{id}", ct);

    public Task CreateNoticeAsync(SaveNoticeDto notice, CancellationToken ct = default)
        => gateway.PostAsync("auth/notices", notice, ct);

    public Task UpdateNoticeAsync(string id, SaveNoticeDto notice, CancellationToken ct = default)
        => gateway.PutAsync($"auth/notices/{id}", notice, ct);

    public Task DeleteNoticeAsync(string id, CancellationToken ct = default)
        => gateway.DeleteAsync($"auth/notices/{id}", ct);
}
