using Microsoft.EntityFrameworkCore;
using NotificationServer.Data;

namespace NotificationServer.Services;

/// <summary>포털 로그인 아이디 → 대표 메일 주소. 메일 내는 자리를 여기 하나로 둬 「대표 메일 우선」 규칙이 갈라지지 않게 한다.
/// 본인이 메일을 껐는지는 보지 않는다(보낼지는 부르는 쪽이 정한다).</summary>
public interface IAccountEmailResolver
{
    /// <summary>로그인 아이디들의 대표 메일. 못 찾은 아이디는 사전에 없다 — 부르는 쪽이 키를 대조해 메일 없는 사람을 짚을 수 있게 한다.</summary>
    Task<Dictionary<string, string>> ByLoginIdsAsync(
        IReadOnlyCollection<string> loginIds, CancellationToken ct = default);
}

/// <inheritdoc cref="IAccountEmailResolver" />
public sealed class AccountEmailResolver(AppDbContext db) : IAccountEmailResolver
{
    /// <inheritdoc />
    public async Task<Dictionary<string, string>> ByLoginIdsAsync(
        IReadOnlyCollection<string> loginIds, CancellationToken ct = default)
    {
        var keys = loginIds
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (keys.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var rows = await (
                from a in db.Accounts
                where keys.Contains(a.UserId) && !a.IsDeleted
                join d in db.AccountProfileDetails on a.Id equals d.AccountId
                where d.DetailType == "Email" && !d.IsDeleted && d.Content != ""
                select new { a.UserId, d.Content, d.IsPrimary })
            .ToListAsync(ct);

        // 대소문자를 가리지 않는다 — 역할표와 부르는 쪽의 아이디 꼴이 늘 같지는 않다.
        return rows
            .GroupBy(r => r.UserId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(r => r.IsPrimary).First().Content.Trim(),
                StringComparer.OrdinalIgnoreCase);
    }
}
