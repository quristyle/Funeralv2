using Microsoft.JSInterop;

namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>
/// 「이어서 지시」 창에 적다 만 글의 <b>임시저장</b>. 브라우저에 적어 둔다
/// (<c>localStorage</c>).
/// </summary>
/// <remarks>
/// <para>
/// [무엇을 고친 것인가]
/// </para>
///
/// <para>
/// 끝난 건에 이어서 시킬 것을 적는 자리는 <b>창</b>이다. 그래서 새로고침
/// 한 번이면 창째 사라지고 적던 글도 함께 없어졌다 — 회선이 흔들려 Blazor 가
/// 스스로 화면을 다시 띄우는 경우에도 같다. 「빠른 지시」의 큰 글상자는
/// 이미 막아 두었는데(<see cref="AiAskPrefs"/>) 이 창만 빠져 있었다.
/// </para>
///
/// <para>
/// [적는 일은 전부 JS 가 한다]
/// </para>
///
/// <para>
/// 이 클래스는 <c>js/continue-draft.js</c> 를 부르는 얇은 껍데기다. 저장소를
/// 직접 만지지 않는 까닭은 둘이다.
/// </para>
///
/// <list type="number">
/// <item>
/// <b>연결이 끊긴 동안 친 글자가 살아야 한다.</b> C# 은 이벤트가 서버까지
/// 와야 값을 아는데, 회선이 끊기면 그 이벤트가 오지 않는다. 브라우저의
/// <c>input</c> 은 연결과 무관하게 돈다.
/// </item>
/// <item>
/// <b>덮어쓰기를 막는다.</b> 한 열쇠에 여러 건이 담기므로, C# 이 제 손에 든
/// 덩어리를 통째로 다시 적으면 그 사이 브라우저가 적어 둔 방금 친 글자가
/// 사라진다 — 고치려던 바로 그 사고다. 고쳐 쓰는 길을 JS 한 곳으로 모아
/// <b>읽고-합치고-적기</b>를 한 번에 시킨다.
/// </item>
/// </list>
///
/// <para>
/// [열쇠에 로그인 아이디가 들어간다]
/// </para>
///
/// <para>
/// 「빠른 지시」는 관리자에게 <b>모두의 카드</b>를 보여 준다. 열쇠를 작업
/// 번호로만 만들면 공용 PC 에서 남이 적다 만 글이 내 창에 뜬다
/// (<see cref="AiTaskDraftStore"/> 와 같은 이유다).
/// </para>
///
/// <para>
/// scoped 다 — 회로 하나가 사용자 한 명의 창 하나다.
/// </para>
/// </remarks>
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

    /// <summary>
    /// 적어 둔 한 건을 읽는다. 없거나 못 읽으면 <c>null</c> 이다 —
    /// <b>그때는 창이 빈 글상자로 열린다.</b>
    /// </summary>
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

    /// <summary>
    /// 글상자에 실시간 감시를 건다. <b>창이 그려진 뒤에</b> 불러야 한다
    /// (<c>OnAfterRenderAsync</c>) — 그 전에는 글상자가 DOM 에 없다.
    /// </summary>
    /// <param name="selector">글상자를 감싼 상자를 가리키는 CSS 선택자.</param>
    /// <param name="taskKey">어느 건에 이어서 지시하는 중인가.</param>
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

    /// <summary>
    /// 모듈을 한 번만 읽어 둔다. <b>못 읽어도 창은 열려야 한다</b> —
    /// 임시저장이 없는 것은 불편이지만, 여기서 터지면 이어서 지시 자체를 못 한다.
    /// </summary>
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

/// <summary>
/// 적어 둔 한 벌. <b>사람이 고치는 칸만</b> 담는다.
/// </summary>
/// <remarks>
/// 칸 이름이 <c>continue-draft.js</c> 가 적는 것과 같아야 한다.
/// </remarks>
public sealed class AiContinueDraft
{
    /// <summary>이번에 더 시킬 일. 이것이 비면 임시본 자체가 없는 것으로 본다.</summary>
    public string? Addition { get; set; }

    /// <summary>창에서 골라 둔 AI. 없으면 지난 회차의 것을 그대로 쓴다.</summary>
    public string? RunnerKind { get; set; }

    /// <summary>적어 둔 때. 브라우저가 <b>현지 시각</b>으로 찍는다.</summary>
    public DateTime? SavedAt { get; set; }
}
