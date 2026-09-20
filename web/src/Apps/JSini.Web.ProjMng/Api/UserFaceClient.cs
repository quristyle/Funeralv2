using JSini.Web.Components.Data;
using JSini.Web.Http;
using Microsoft.Extensions.Logging;

namespace JSini.Web.ProjMng.Api;

/// <summary>
/// <b>로그인 아이디로 사람의 이름과 얼굴을 푼다</b> — <c>auth/user/faces</c>.
/// </summary>
/// <remarks>
/// <para>
/// [왜 업무 자료에 얼굴이 안 실려 오나]
/// </para>
///
/// <para>
/// 업무 DB 가 아는 것은 지시한 사람의 로그인 아이디뿐이다
/// (<c>projmng.ai_task.cre_id</c>). 사진은 계정 확장 속성이라
/// <c>scom</c> 에 있고, ProjMngServer 는 그 스키마를 보지 않는다 —
/// 보게 만들면 서비스 경계를 넘는다. 그래서 <b>화면이 한 번 더 묻는다.</b>
/// </para>
///
/// <para>
/// [한 번 물어본 것은 다시 묻지 않는다]
/// </para>
///
/// <para>
/// 「빠른 지시」 화면은 안 끝난 작업이 있는 동안 <b>몇 초마다 목록을 다시
/// 읽는다.</b> 얼굴까지 그때마다 물으면 카드 넉 장짜리 화면이 게이트웨이를
/// 쉼 없이 두드린다. 그래서 아는 것은 들고 있고 <b>모르는 아이디만</b>
/// 모아서 한 번에 묻는다.
/// </para>
///
/// <para>
/// 못 찾은 아이디도 기억한다(<see cref="_asked"/>). 안 그러면 없는 사람
/// 하나 때문에 조회가 영영 반복된다 — <b>가장 흔한 무한 조회의 모양</b>이다.
/// </para>
///
/// <para>
/// scoped 다. 회로 하나가 사용자 한 명이고, 그 사람이 화면을 떠나면 함께 사라진다.
/// </para>
/// </remarks>
public sealed class UserFaceClient(GatewayClient gateway, ILogger<UserFaceClient> logger)
{
    private const string Url = "auth/user/faces";

    /// <summary>
    /// 한 번에 물어보는 상한. 서버도 같은 수에서 자른다
    /// (<c>AuthServer/Endpoints/UserEndpoints.cs</c> 의 <c>MaxFaces</c>).
    /// </summary>
    private const int Chunk = 100;

    private readonly Dictionary<string, UserFace> _known = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>이미 물어본 아이디. <b>못 찾은 것도 들어 있다.</b></summary>
    private readonly HashSet<string> _asked = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 아는 얼굴. 아직 안 물어봤거나 없는 사람이면 <c>null</c>.
    /// <b>화면은 그때 아이디 첫 글자를 그린다.</b>
    /// </summary>
    public UserFace? Get(string? loginId)
        => !string.IsNullOrWhiteSpace(loginId) && _known.TryGetValue(loginId.Trim(), out var face)
            ? face
            : null;

