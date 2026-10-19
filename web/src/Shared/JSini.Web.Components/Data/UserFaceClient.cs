using JSini.Web.Http;
using Microsoft.Extensions.Logging;

namespace JSini.Web.Components.Data;

/// <summary>로그인 아이디로 사람의 이름과 얼굴을 푼다(auth/user/faces). 세 번째 모듈이 쓰게 되어 여기로 올렸다 — 두 모듈이 쓰면 복제, 세 번째부터 승격이 저장소의 규칙이다. 업무 DB 는 로그인 아이디만 알고 이름과 사진의 정본은 포털 계정이라 화면이 한 번 더 묻는다(업무 표에 이름을 베끼면 같은 사람이 화면마다 다른 이름으로 선다).
/// 한 번 물어본 것은 다시 묻지 않는다 — 아는 것은 들고 모르는 아이디만 모아 한 번에 묻는다. 못 찾은 아이디도 기억한다: 없는 사람 하나 때문에 조회가 영영 반복되는 것이 가장 흔한 무한 조회다. scoped — 회로 하나가 사용자 한 명이다.</summary>
public sealed class UserFaceClient(GatewayClient gateway, ILogger<UserFaceClient> logger)
{
    private const string Url = "auth/user/faces";

    /// <summary>한 번에 물어보는 상한. 서버도 같은 수에서 자른다(AuthServer UserEndpoints 의 MaxFaces).</summary>
    private const int Chunk = 100;

    private readonly Dictionary<string, UserFace> _known = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>이미 물어본 아이디. <b>못 찾은 것도 들어 있다.</b></summary>
    private readonly HashSet<string> _asked = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>아는 얼굴. 아직 안 물었거나 없는 사람이면 null — 화면은 그때 첫 글자를 그린다.</summary>
    public UserFace? Get(string? loginId)
        => !string.IsNullOrWhiteSpace(loginId) && _known.TryGetValue(loginId.Trim(), out var face)
            ? face
            : null;

    /// <summary>이 사람의 사진 주소. 모르거나 없으면 <c>null</c>.</summary>
    public string? PhotoOf(string? loginId) => Get(loginId)?.Photo;

    /// <summary>이 사람의 이름. 모르면 <c>null</c> — 부르는 쪽이 아이디로 물러선다.</summary>
    public string? NameOf(string? loginId) => Get(loginId)?.Name;

    /// <summary>이 아이디들의 얼굴을 채워 둔다. 모르는 것만 묻는다. 실패는 삼킨다 — 얼굴이 안 뜨는 것과 목록이 안 뜨는 것은 무게가 다르다.</summary>
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

    /// <summary>아바타 하나가 제 얼굴을 묻는다. 같은 그림 차례에 물은 것들을 모아 한 번에 보낸다 — 목록 스무 줄이 저마다 부르면 왕복도 스물이 된다. 형제 부품들의 OnParametersSetAsync 는 한 그림 차례 안에서 첫 await 까지 잇달아 돌아, 한 번 양보하고 보내면 줄 선 아이디가 한 묶음이 된다.</summary>
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

    /// <summary>사진을 못 받아 왔다. 주소를 버리고 첫 글자로 돌아간다 — 깨진 그림 아이콘은 사진 없는 사람보다 나쁘게 보인다.</summary>
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
