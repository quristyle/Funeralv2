using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace JSini.Web.ProjMng.Components.Shared;

public partial class MailHtmlEditor
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>본문 HTML.</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>사람이 고쳤을 때만 온다. 바깥에서 Value 를 바꾼 것은 다시 알리지 않는다.</summary>
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }

    /// <summary>편집 칸 높이(CSS 길이).</summary>
    [Parameter] public string Height { get; set; } = "420px";

    private ElementReference _frame;
    private ElementReference _toolbar;
    private IJSObjectReference? _module;
    private DotNetObjectReference<MailHtmlEditor>? _self;

    /// <summary>iframe 에 실은 문서. 사람이 고친 것으로는 안 바꾼다 — 바꾸면 문서가 새로 서며 커서가 날아간다.</summary>
    private string _doc = string.Empty;

    /// <summary>지금의 값. 편집기가 스스로 낸 것과 바깥에서 들어온 것을 가르는 기준이다.</summary>
    private string? _last;

    private bool _source;

    /// <summary>「HTML」 탭이면 참. 치환자 넣기가 커서 자리를 못 잡는 자리다.</summary>
    public bool IsSource => _source;

    protected override void OnParametersSet()
    {
        if (Value != _last)
        {
            _last = Value;
            _doc = Value ?? string.Empty;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        try
        {
            _module = await JS.InvokeAsync<IJSObjectReference>("import", "./_content/JSini.Web.ProjMng/js/mail-html-editor.js");
            _self = DotNetObjectReference.Create(this);
            await _module.InvokeAsync<bool>("attach", _frame, _toolbar, _self);
        }
        catch (JSDisconnectedException) { }
    }

    [JSInvokable]
    public Task OnEdited(string html)
    {
        _last = html;
        return ValueChanged.InvokeAsync(html);
    }

    private Task OnSourceChangedAsync(string? html)
    {
        _last = html;
        _doc = html ?? string.Empty;
        return ValueChanged.InvokeAsync(html);
    }

    private async Task SetSourceAsync(bool source)
    {
        if (_source == source) return;

        // 원문으로 넘어가기 전에 편집 칸에서 마지막으로 친 것을 걷어 온다 — 모아 올리는 사이의 글자가 빠진다.
        if (source) await FlushAsync();

        _source = source;
    }

    /// <summary>지금 화면의 본문. 보내기·저장 직전에 부른다.</summary>
    public async Task<string?> FlushAsync()
    {
        if (_source || _module is null) return _last;

        try
        {
            var html = await _module.InvokeAsync<string?>("read", _frame);

            if (html is not null && html != _last)
            {
                _last = html;
                await ValueChanged.InvokeAsync(html);
            }
        }
        catch (JSDisconnectedException) { }

        return _last;
    }

    /// <summary>커서 자리에 글자를 넣는다. 「HTML」 탭이면 못 넣고 거짓을 준다.</summary>
    public async Task<bool> InsertAsync(string text)
    {
        if (_source || _module is null) return false;

        try
        {
            return await _module.InvokeAsync<bool>("insertText", _frame, text);
        }
        catch (JSDisconnectedException)
        {
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("detach", _frame);
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException) { }
        }

        _self?.Dispose();
    }
}
