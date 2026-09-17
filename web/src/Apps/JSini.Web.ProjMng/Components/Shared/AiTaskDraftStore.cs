using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>
/// AI 작업 지시의 <b>임시저장</b>. 브라우저에 적어 둔다(<c>localStorage</c>).
/// </summary>
/// <remarks>
/// <para>
/// [무엇을 고친 것인가]
/// </para>
///
/// <para>
/// 포털의 탭은 <b>주소 목록일 뿐</b>이고 떠난 화면은 그 자리에서 버려진다
/// (<c>PortalTabs</c> 머리말). 그래서 작업 내용을 한참 적다가 다른 탭에 잠깐
/// 다녀오면 <b>적던 것이 통째로 사라졌다.</b> 이 화면은 본문이 길다 — 몇십 분
/// 적은 것이 한 번의 탭 이동으로 없어진다.
/// </para>
///
/// <para>
/// [왜 서버가 아니라 브라우저인가]
/// </para>
///
/// <para>
/// 서버에 적어 두려면 「저장」과 구별되지 않는 저장을 하게 된다 — 쓰다 만 글이
/// 서버의 본문을 덮어쓰고, 그 본문은 <b>실행기가 집어 가는 바로 그 글</b>이다.
/// 임시본은 「아직 서버에 없는 것」이어야 해서 브라우저에 둔다.
/// </para>
///
/// <para>
/// 회로(서버 메모리)에 두지 않는 이유는 그것이 <b>창을 닫으면 함께 사라지기</b>
/// 때문이다. 실수로 창을 닫은 경우를 살리는 것이 이 기능의 절반이다.
/// </para>
///
/// <para>
/// [열쇠에 로그인 아이디가 들어간다]
/// </para>
///
/// <para>
/// 작업 번호는 모두의 것이라, 열쇠를 번호로만 만들면 <b>공용 PC 에서 남의
/// 임시본이 내 화면에 뜬다.</b> <see cref="Use"/> 로 받은 아이디를 열쇠에 섞는다.
/// </para>
///
/// <para>
/// [한 열쇠에 전부 담는다]
/// </para>
///
/// <para>
/// 작업마다 열쇠를 나누면 「무엇이 적혀 있나」를 알려고 저장소를 훑어야 하고,
/// 그건 JS 왕복이 는다는 뜻이다. 한 덩이로 두면 화면이 뜰 때 <b>왕복 한 번</b>에
/// 전부 읽고, 그 뒤로는 우리가 들고 있는 것이 정본이다 — 이 열쇠에 쓰는 화면이
/// 하나뿐이라 그래도 된다.
/// </para>
///
/// <para>
/// scoped 다 — 회로 하나가 사용자 한 명의 창 하나다.
/// </para>
/// </remarks>
public sealed class AiTaskDraftStore(IJSRuntime js, ILogger<AiTaskDraftStore> logger)
{
    /// <summary>저장소 열쇠의 앞부분. 뒤에 로그인 아이디가 붙는다.</summary>
    public const string KeyPrefix = "jsini-ai-task-drafts:";

    /// <summary>
    /// 들고 있을 임시본 수. 넘으면 오래된 것부터 버린다 — 한 사람이 동시에
    /// 손대는 작업이 열 건을 넘지 않는다.
    /// </summary>
    private const int MaxDrafts = 10;

    /// <summary>
    /// 전부 합쳐 이만큼(글자)까지만 들고 있는다.
    ///
    /// <para>
    /// <c>localStorage</c> 는 도메인마다 5MB 안팎이고 그 자리를 탭 고정·공지
    /// 표시와 나눠 쓴다. 넘치면 브라우저가 <b>쓰기를 통째로 거절</b>하므로
    /// (그러면 임시저장이 조용히 안 된다) 그 앞에서 우리가 줄인다.
    /// </para>
    /// </summary>
    private const int MaxChars = 400_000;

