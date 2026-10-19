using Microsoft.JSInterop;

namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>「이어서 지시」 창에 적다 만 글의 임시저장. 브라우저(localStorage)에 적는다 — 창은 새로고침에 사라지고 회선이 흔들려도 다시 뜨므로. 적는 일은 전부 JS 가 한다: 연결이 끊긴 동안 친 글자가 살아야 하고, 한 열쇠에 여러 건이 담기므로 C# 이 덩어리를 통째로 다시 적으면 그사이 친 글자가 사라진다. 열쇠에 로그인 아이디를 넣어 공용 PC 에서 남의 글이 뜨지 않게 한다.</summary>
public sealed class AiContinueDraftStore(IJSRuntime js)
{
    /// <summary>저장소 열쇠의 앞부분. 뒤에 로그인 아이디가 붙는다.</summary>
    public const string KeyPrefix = "jsini-ai-continue-drafts:";

    private const string ModulePath = "./_content/JSini.Web.ProjMng/js/continue-draft.js";

    private IJSObjectReference? _module;
    private string _key = KeyPrefix + "?";

    /// <summary>현재 사용자에게 할당된 <c>localStorage</c> 열쇠.</summary>
    public string CurrentKey => _key;

    /// <summary>누구의 것인지 정한다. 창을 열 때 부른다.</summary>
    public void Use(string? username) =>
        _key = KeyPrefix + (string.IsNullOrWhiteSpace(username) ? "?" : username.Trim());

    /// <summary>적어 둔 한 건을 읽는다. 없거나 못 읽으면 null 이고, 그때 창은 빈 글상자로 열린다.</summary>
    public async Task<AiContinueDraft?> ReadAsync(long taskKey)
    {
        var module = await ModuleAsync();

        if (module is null)
        {
            return null;
        }

        try
        {
            return await module.InvokeAsync<AiContinueDraft?>("read", _key, taskKey);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>글상자에 실시간 감시를 건다(창이 그려진 뒤에 불러야 한다). selector 는 글상자를 감싼 상자, taskKey 는 이어서 지시하는 건이다.</summary>
    public async Task AttachAsync(string selector, long taskKey)
    {
        var module = await ModuleAsync();

        if (module is null)
        {
            return;
        }

        try
        {
            await module.InvokeVoidAsync("attach", selector, _key, taskKey);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException)
        {
        }
    }

    /// <summary>이 회차를 맡을 AI 를 적어 둔다. 적은 글이 없으면 아무것도 남지 않는다.</summary>
    public async Task SaveKindAsync(long taskKey, string? runnerKind)
    {
        var module = await ModuleAsync();

        if (module is null)
        {
            return;
        }

        try
        {
            await module.InvokeVoidAsync("saveKind", _key, taskKey, runnerKind);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException)
        {
        }
    }

    /// <summary>한 건의 임시본을 버리고 글상자도 비운다.</summary>
    public async Task ClearAsync(string? selector, long taskKey)
    {
        var module = await ModuleAsync();

        if (module is null)
        {
            return;
        }

        try
        {
            await module.InvokeVoidAsync("clear", selector, _key, taskKey);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException)
        {
        }
    }

    /// <summary>모듈을 한 번만 읽어 둔다. 못 읽어도 창은 열려야 한다 — 임시저장이 없는 것은 불편이지만 여기서 터지면 이어서 지시를 못 한다.</summary>
    private async ValueTask<IJSObjectReference?> ModuleAsync()
    {
        if (_module is not null)
        {
            return _module;
        }

        try
        {
            _module = await js.InvokeAsync<IJSObjectReference>("import", ModulePath);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException)
        {
            return null;
        }

        return _module;
    }
}

/// <summary>적어 둔 한 벌. 사람이 고치는 칸만 담는다. 칸 이름은 continue-draft.js 가 적는 것과 같아야 한다.</summary>
public sealed class AiContinueDraft
{
    /// <summary>이번에 더 시킬 일. 이것이 비면 임시본 자체가 없는 것으로 본다.</summary>
    public string? Addition { get; set; }

    /// <summary>창에서 골라 둔 AI. 없으면 지난 회차의 것을 그대로 쓴다.</summary>
    public string? RunnerKind { get; set; }

    /// <summary>적어 둔 때. 브라우저가 <b>현지 시각</b>으로 찍는다.</summary>
    public DateTime? SavedAt { get; set; }
}
