using Microsoft.EntityFrameworkCore;
using NotificationServer.Data;

namespace NotificationServer.Services;

/// <summary>
/// 포털 로그인 아이디 → <b>대표 메일 주소</b>.
/// </summary>
/// <remarks>
/// <para>
/// [왜 서비스로 꺼냈나]
/// </para>
///
/// <para>
/// 같은 질의가 <c>EmailEndpoints</c> 안에 private static 으로 숨어 있었다.
/// 메일을 내는 자리가 <b>둘</b>이 되면서(직발송 · <see cref="PushSender"/> 의
/// 메일 곁가지) 그 모양을 베껴야 했는데, 베끼면 <b>「대표 메일 우선」 규칙이
/// 두 곳에 산다.</b> 한쪽만 고치는 날 같은 사람에게 서로 다른 주소로 메일이
/// 간다 — 그 어긋남은 메일함에서만 보인다.
/// </para>
///
/// <para>
/// [본인이 메일을 껐는지는 <b>여기서 안 본다</b>]
/// </para>
///
/// <para>
/// 이 클래스가 답하는 것은 「이 사람의 주소가 무엇인가」 하나다. 보낼지 말지는
/// 부르는 쪽이 정한다 — 사람을 짚어 보내는 업무 메일은 본인의 뜻을 보지 않고
/// (<c>EmailEndpoints.ResolveUserEmailsAsync</c> 머리말), 역할로 가는 알림은
/// 지킨다. 그 갈림을 여기에 넣으면 둘 중 하나가 반드시 틀린다.
/// </para>
///
/// <para>
/// <c>scom</c> 은 이 서비스의 DB 가 아니라 <b>조회만</b> 한다
/// (<c>Entities/ScomIdentityRows.cs</c> 머리말).
/// </para>
/// </remarks>
public interface IAccountEmailResolver
{
    /// <summary>
    /// 로그인 아이디들의 대표 메일. <b>못 찾은 아이디는 사전에 없다</b> —
    /// 부르는 쪽이 키를 대조해 「메일이 없는 사람」을 짚어 말할 수 있게.
    /// </summary>
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

        // **대소문자를 가리지 않는다.** 역할표에서 꺼낸 아이디와 부르는 쪽이
        // 적어 보낸 아이디의 꼴이 늘 같지는 않다 — 못 알아보면 받아야 할
        // 사람이 조용히 빠진다.
        return rows
            .GroupBy(r => r.UserId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(r => r.IsPrimary).First().Content.Trim(),
                StringComparer.OrdinalIgnoreCase);
    }
}
