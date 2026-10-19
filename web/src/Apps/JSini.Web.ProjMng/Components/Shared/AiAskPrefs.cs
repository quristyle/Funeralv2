using JSini.Web.Components.Data;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>「빠른 지시」 화면에서 마지막에 고른 것을 브라우저(localStorage)에 적어 둔다. 기기마다 다른 것이 맞아 서버에 담지 않고, 열쇠에 로그인 아이디를 넣어 공용 PC 에서 남의 대상이 뜨지 않게 한다. 올리기는 대상이 허용할 때만 켜져 있다.</summary>
public sealed class AiAskPrefs(IJSRuntime js, ILogger<AiAskPrefs> logger)
{
    /// <summary>저장소 열쇠의 앞부분. 뒤에 로그인 아이디가 붙는다.</summary>
    public const string KeyPrefix = "jsini-ai-ask-prefs:";

    private string _key = KeyPrefix + "?";
    private bool _read;
    private AiAskPref? _current;

    /// <summary>현재 사용자에게 할당된 localStorage 열쇠.</summary>
    public string CurrentKey => _key;

    /// <summary>누구의 것인지 정한다. 화면이 뜰 때 한 번 부르고, 사람이 바뀌면 다시 읽는다.</summary>
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

    /// <summary>적어 둔 것을 읽는다. 회로가 붙은 뒤에 불러야 한다. 없거나 깨졌으면 null 이고, 그때 화면이 제 초기값을 쓴다 — 여기서 기본값을 지어내면 기억과 처음 값이 구별되지 않는다.</summary>
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
            // 저장소를 못 쓰는 브라우저다. 기억만 없는 것으로 보고 넘어간다.
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

    /// <summary>고른 것을 적어 둔다. 읽기 전에는 적지 않는다 — 기억을 얹기 전에 적으면 초기값이 기억을 덮는다.</summary>
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

    /// <summary>작성 중인 본문(임시저장)만 따로 갱신한다.</summary>
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
            _current.DraftSavedAt = AppTime.UtcNow;
        }

        await SaveAsync(_current);
    }

    /// <summary>임시저장된 본문을 비운다. 보냈거나 사용자가 비웠을 때 부른다.</summary>
    public async Task ClearDraftAsync() => await SaveDraftAsync(null);
}

/// <summary>기억해 두는 한 벌. 고르는 칸과 작성 중이던 임시본을 담는다.</summary>
public sealed class AiAskPref
{
    public long? TargetKey { get; set; }
    public string? RunnerKind { get; set; }
    public bool AutoPush { get; set; }

    /// <summary>메일로 받나. 기억이 없으면 켜진 것으로 본다 — 이 화면의 존재 이유가 답을 메일로 받는 것이다.</summary>
    public bool NotifyEmail { get; set; } = true;

    /// <summary>PWA 알림을 받나.</summary>
    public bool NotifyPwa { get; set; } = true;

    /// <summary>아래 카드를 몇 단으로 깔 것인가(1 · 2 · 3). 기억이 없으면 둘이다. 기기마다 맞는 값이라 여기 둔다. 범위를 넘는 값(0 등)이 들어와도 읽는 쪽이 1~3 으로 맞춘다 — 단이 0개인 격자는 카드가 안 그려진다.</summary>
    public int ListCols { get; set; } = 2;

    /// <summary>작성 중이던 본문(임시저장). 서버 통신 두절이나 새로고침 시 복원에 쓴다.</summary>
    public string? DraftText { get; set; }

    /// <summary>임시저장된 일시.</summary>
    public DateTime? DraftSavedAt { get; set; }
}
