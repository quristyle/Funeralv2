using JSini.Web.Http;
using Microsoft.Extensions.Logging;

namespace JSini.Web.Components.Data;

/// <summary>
/// <b>로그인 아이디로 사람의 이름과 얼굴을 푼다</b> — <c>auth/user/faces</c>.
/// </summary>
/// <remarks>
/// <para>
/// [여기로 올라온 까닭 — 세 번째다 (2026-10-15)]
/// </para>
///
/// <para>
/// 같은 부품이 프로젝트관리와 생활과환경에 한 벌씩 있었다. 저장소의 규칙은
/// <b>두 모듈이 쓰면 복제, 세 번째부터 승격</b>이고(web/CLAUDE.md), 헬프데스크의
/// 접수 대시보드가 세 번째가 되어 올렸다 — 베낀 쪽 머리말에 적어 둔
/// 「세 번째 모듈이 생기면 그때 올린다」를 지킨 것이다. 업무 모듈끼리는 서로
/// 참조할 수 없으므로 공유하려면 여기 말고는 둘 자리가 없다.
/// </para>
///
/// <para>
/// [왜 업무 자료에 얼굴이 안 실려 오나]
/// </para>
///
/// <para>
/// 업무 DB 가 아는 것은 사람의 <b>로그인 아이디</b>뿐이다 — 프로젝트관리는
/// <c>projmng.ai_task.cre_id</c>, 헬프데스크는 <c>helpdesk.admin.loginid</c>.
/// 이름과 사진의 정본은 포털 계정(<c>scom.accounts</c> 와 그 확장 속성
/// <c>Avatar</c>)이고, 업무 서버는 그 스키마를 보지 않는다 — 보게 만들면
/// 서비스 경계를 넘는다. 그래서 <b>화면이 한 번 더 묻는다.</b>
/// </para>
///
/// <para>
/// 업무 표에 이름을 베껴 두는 길도 있지만, 그러면 포털에서 이름을 고쳐도
/// 따라오지 않아 <b>같은 사람이 화면마다 다른 이름</b>으로 선다.
/// </para>
///
/// <para>
/// [한 번 물어본 것은 다시 묻지 않는다]
/// </para>
///
/// <para>
/// 목록 화면은 조건을 바꿀 때마다 다시 읽는다. 얼굴까지 그때마다 물으면 같은
/// 사람을 몇 번이고 묻는다. 그래서 아는 것은 들고 있고 <b>모르는 아이디만</b>
/// 모아서 한 번에 묻는다.
/// </para>
///
/// <para>
/// 못 찾은 아이디도 기억한다(<see cref="_asked"/>). 안 그러면 없는 사람 하나
/// 때문에 조회가 영영 반복된다 — <b>가장 흔한 무한 조회의 모양</b>이다.
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
    /// <b>화면은 그때 첫 글자를 그린다.</b>
    /// </summary>
    public UserFace? Get(string? loginId)
        => !string.IsNullOrWhiteSpace(loginId) && _known.TryGetValue(loginId.Trim(), out var face)
            ? face
            : null;

    /// <summary>이 사람의 사진 주소. 모르거나 없으면 <c>null</c>.</summary>
    public string? PhotoOf(string? loginId) => Get(loginId)?.Photo;

    /// <summary>이 사람의 이름. 모르면 <c>null</c> — 부르는 쪽이 아이디로 물러선다.</summary>
    public string? NameOf(string? loginId) => Get(loginId)?.Name;

    /// <summary>
    /// 이 아이디들의 얼굴을 채워 둔다. <b>모르는 것만 묻는다.</b>
    /// </summary>
    /// <remarks>
    /// <b>실패를 삼킨다.</b> 얼굴이 안 뜨는 것과 목록이 안 뜨는 것은 무게가
    /// 다르다 — 못 읽으면 첫 글자로 그려진다.
    /// </remarks>
    public async Task EnsureAsync(IEnumerable<string?> loginIds, CancellationToken ct = default)
    {
        var wanted = loginIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // 남이 이미 묻고 있는 아이디는 그 답을 함께 기다린다. 「물어봤다」만
        // 보고 돌아가면 부르는 쪽이 답이 오기 전에 첫 글자를 그리고 끝난다.
        var pending = wanted
            .Where(_inflight.ContainsKey)
            .Select(id => _inflight[id])
            .Distinct()
            .ToList();

        var missing = wanted
            .Where(id => !_asked.Contains(id))
            .Take(Chunk)
            .ToList();

        if (missing.Count > 0)
        {
            var fetch = FetchAsync(missing, ct);
            if (!fetch.IsCompleted)
            {
                foreach (var id in missing)
                {
                    _inflight[id] = fetch;
                }
            }

            pending.Add(fetch);
        }

        if (pending.Count > 0)
        {
            await Task.WhenAll(pending);
        }
    }

    /// <summary>
    /// 아바타 하나가 제 얼굴을 묻는다. <b>같은 그림 차례에 물은 것들을 모아</b>
    /// 한 번에 보낸다 — 목록 스무 줄이 저마다 부르면 왕복도 스물이 된다.
    /// </summary>
    /// <remarks>
    /// 형제 부품들의 <c>OnParametersSetAsync</c> 는 한 그림 차례 안에서 첫
    /// <c>await</c> 까지 잇달아 돈다. 그래서 한 번 양보하고 나서 보내면
    /// 그 사이에 줄을 선 아이디가 모두 한 묶음이 된다.
    /// </remarks>
    public Task WhenKnownAsync(string? loginId)
    {
        if (string.IsNullOrWhiteSpace(loginId))
        {
            return Task.CompletedTask;
        }

        var id = loginId.Trim();
        if (_inflight.TryGetValue(id, out var running))
        {
            return running;
        }

        if (_asked.Contains(id))
        {
            return Task.CompletedTask;
        }

        _queued ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _queued.Add(id);
        return _queuedFlush ??= FlushQueuedAsync();
    }

    private HashSet<string>? _queued;
    private Task? _queuedFlush;

    private async Task FlushQueuedAsync()
    {
        await Task.Yield();

        var ids = _queued ?? [];
        _queued = null;
        _queuedFlush = null;

        // 상한(Chunk)을 넘는 줄도 다 묻는다 — 한 묶음만 보내면 나머지가 첫 글자로 남는다.
        while (ids.Count > 0)
        {
            var chunk = ids.Take(Chunk).ToList();
            ids.ExceptWith(chunk);
            await EnsureAsync(chunk);
        }
    }

    /// <summary>지금 묻고 있는 아이디와 그 조회.</summary>
    private readonly Dictionary<string, Task> _inflight = new(StringComparer.OrdinalIgnoreCase);

    private async Task FetchAsync(List<string> missing, CancellationToken ct)
    {
        // **묻기 전에 적어 둔다.** 조회가 도는 동안 사람이 조건을 바꾸면
        // 같은 아이디를 또 묻게 된다.
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
            // 화면을 떠났다. **고장이 아니므로 남기지 않는다.**
            Forget(missing);
        }
        catch (Exception ex)
        {
            // **다시 물을 수 있게 되돌린다.** 잠깐 끊긴 것일 수 있고, 그때
            // 적어 둔 채로 두면 이 화면이 사는 동안 얼굴이 영영 안 뜬다.
            Forget(missing);

            logger.LogWarning(ex, "사람의 얼굴을 읽지 못했다. 첫 글자로 그린다.");
        }
        finally
        {
            foreach (var id in missing)
            {
                _inflight.Remove(id);
            }
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
    /// 목록에서 그것이 뜨면 사진이 없는 사람보다 더 나쁘게 보인다.
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
