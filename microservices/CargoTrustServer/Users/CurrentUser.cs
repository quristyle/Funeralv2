using CargoTrustServer.Common;
using CargoTrustServer.Data;

namespace CargoTrustServer.Users;

/// <summary>
/// 지금 요청을 보낸 사람. 요청 범위(scoped) 서비스이고 <see cref="Endpoints.CargoUserFilter"/> 가 채운다.
///
/// 핸들러는 <c>CurrentUser me</c> 로 받는다. 채워지기 전에는 핸들러가 불리지 않는다 —
/// 필터가 신원이 없으면 401 로 끊기 때문이다.
/// </summary>
public class CurrentUser
{
    private AppUser? _user;

    /// <summary>app_user 줄. 필터를 지나기 전에 읽으면 예외다.</summary>
    public AppUser Entity => _user ?? throw new InvalidOperationException("CurrentUser 가 아직 채워지지 않았다.");

    public long UserId => Entity.UserId;
    public string ExternalUserId => Entity.ExternalUserId;
    public string DisplayName => Entity.DisplayName ?? Entity.ExternalUserId;
    public UserType UserType => Entity.UserType;
    public bool IsBlocked => Entity.Status == UserStatus.BLOCKED;

    /// <summary>포털 역할(설정 AdminRoles) 또는 user_type = ADMIN.</summary>
    public bool IsAdmin { get; private set; }

    public void Set(AppUser user, bool isAdmin)
    {
        _user = user;
        IsAdmin = isAdmin;
    }
}
