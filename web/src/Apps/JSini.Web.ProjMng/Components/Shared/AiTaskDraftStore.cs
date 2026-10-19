using JSini.Web.Components.Data;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>AI 작업 지시의 임시저장. 브라우저(localStorage)에 적어 둔다 — 포털 탭을 옮기면 화면이 버려져 적던 글을 살린다. 서버에 두면 쓰다 만 글이 실행기가 집어 가는 본문을 덮는다.
/// 열쇠에 로그인 아이디를 섞는다(작업 번호는 모두의 것이라 공용 PC 에서 남의 임시본이 뜬다). 한 열쇠에 전부 담아 읽기 왕복을 한 번으로 한다. scoped — 회로 하나가 창 하나다.</summary>
public sealed class AiTaskDraftStore(IJSRuntime js, ILogger<AiTaskDraftStore> logger)
{
    /// <summary>저장소 열쇠의 앞부분. 뒤에 로그인 아이디가 붙는다.</summary>
    public const string KeyPrefix = "jsini-ai-task-drafts:";

    /// <summary>들고 있을 임시본 수. 넘으면 오래된 것부터 버린다.</summary>
    private const int MaxDrafts = 10;

    /// <summary>전부 합쳐 이만큼(글자)까지만 든다. localStorage 는 도메인마다 5MB 안팎이고 탭 고정·공지 표시와 나눠 쓴다 — 넘치면 브라우저가 쓰기를 통째로 거절하므로 그 앞에서 줄인다.</summary>
    private const int MaxChars = 400_000;

    /// <summary>이만큼 지난 임시본은 읽을 때 버린다.</summary>
    private static readonly TimeSpan Keep = TimeSpan.FromDays(7);

    private string _key = KeyPrefix + "?";
    private Dictionary<long, AiTaskDraft> _map = [];
    private bool _read;

    /// <summary>브라우저가 저장소를 막고 있다(사생활 보호 모드 · 용량 초과). 화면이 이것을 사람에게 말해야 한다 — 안 되는 것을 모르고 믿는 편이 아예 없는 것보다 나쁘다.</summary>
    public bool Blocked { get; private set; }

    /// <summary>적어 둔 임시본. 열쇠는 작업 번호이고 <c>0</c> 은 아직 저장 안 한 새 작업이다.</summary>
    public IReadOnlyDictionary<long, AiTaskDraft> Items => _map;

    /// <summary>누구의 임시본인지 정한다. 사람이 바뀌면 들고 있던 것을 버리고 다시 읽는다.</summary>
    public void Use(string? username)
    {
        var key = KeyPrefix + (string.IsNullOrWhiteSpace(username) ? "?" : username.Trim());

        if (key == _key)
        {
            return;
        }

        _key = key;
        _map = [];
        _read = false;
    }

    /// <summary>적어 둔 것을 읽는다. 회로가 붙은 뒤에 불러야 한다(프리렌더에서는 JS 를 부를 수 없다). 여러 번 불러도 왕복은 한 번이다.</summary>
    public async Task<IReadOnlyDictionary<long, AiTaskDraft>> ReadAsync()
    {
        if (_read)
        {
            return _map;
        }

        _read = true;

        string? raw;

        try
        {
            raw = await js.InvokeAsync<string?>("localStorage.getItem", _key);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException)
        {
            // 저장소를 못 쓰는 브라우저다 — 임시저장만 없는 것으로 보고 읽기 실패로 화면을 세우지 않는다.
            Blocked = true;
            logger.LogDebug(ex, "임시저장을 읽지 못했다.");
            return _map;
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            return _map;
        }

        try
        {
            _map = JsonSerializer.Deserialize<Dictionary<long, AiTaskDraft>>(raw) ?? [];
        }
        catch (JsonException ex)
        {
            // 적어 둔 것이 깨졌다 — 지우고 넘어간다. 임시본 때문에 화면이 안 열리면 안 된다.
            logger.LogDebug(ex, "임시저장이 깨져 있어 지운다.");
            _map = [];
            await FlushAsync();
            return _map;
        }

        Prune();
        return _map;
    }

    /// <summary>임시본 하나를 적어 둔다. SavedAt 은 여기서 찍는다 — 화면이 찍으면 「언제 적힌 것인가」가 화면마다 갈린다.</summary>
    public Task SaveAsync(AiTaskDraft draft)
    {
        draft.SavedAt = AppTime.UtcNow;
        _map[draft.TaskKey] = draft;

        Prune();
        return FlushAsync();
    }

    /// <summary>임시본 하나를 버린다. 없으면 아무 일도 하지 않는다(왕복도 없다).</summary>
    public Task RemoveAsync(long taskKey) =>
        _map.Remove(taskKey) ? FlushAsync() : Task.CompletedTask;

    private async Task FlushAsync()
    {
        try
        {
            if (_map.Count == 0)
            {
                await js.InvokeVoidAsync("localStorage.removeItem", _key);
            }
            else
            {
                await js.InvokeVoidAsync("localStorage.setItem", _key, JsonSerializer.Serialize(_map));
            }

            Blocked = false;
        }
        catch (JSDisconnectedException)
        {
            // 창이 닫혔다. 적을 곳이 없고, 적을 것도 이 회로와 함께 사라진다.
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            // 용량을 넘겼거나 저장소가 막혀 있다. 말해 줘야 한다 — 아니면 임시저장이 안 되는 것을 아무도 모른다.
            Blocked = true;
            logger.LogDebug(ex, "임시저장을 적지 못했다.");
        }
    }

    /// <summary>오래된 것과 넘치는 것을 버린다. 방금 적은 것은 가장 최근이라 마지막까지 남는다.</summary>
    private void Prune()
    {
        var old = AppTime.UtcNow - Keep;

        foreach (var key in _map.Where(p => p.Value.SavedAt < old).Select(p => p.Key).ToList())
        {
            _map.Remove(key);
        }

        var ordered = _map.OrderByDescending(p => p.Value.SavedAt).Select(p => p.Key).ToList();

        var chars = 0;

        for (var i = 0; i < ordered.Count; i++)
        {
            chars += _map[ordered[i]].Contents?.Length ?? 0;

            // 첫 하나는 아무리 길어도 남긴다 — 방금 적은 것이다.
            if (i >= MaxDrafts || (i > 0 && chars > MaxChars))
            {
                _map.Remove(ordered[i]);
            }
        }
    }
}

/// <summary>적어 둔 한 벌. 사람이 고치는 칸만 담는다 — 서버가 정하는 값(상태·실행 이력)을 담으면 되살릴 때 옛 상태로 덮어쓴다.</summary>
public sealed class AiTaskDraft
{
    /// <summary>어느 작업의 것인가. <c>0</c> 은 아직 저장하지 않은 새 작업이다.</summary>
    public long TaskKey { get; set; }

    public string? Title { get; set; }
    public string? Contents { get; set; }
    public long? TargetKey { get; set; }
    public string? RunnerKind { get; set; }
    public int TimeoutMinutes { get; set; }
    public bool AutoPush { get; set; }
    public bool NotifyEmail { get; set; }
    public string? NotifyTo { get; set; }
    public string? NotifyWhen { get; set; }

    /// <summary>임시본을 뜰 때 서버가 들고 있던 판(RowVersion). 되살릴 때 지금 서버의 것과 견줘, 다르면 그 사이 누군가 저장한 것이라 화면이 미리 말해 준다.</summary>
    public int BaseRowVersion { get; set; }

    /// <summary>적어 둔 때. <see cref="AiTaskDraftStore.SaveAsync"/> 가 찍는다.</summary>
    public DateTime SavedAt { get; set; }
}
