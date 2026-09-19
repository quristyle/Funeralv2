using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>
/// 「빠른 지시」 화면에서 <b>마지막에 고른 것</b>. 브라우저에 적어 둔다
/// (<c>localStorage</c>).
/// </summary>
/// <remarks>
/// <para>
/// [왜 기억하나]
/// </para>
///
/// <para>
/// 이 화면은 <b>같은 자리에 같은 것을 반복해서 던지는 자리</b>다 — 대상도
/// AI 도 거의 안 바뀐다. 그런데 열 때마다 초기값으로 돌아가면 한 줄 적기 전에
/// 고르는 칸 둘을 먼저 만져야 하고, 그 둘은 <b>모바일에서 목록을 펴서
/// 고르는 칸</b>이다. 「길에서 한 줄 던진다」는 이 화면의 전제가 거기서 깨진다.
/// </para>
///
/// <para>
/// [올리기까지 기억한다]
/// </para>
///
/// <para>
/// 한동안 올리기만은 <b>일부러 안 기억했다</b> — 켠 것을 모르는 채로 다음
/// 건이 운영 배포까지 나가는 것이 무서웠다. 그런데 여기서 시키는 건은 거의 다
/// 「고쳐서 올려 달라」라서, 보낼 때마다 그 칸을 다시 켜야 했다.
/// </para>
///
/// <para>
/// 지금은 기억하되 <b>막는 것을 화면에 남겨 둔다</b> — 켜져 있으면 단추 글자가
/// 「보내고 올리기까지」로 바뀌고(누르기 직전에 눈이 닿는 자리다), 눌러도
/// 묻는 창이 한 번 더 뜬다. 허용하지 않는 대상으로 옮기면 저절로 꺼져 보인다
/// (<c>AiAsk.PushOn</c>).
/// </para>
///
/// <para>
/// [왜 서버가 아니라 브라우저인가]
/// </para>
///
/// <para>
/// 고른 값이 <b>기기마다 다른 것이 맞기</b> 때문이다. 자리에 앉아 쓰는 PC 와
/// 길에서 쓰는 휴대폰은 시키는 것이 다르다. 서버에 담으면 한쪽에서 바꾼 것이
/// 다른 쪽을 따라온다.
/// </para>
///
/// <para>
/// 열쇠에 로그인 아이디가 들어간다 — 공용 PC 에서 남이 고른 대상이 내 화면에
/// 뜨면 안 된다(<see cref="AiTaskDraftStore"/> 와 같은 이유다).
/// </para>
///
/// <para>
/// scoped 다 — 회로 하나가 사용자 한 명의 창 하나다.
/// </para>
/// </remarks>
public sealed class AiAskPrefs(IJSRuntime js, ILogger<AiAskPrefs> logger)
{
    /// <summary>저장소 열쇠의 앞부분. 뒤에 로그인 아이디가 붙는다.</summary>
    public const string KeyPrefix = "jsini-ai-ask-prefs:";

    private string _key = KeyPrefix + "?";
    private bool _read;
    private AiAskPref? _current;

    /// <summary>현재 사용자에게 할당된 localStorage 열쇠.</summary>
    public string CurrentKey => _key;

    /// <summary>
    /// 누구의 것인지 정한다. 화면이 뜰 때 한 번 부른다.
    /// 사람이 바뀌면 다시 읽는다.
    /// </summary>
    public void Use(string? username)
    {
        var key = KeyPrefix + (string.IsNullOrWhiteSpace(username) ? "?" : username.Trim());

        if (key == _key)
        {
            return;
        }

        _key = key;
        _read = false;
        _current = null;
    }

    /// <summary>
    /// 적어 둔 것을 읽는다. <b>회로가 붙은 뒤에</b> 불러야 한다
    /// (<c>OnAfterRenderAsync</c>) — 프리렌더 중에는 JS 를 부를 수 없다.
    ///
    /// <para>
    /// 없거나 깨졌으면 <c>null</c> 이다. <b>그때는 화면이 제 초기값을 쓴다</b> —
    /// 여기서 기본값을 지어내면 「기억한 것」과 「처음 값」이 구별되지 않는다.
    /// </para>
    /// </summary>
    public async Task<AiAskPref?> ReadAsync()
    {
        _read = true;

        string? raw;

        try
        {
            raw = await js.InvokeAsync<string?>("localStorage.getItem", _key);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException)
        {
            // 저장소를 못 쓰는 브라우저다. 기억만 없는 것으로 보고 넘어간다 —
            // 읽기 실패로 화면을 세우지 않는다.
            logger.LogDebug(ex, "빠른 지시 설정을 읽지 못했다.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            _current = JsonSerializer.Deserialize<AiAskPref>(raw);
            return _current;
        }
        catch (JsonException ex)
        {
            logger.LogDebug(ex, "빠른 지시 설정이 깨져 있다.");
            return null;
        }
    }

    /// <summary>
    /// 고른 것을 적어 둔다. <b>읽기 전에는 적지 않는다</b> — 화면이 아직
    /// 기억한 값을 얹지 않은 상태라, 그대로 적으면 초기값이 기억을 덮는다.
    /// </summary>
    public async Task SaveAsync(AiAskPref pref)
    {
        if (!_read)
        {
            return;
        }

        _current = pref;

        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", _key, JsonSerializer.Serialize(pref));
        }
        catch (JSDisconnectedException)
        {
            // 창이 닫혔다. 적을 곳이 없다.
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            // 저장소가 막혔거나 꽉 찼다. 기억이 안 될 뿐이라 화면은 그대로 둔다.
            logger.LogDebug(ex, "빠른 지시 설정을 적지 못했다.");
        }
    }

    /// <summary>
    /// 작성 중인 본문(임시저장)만 따로 갱신한다.
    /// </summary>
    public async Task SaveDraftAsync(string? text)
    {
        if (!_read)
        {
            return;
        }

        _current ??= await ReadAsync() ?? new AiAskPref();

        if (string.IsNullOrWhiteSpace(text))
        {
            _current.DraftText = null;
            _current.DraftSavedAt = null;
        }
        else
        {
            _current.DraftText = text;
            _current.DraftSavedAt = DateTime.Now;
        }

        await SaveAsync(_current);
    }

    /// <summary>
    /// 임시저장된 본문을 비운다. 보냈거나 사용자가 비웠을 때 부른다.
    /// </summary>
    public async Task ClearDraftAsync() => await SaveDraftAsync(null);
}

/// <summary>
/// 기억해 두는 한 벌. 고르는 칸과 작성 중이던 임시본을 담는다.
/// </summary>
public sealed class AiAskPref
{
    public long? TargetKey { get; set; }
    public string? RunnerKind { get; set; }
    public bool AutoPush { get; set; }

    /// <summary>
    /// 메일로 받나. <b>기억이 없으면 켜진 것으로 본다</b>(화면의 초기값) —
    /// 이 화면의 존재 이유가 「답을 메일로 받는 것」이다.
    /// </summary>
    public bool NotifyEmail { get; set; } = true;

    /// <summary>
    /// 작성 중이던 본문(임시저장). 서버 통신 두절이나 새로고침 시 복원에 쓴다.
    /// </summary>
    public string? DraftText { get; set; }

    /// <summary>임시저장된 일시.</summary>
    public DateTime? DraftSavedAt { get; set; }
}
