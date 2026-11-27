using AuthServer.Data;
using AuthServer.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Services;

/// <summary>세션을 닫는 자리. 비밀번호를 바꾸는 곳이 여럿이라 DbContext 에 붙인다.</summary>
public static class AuthSessions
{
    /// <summary>이 계정의 열린 세션을 모두 닫는다. 닫은 수를 준다.</summary>
    public static Task<int> RevokeAllSessionsAsync(
        this AppDbContext db, string accountId, string reason, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return db.AuthSessions
            .Where(s => s.AccountId == accountId && s.RevokedAt == null)
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.RevokedAt, now)
                .SetProperty(s => s.RevokeReason, reason)
                .SetProperty(s => s.UpdatedAt, now), ct);
    }

    /// <summary>세션 하나를 닫는다. 이미 닫혔거나 없으면 0.</summary>
    public static Task<int> RevokeSessionAsync(
        this AppDbContext db, string sessionId, string reason, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return db.AuthSessions
            .Where(s => s.Id == sessionId && s.RevokedAt == null)
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.RevokedAt, now)
                .SetProperty(s => s.RevokeReason, reason)
                .SetProperty(s => s.UpdatedAt, now), ct);
    }
}