    /// <summary>이만큼 지난 임시본은 읽을 때 버린다.</summary>
    private static readonly TimeSpan Keep = TimeSpan.FromDays(7);

    private string _key = KeyPrefix + "?";
    private Dictionary<long, AiTaskDraft> _map = [];
    private bool _read;

    /// <summary>
    /// 브라우저가 저장소를 막고 있다(사생활 보호 모드 · 용량 초과).
    /// <b>화면이 이것을 사람에게 말해야 한다</b> — 임시저장이 안 되는 것을
    /// 모르고 믿는 편이 아예 없는 것보다 나쁘다.
    /// </summary>
    public bool Blocked { get; private set; }

    /// <summary>적어 둔 임시본. 열쇠는 작업 번호이고 <c>0</c> 은 아직 저장 안 한 새 작업이다.</summary>
    public IReadOnlyDictionary<long, AiTaskDraft> Items => _map;

    /// <summary>
    /// 누구의 임시본인지 정한다. 화면이 뜰 때 한 번 부른다.
    /// 사람이 바뀌면 들고 있던 것을 버리고 다시 읽는다.
    /// </summary>
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

    /// <summary>
    /// 적어 둔 것을 읽는다. <b>회로가 붙은 뒤에</b> 불러야 한다
    /// (<c>OnAfterRenderAsync</c>) — 프리렌더 중에는 JS 를 부를 수 없다.
    ///
    /// <para>여러 번 불러도 왕복은 한 번이다.</para>
    /// </summary>
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
            // 저장소를 못 쓰는 브라우저다. 임시저장만 없는 것으로 보고 넘어간다 —
            // 읽기 실패로 화면을 세우지 않는다.
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
            // 적어 둔 것이 깨졌다. 지우고 넘어간다 — 임시본 때문에 화면이
            // 안 열리면 안 된다.
            logger.LogDebug(ex, "임시저장이 깨져 있어 지운다.");
            _map = [];
            await FlushAsync();
            return _map;
        }

        Prune();
        return _map;
    }

    /// <summary>
    /// 임시본 하나를 적어 둔다. <see cref="AiTaskDraft.SavedAt"/> 은 여기서 찍는다 —
    /// 화면이 찍으면 「언제 적힌 것인가」가 화면마다 갈린다.
    /// </summary>
    public Task SaveAsync(AiTaskDraft draft)
    {
        draft.SavedAt = DateTime.Now;
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
            // 용량을 넘겼거나 저장소가 막혀 있다. **말해 준다** — 아래 줄이
            // 없으면 임시저장이 안 되는 것을 아무도 모른다.
            Blocked = true;
            logger.LogDebug(ex, "임시저장을 적지 못했다.");
        }
    }

    /// <summary>
    /// 오래된 것 · 넘치는 것을 버린다. <b>방금 적은 것은 언제나 남는다</b> —
    /// 가장 최근이라 마지막까지 살아남는다.
    /// </summary>
    private void Prune()
    {
        var old = DateTime.Now - Keep;

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

/// <summary>
/// 적어 둔 한 벌. <b>사람이 고치는 칸만</b> 담는다 — 상태·실행 이력처럼
/// 서버가 정하는 값은 담지 않는다(담으면 되살릴 때 옛 상태로 덮어쓴다).
/// </summary>
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

    /// <summary>
    /// 임시본을 뜰 때 서버가 들고 있던 판(<c>RowVersion</c>).
    ///
    /// <para>
    /// 되살릴 때 이 값과 지금 서버의 것을 견준다. 다르면 <b>그 사이 누군가
    /// 저장했다</b>는 뜻이고, 그대로 저장하면 그 사람의 글이 덮인다 —
    /// 화면이 그것을 미리 말해 준다.
    /// </para>
    /// </summary>
    public int BaseRowVersion { get; set; }

    /// <summary>적어 둔 때. <see cref="AiTaskDraftStore.SaveAsync"/> 가 찍는다.</summary>
    public DateTime SavedAt { get; set; }
}
