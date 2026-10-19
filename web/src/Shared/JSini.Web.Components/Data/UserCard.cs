using JSini.Web.Http;
using Microsoft.Extensions.Logging;

namespace JSini.Web.Components.Data;

/// <summary>아바타를 누르면 뜨는 사람 카드의 손잡이. UserAvatar 가 열고 레이아웃의 UserCardHost 한 벌이 그린다. 카드를 아바타마다 두지 않는 까닭은 목록 스무 줄이 저마다 창을 들면 무겁고, 격자 칸의 overflow: hidden 에 말풍선이 잘리기 때문이다.
/// 연락처(메일 · 전화)는 누른 사람 한 명만 묻는다 — 얼굴 목록에 실으면 목록이 열릴 때마다 줄 수만큼 연락처가 브라우저로 간다.</summary>
public sealed class UserCard(GatewayClient gateway, ILogger<UserCard> logger)
{
    private const string Url = "auth/user/card";

    private readonly Dictionary<string, UserCardInfo> _known = new(StringComparer.OrdinalIgnoreCase);

    public event Action? Changed;

    /// <summary>지금 열린 카드의 사람. 닫혀 있으면 <c>null</c>.</summary>
    public UserCardRequest? Current { get; private set; }

    public bool IsOpen => Current is not null;

    /// <summary>카드를 연다. anchorId 는 누른 아바타의 요소 아이디다. 책상에서는 말풍선이 그 아래(자리가 없으면 위)에 붙는다.</summary>
    public void Open(string loginId, string anchorId, string? name = null, string? photo = null)
    {
        if (string.IsNullOrWhiteSpace(loginId))
        {
            return;
        }

        Current = new UserCardRequest(loginId.Trim(), anchorId, name, photo);
        Changed?.Invoke();
    }

    public void Close()
    {
        if (Current is null)
        {
            return;
        }

        Current = null;
        Changed?.Invoke();
    }

    /// <summary>그 사람의 카드 자료. 없는 사람이거나 못 읽었으면 null — 카드는 아바타가 이미 아는 이름과 얼굴만으로 선다.</summary>
    public async Task<UserCardInfo?> LoadAsync(string loginId, CancellationToken ct = default)
    {
        if (_known.TryGetValue(loginId, out var known))
        {
            return known;
        }

        try
        {
            var wire = await gateway.GetOneAsync<UserCardWire>(
                $"{Url}?id={Uri.EscapeDataString(loginId)}", ct);

            if (wire is null || string.IsNullOrWhiteSpace(wire.UserId))
            {
                return null;
            }

            var info = new UserCardInfo(
                wire.UserId.Trim(),
                string.IsNullOrWhiteSpace(wire.Name) ? wire.UserId.Trim() : wire.Name.Trim(),
                // 바깥 기본 이미지 주소는 걸지 않는다(UserFaceClient 와 같은 규칙).
                FileDownload.FileIdOf(wire.Avatar) is { } fileId ? FileDownload.ThumbnailUrlFor(fileId) : null,
                Blank(wire.Company),
                Blank(wire.Department),
                Blank(wire.Email),
                Blank(wire.Phone));

            _known[loginId] = info;
            return info;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (ApiException ex)
        {
            // 없는 사람(404)도 여기로 온다. 기억하지 않는다 — 잠깐 끊긴 것일 수 있다.
            logger.LogInformation(ex, "{LoginId} 의 사람 카드를 읽지 못했다.", loginId);
            return null;
        }
    }

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>카드를 연 요청 — 누가, 어느 아바타에서, 무엇을 이미 알고 있었나.</summary>
public sealed record UserCardRequest(string LoginId, string AnchorId, string? Name, string? Photo);

/// <summary>카드에 그리는 한 사람.</summary>
public sealed record UserCardInfo(
    string UserId, string Name, string? Photo, string? Company, string? Department, string? Email, string? Phone);

/// <summary><c>auth/user/card</c> 응답 한 벌.</summary>
public sealed class UserCardWire
{
    public string? UserId { get; set; }
    public string? Name { get; set; }
    public string? Avatar { get; set; }
    public string? Company { get; set; }
    public string? Department { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
}
