using CargoTrustServer.Common;
using CargoTrustServer.Data;
using CargoTrustServer.Endpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CargoTrustServer.Users;

/// <summary>
/// 게이트웨이의 신원(X-User-*)을 app_user 줄로 잇는다.
///
/// 회원 체계를 따로 두지 않는다(설계안 20) — 처음 부르면 줄을 만들고(user_type = DRIVER),
/// 이름은 X-User-Name 으로 매번 맞춘다.
/// </summary>
public class CargoUserService(CargoTrustDbContext db, IOptions<CargoTrustOptions> options, ILogger<CargoUserService> logger)
{
    // last_seen_at 을 요청마다 쓰면 조회 한 번이 쓰기 한 번이 된다. 이 간격 안에서는 건너뛴다.
    private static readonly TimeSpan SeenInterval = TimeSpan.FromMinutes(5);

    public async Task<(AppUser User, bool IsAdmin)> EnsureAsync(UserContext ctx, CancellationToken ct)
    {
        var externalId = ctx.UserId.Trim();
        var name = string.IsNullOrWhiteSpace(ctx.UserName) ? null : ctx.UserName.Trim();
        if (name is { Length: > 100 }) name = name[..100];

        var user = await db.Users.FirstOrDefaultAsync(u => u.ExternalUserId == externalId, ct);
        var now = DateTimeOffset.UtcNow;

        if (user is null)
        {
            user = new AppUser
            {
                ExternalUserId = externalId,
                DisplayName = name,
                UserType = UserType.DRIVER,
                Status = UserStatus.ACTIVE,
                CreatedAt = now,
                LastSeenAt = now,
            };
            db.Users.Add(user);
            try
            {
                await db.SaveChangesAsync(ct);
                logger.LogInformation("CargoTrust 사용자 줄 생성: {ExternalUserId} → {UserId}", externalId, user.UserId);
            }
            catch (DbUpdateException)
            {
                // 같은 사람의 첫 요청 둘이 겹치면 한쪽이 유일 제약(uq_app_user_external)에 걸린다.
                // 먼저 만든 줄을 다시 읽어 쓴다.
                db.Entry(user).State = EntityState.Detached;
                user = await db.Users.FirstAsync(u => u.ExternalUserId == externalId, ct);
            }
        }
        else
        {
            var changed = false;
            if (name is not null && user.DisplayName != name)
            {
                user.DisplayName = name;
                changed = true;
            }
            if (user.LastSeenAt is null || now - user.LastSeenAt.Value > SeenInterval)
            {
                user.LastSeenAt = now;
                changed = true;
            }
            if (changed)
                await db.SaveChangesAsync(ct);
        }

        var adminRoles = options.Value.EffectiveAdminRoles;
        var isAdmin = user.UserType == UserType.ADMIN
                      || ctx.Roles.Any(r => adminRoles.Contains(r.Trim(), StringComparer.OrdinalIgnoreCase));
        return (user, isAdmin);
    }
}