    /// <summary>
    /// 이 아이디들의 얼굴을 채워 둔다. <b>모르는 것만 묻는다.</b>
    /// </summary>
    /// <remarks>
    /// <b>실패를 삼킨다.</b> 얼굴이 안 뜨는 것과 지시 목록이 안 뜨는 것은
    /// 무게가 다르다 — 못 읽으면 첫 글자로 그려진다.
    /// </remarks>
    public async Task EnsureAsync(IEnumerable<string?> loginIds, CancellationToken ct = default)
    {
        var missing = loginIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(id => !_asked.Contains(id))
            .Take(Chunk)
            .ToList();

        if (missing.Count == 0)
        {
            return;
        }

        // **묻기 전에 적어 둔다.** 조회가 도는 동안 타이머가 한 번 더
        // 들어오면 같은 아이디를 또 묻게 된다.
        foreach (var id in missing)
        {
            _asked.Add(id);
        }

        try
        {
            var query = string.Join(',', missing.Select(Uri.EscapeDataString));
            var faces = await gateway.GetListAsync<UserFaceWire>($"{Url}?ids={query}", ct);

            foreach (var wire in faces)
            {
                if (string.IsNullOrWhiteSpace(wire.UserId))
                {
                    continue;
                }

                _known[wire.UserId.Trim()] = new UserFace(
                    wire.UserId.Trim(),
                    string.IsNullOrWhiteSpace(wire.Name) ? wire.UserId.Trim() : wire.Name.Trim(),
                    // 우리 파일일 때만 그림을 건다. 바깥 기본 이미지 주소를
                    // 그대로 걸면 화면을 열 때마다 바깥으로 요청이 나간다.
                    FileDownload.FileIdOf(wire.Avatar) is { } fileId
                        ? FileDownload.ThumbnailUrlFor(fileId)
                        : null);
            }
        }
        catch (OperationCanceledException)
        {
            // 화면을 떠났거나 따라가기를 껐다. **고장이 아니므로 남기지 않는다** —
            // 남기면 이 화면을 닫을 때마다 경고가 한 줄씩 쌓인다.
            Forget(missing);
        }
        catch (Exception ex)
        {
            // **다시 물을 수 있게 되돌린다.** 잠깐 끊긴 것일 수 있고,
            // 그때 적어 둔 채로 두면 이 화면이 사는 동안 얼굴이 영영 안 뜬다.
            Forget(missing);

            logger.LogWarning(ex, "지시자 얼굴을 읽지 못했다. 이름 첫 글자로 그린다.");
        }
    }

    /// <summary>물어봤다는 표시를 지운다. 다음에 다시 묻는다.</summary>
    private void Forget(IEnumerable<string> loginIds)
    {
        foreach (var id in loginIds)
        {
            _asked.Remove(id);
        }
    }

    /// <summary>
    /// 사진을 못 받아 왔다. <b>주소를 버리고 첫 글자로 돌아간다.</b>
    /// </summary>
    /// <remarks>
    /// 브라우저는 그림을 못 받으면 그 자리에 깨진 그림 아이콘을 그리는데,
    /// 카드 목록에서 그것이 뜨면 사진이 없는 사람보다 더 나쁘게 보인다.
    /// 헤더가 같은 일을 한다(<c>CurrentUser.MarkAvatarUnavailable</c>).
    /// </remarks>
    public void MarkPhotoUnavailable(string? loginId)
    {
        if (Get(loginId) is not { Photo: not null } face)
        {
            return;
        }

        logger.LogInformation("{LoginId} 의 프로필 사진을 표시하지 못했다: {Url}", face.UserId, face.Photo);

        _known[face.UserId] = face with { Photo = null };
    }
}

/// <summary>사람 하나의 이름과 얼굴. 화면이 쓰는 모양이다.</summary>
/// <param name="UserId">포털 로그인 아이디.</param>
/// <param name="Name">사람이 읽는 이름.</param>
/// <param name="Photo">셸 중계 썸네일 주소. 사진이 없으면 <c>null</c>.</param>
public sealed record UserFace(string UserId, string Name, string? Photo)
{
    /// <summary>사진이 없을 때 동그라미에 넣을 글자 한 자.</summary>
    public string Initial => string.IsNullOrWhiteSpace(Name)
        ? "?"
        : Name.Trim()[..1].ToUpperInvariant();
}

/// <summary><c>auth/user/faces</c> 응답 한 벌.</summary>
public sealed class UserFaceWire
{
    public string? UserId { get; set; }
    public string? Name { get; set; }

    /// <summary>DB 에 적힌 값 그대로다 — 셸 중계 경로로 옮겨 걸어야 한다.</summary>
    public string? Avatar { get; set; }
}
