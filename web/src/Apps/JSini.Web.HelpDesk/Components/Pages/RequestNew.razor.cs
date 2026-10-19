using Microsoft.AspNetCore.Components;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using DevExpress.Blazor;
using JSini.Web.Http;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.HelpDesk.Api;
using JSini.Web.HelpDesk.Components.Shared;

namespace JSini.Web.HelpDesk.Components.Pages;

public partial class RequestNew
{
    [Inject] private HelpDeskApi Api { get; set; } = default!;
    [Inject] private HelpDeskContext Context { get; set; } = default!;
    [Inject] private ContentImages Images { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>편집기를 짚는 이름. JS 와 CSS 가 같은 글자를 본다.</summary>
    private const string EditorSelector = ".hd-content-editor";

    /// <summary>숨겨 둔 파일 칸의 id. JS 가 이 이름으로 찾아 그림을 밀어 넣는다.</summary>
    private const string PasteInputId = "hd-paste-input";

    /// <summary>제목 칸을 짚는 이름. 쓰던 글을 적어 두는 JS 가 이 안에서 입력칸을 찾는다.</summary>
    private const string TitleSelector = ".hd-title-field";

    /// <summary>쓰던 글을 적어 두는 열쇠의 앞부분. 뒤에 포털 로그인 아이디가 붙는다 — 없으면 공용 PC 에서 남이 쓰다 만 글이 뜬다.</summary>
    private const string DraftKeyPrefix = "jsini-hd-request-draft:";

    [CascadingParameter] private Task<AuthenticationState>? AuthState { get; set; }

    /// <summary>도구줄 「그림」 단추의 이름표. JS 가 이 이름으로 알아본다 — 좁은 화면에서는 넘침 메뉴로 옮겨 가 자리로는 찾을 수 없다.</summary>
    private const string PictureButtonClass = "hd-pick-image";

    /// <summary>휴대폰 도구줄에 세우는 명령 아홉. 차례가 곧 화면의 차례다.
    /// 글꼴·색·첨자·표·맞춤은 뺐다 — 이 화면의 글은 서식이 아니라 캡처와 몇 줄이다.</summary>
    private static readonly string[] PhoneToolbarItems =
    [
        HtmlEditorToolbarItemNames.Undo,
        HtmlEditorToolbarItemNames.Redo,
        HtmlEditorToolbarItemNames.FontBold,
        HtmlEditorToolbarItemNames.FontUnderline,
        HtmlEditorToolbarItemNames.ClearFormatting,
        HtmlEditorToolbarItemNames.BulletList,
        HtmlEditorToolbarItemNames.NumberedList,
        HtmlEditorToolbarItemNames.ShowHyperlinkDialog,
        HtmlEditorToolbarItemNames.ShowInsertPictureDialog,
    ];

    /// <summary>본문에 붙이는 그림 한 장의 상한. ContentImages.MaxBytes 하나를 댓글 칸과 같이 쓴다 — 두 벌이면 화면마다 다른 크기에서 막힌다.</summary>
    private const long MaxImageBytes = ContentImages.MaxBytes;

    /// <summary>한 번에 붙여넣을 수 있는 장수.</summary>
    private const int MaxPastedImages = 10;

    private string? _title;
    private string _content = string.Empty;
    private bool _saving;

    /// <summary>지금 올리고 있는 그림 수. 0 이 아니면 「등록」을 잠근다.</summary>
    private int _pasting;

    private FilePicker? _picker;
    private IReadOnlyList<PickedFile> _files = [];

    private IJSObjectReference? _editorJs;

    /// <summary>붙여넣기 처리기를 걸었는가.</summary>
    private bool _bound;

    /// <summary>붙여넣기 처리기를 거는 중이다. 기다리는 동안 다음 그리기가 또 걸지 않게 막는다.</summary>
    private bool _binding;

    private const int BindAttempts = 20;

    private static readonly TimeSpan BindWait = TimeSpan.FromMilliseconds(250);

    /// <summary>편집기 한 벌에만 붙는 이름. <c>@key</c> 와 같은 값으로 짓는다.</summary>
    private string EditorInstanceClass => $"hd-content-editor--{(_isPhone ? "p" : "w")}{_editorKey}";

    /// <summary>편집기를 다시 세우는 열쇠. 되살리기나 지우고 새로 쓰기 때 바꾼다.</summary>
    private int _editorKey;

    // ── 쓰던 글 (임시 보관) ──────────────────────────────────

    private IJSObjectReference? _draftJs;

    /// <summary>JS 가 「적히지 않는다」를 알려 올 때 쓰는 손잡이.</summary>
    private DotNetObjectReference<RequestNew>? _self;

    /// <summary>누구의 것인가. <see cref="ReadDraftAsync"/> 에서 정해진다.</summary>
    private string _draftKey = DraftKeyPrefix + "?";

    /// <summary>적어 둔 것을 읽어 봤는가. <b>화면마다 한 번</b>이다.</summary>
    private bool _draftRead;

    /// <summary>지켜보기를 걸었는가.</summary>
    private bool _draftWatched;

    /// <summary>지켜보기를 영영 거두었는가(등록 성공). 없으면 거둔 다음 그리기에 다시 걸려 등록한 글이 되살아난다.</summary>
    private bool _draftStopped;

    /// <summary>되살린 글이 적힌 때. null 이면 되살린 것이 없다 — 안내 줄과 「지우고 새로 쓰기」가 이 값을 본다.</summary>
    private DateTime? _restoredAt;

    /// <summary>브라우저가 적기를 거절했다(사생활 보호 · 용량 초과 · 본문이 큼). 화면이 이것을 말해야 한다.
    /// 모르고 믿는 편이 아예 없는 것보다 나쁘다.</summary>
    private bool _draftBlocked;

    protected override async Task OnInitializedAsync()
    {
        await Context.LoadIdentityAsync();

    }

    /// <summary>도구줄을 우리 것으로 손본다 — 한국어 이름표 · 「그림」 단추 이름표 · 휴대폰 전용 도구줄.
    /// 휴대폰에서는 접히는 묶음을 없애고 바로 눌리는 아홉 단추 묶음 하나만 세운다(넘침 단추가 안 생기는 수). DevExpress 드롭다운은 포털 안에서 열리지 않는다.</summary>
    private void CustomizeEditorToolbar(DevExpress.Blazor.Office.IToolbar toolbar)
    {
        // 기본 묶음 전부 — 넓은 화면에서만 선다.
        for (var g = 0; g < toolbar.Groups.Count; g++)
        {
            toolbar.Groups[g].GetVisible = () => !_isPhone;
        }

        // 휴대폰 묶음. 표준 명령을 이름으로 담는다 — 아이콘·활성 상태·실행취소 이력이 편집기 것 그대로 따라온다.
        var phone = toolbar.Groups.AddCustomGroup();
        phone.GetVisible = () => _isPhone;

        DevExpress.Blazor.Office.IBarItem? phonePicture = null;

        foreach (var name in PhoneToolbarItems)
        {
            var item = phone.Items.Add(name);

            if (name == HtmlEditorToolbarItemNames.ShowInsertPictureDialog)
            {
                phonePicture = item;
            }
        }

        // 한국어 이름표는 맨 나중에 입힌다 — 휴대폰 단추도 함께 훑어야 하고 이 칸이 모든 칸을 돈다.
        // CssClass 는 그 뒤에 적어야 남는다.
        HtmlEditorKorean.Apply(toolbar);

        // 「그림」은 기본 묶음과 휴대폰 묶음 두 자리에 있고 둘 다 같은 이름을 달아야 한다.
        // JS 가 이름으로 누름을 가로채므로 한쪽을 빠뜨리면 편집기 제 판이 열려 사진이 base64 로 박힌다.
        var picture = toolbar.Groups[HtmlEditorToolbarGroupNames.InsertElement]
            ?.Items[HtmlEditorToolbarItemNames.ShowInsertPictureDialog];

        if (picture is not null)
        {
            picture.CssClass = PictureButtonClass;
        }

        if (phonePicture is not null)
        {
            phonePicture.CssClass = PictureButtonClass;
        }
    }

    /// <summary>도구줄을 손보는 대리자. 한 번 만들어 들고 있는다 — @CustomizeEditorToolbar 로 적으면 그릴 때마다 새 대리자가 되어 도구줄을 다시 세운다.</summary>
    private Action<DevExpress.Blazor.Office.IToolbar>? _customizeToolbar;

    private Action<DevExpress.Blazor.Office.IToolbar> CustomizeToolbar =>
        _customizeToolbar ??= CustomizeEditorToolbar;

    /// <summary>휴대폰 폭인가(≤767px). DxLayoutBreakpoint 가 채운다 — DeviceSize.XSmall(576px)을 쓰면 helpdesk.css 의 휴대폰 갈래와 어긋난다.</summary>
    private bool _isPhone;

    /// <summary>폭이 경계를 넘나들었다. 편집기를 다시 세운다(@key) — GetVisible 은 도구줄이 다시 그려질 때만 다시 읽혀 그대로 두면 접힌 묶음이 남는다.
    /// 다시 세우기 전에 글을 걷고(FlushContentAsync), 붙여넣기·임시 보관 걸이를 푼다 — 새 편집기에는 안 걸려 있어 다시 걸리지 않으면 그림이 base64 로 박힌다.</summary>
    private async Task OnPhoneChangedAsync(bool isPhone)
    {
        if (_isPhone == isPhone)
        {
            return;
        }

        // 빈 것으로는 덮지 않는다 — 되살린 글이 얹히기 전에 폭이 갈리면 빈 칸을 읽어 그 글을 지운다.
        await FlushContentAsync(keepIfBlank: true);

        _isPhone = isPhone;

        // 새 글칸에는 아무것도 안 걸려 있으니 다시 건다.
        _bound = false;
        _draftWatched = false;

        StateHasChanged();
    }

    /// <summary>편집기에 붙여넣기 처리기를 건다. 걸릴 때까지 그리기마다 다시 보고 걸리면 그만 본다.
    /// 연결이 없어도 쓸 수 있으므로 Context.CanUse 로 돌려보내지 않는다(고객 줄은 서버가 첫 글에 만든다).</summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // 적어 둔 것부터 읽는다 — 얹으면 화면을 한 번 더 그려야 들어가므로 얹는 일은 다음 차례로 미룬다.
        if (!_draftRead)
        {
            await ReadDraftAsync();
            return;
        }

        await BindEditorAsync();
        await WatchDraftAsync();
    }

    /// <summary>편집기에 붙여넣기·끌어놓기·「그림」 단추 처리기를 건다.</summary>
    private async Task BindEditorAsync()
    {
        if (_bound || _binding)
        {
            return;
        }

        _binding = true;
        try
        {
            _editorJs ??= await JS.InvokeAsync<IJSObjectReference>(
                "import", "./_content/JSini.Web.HelpDesk/js/request-editor.js");

            // 글칸이 설 때까지 여기서 기다린다 — 다음 그리기를 기다리면 회선이 느릴 때 영영 안 걸린다. 한 벌의 이름으로 짚는다.
            for (var attempt = 0; attempt < BindAttempts && !_bound; attempt++)
            {
                var selector = "." + EditorInstanceClass;

                var bound = await _editorJs.InvokeAsync<bool>(
                    "bind", selector, PasteInputId, "." + PictureButtonClass);

                _bound = bound && selector == "." + EditorInstanceClass;

                if (!_bound)
                {
                    await Task.Delay(BindWait);
                }
            }
        }
        catch (JSException)
        {
            // 브라우저에서 못 걸었다 — 붙여넣기는 편집기 기본 동작(data URI)으로 떨어지고, 등록 직전의 훑기가 파일로 바꾼다.
        }
        catch (InvalidOperationException)
        {
            // 회로가 닫히는 중이다(화면을 옮기는 순간 등).
        }
        finally
        {
            _binding = false;
        }
    }

    // ── 재요청 ──
    // 상세의 「재요청」이 ?resubmit={번호} 로 보낸다. 같은 내용으로 새 글을 채우는 일은 여기서 한다(RequestDetail.ResubmitAsync).

    /// <summary>「재요청」으로 들어왔을 때 바탕이 되는 요청 번호. 본문은 주소에 싣지 않고 번호만 넘겨 여기서 다시 읽는다(캡처가 커서 주소 길이 한도를 넘는다).</summary>
    [Parameter, SupplyParameterFromQuery(Name = "resubmit")]
    public string? ResubmitId { get; set; }

    /// <summary>재요청 글의 제목 앞에 붙는 말.</summary>
    private const string ResubmitPrefix = "[재요청] ";

    /// <summary>재요청의 바탕 글을 읽어 제목·본문을 채운다. 채웠으면 true.
    /// 본문은 NoticeHtml.Sanitize 로 거르고 그림 주소를 중계 주소(/files/…)로 옮긴다 — 빼먹으면 이전 캡처가 깨진 네모가 된다. 못 읽어도 화면은 빈 채로 세운다.</summary>
    private async Task<bool> SeedFromSourceAsync()
    {
        if (!int.TryParse(ResubmitId, out var source) || source <= 0)
        {
            return false;
        }

        JsonElement? origin;
        try
        {
            origin = await Api.GetAsync<JsonElement>($"requests/{source}");
        }
        catch (ApiException)
        {
            origin = null;
        }

        if (origin is not { ValueKind: JsonValueKind.Object } found)
        {
            Say($"{source}번 요청을 읽지 못해 빈 화면으로 시작합니다.", NoticeTone.Warning);
            return false;
        }

        var title = Field(found, "title") ?? string.Empty;

        // 칸 이름이 `description` 이다. 옛 이름도 함께 본다 — 상세 화면과 같다.
        var body = Field(found, "description") ?? Field(found, "content");

        _title = title.StartsWith(ResubmitPrefix, StringComparison.Ordinal)
            ? title
            : ResubmitPrefix + title;

        _content = ResubmitBody(source, title, body);

        return true;
    }

    /// <summary>재요청 본문 — 이전 글로 가는 링크 · 빈 줄 · 이전 본문. 빈 줄은 다시 부탁하는 사람이 「무엇이 아직 안 됐는가」를 적을 자리다.</summary>
    private static string ResubmitBody(int source, string title, string? body)
    {
        var label = string.IsNullOrWhiteSpace(title) ? $"요청 #{source}" : $"#{source} {title}";

        return $"<p><a href=\"/helpdesk/request/detail/{source}\">◀ 이전 요청 — {Escape(label)}</a></p>"
            + "<p><br></p>"
            + "<hr />"
            + NoticeHtml.Sanitize(body);
    }

    /// <summary>글자를 HTML 안에 넣어도 되게 만든다. 제목에 <c>&lt;</c> 가 들어 있을 수 있다.</summary>
    private static string Escape(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    /// <summary>응답에서 칸 하나를 글자로 꺼낸다. 없으면 <c>null</c>.</summary>
    private static string? Field(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var value)
        && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
            ? value.ToString()
            : null;

    // ── 쓰던 글 ──
    // 언제 읽고 언제 버리는가. 적는 방법은 request-draft.js 에 있다.

    /// <summary>적어 둔 것을 읽어 제목·본문에 얹는다. 화면마다 한 번. 읽지 못해도 화면은 세운다(임시 보관은 없어도 등록은 된다).
    /// 편집기에는 값만 넘긴다. 값을 읽어 오는 쪽만 편집기를 믿지 못한다(FlushContentAsync). 끝에 StateHasChanged 를 언제나 불러야 OnAfterRenderAsync 가 와서 걸이가 선다.</summary>
    private async Task ReadDraftAsync()
    {
        _draftRead = true;

        // 헬프데스크 신원으로 키를 짓지 않는다 — 조회가 실패하면 조용히 비어 적을 때와 읽을 때 열쇠가 갈린다.
        var owner = AuthState is null ? null : (await AuthState).User.Identity?.Name?.Trim();
        _draftKey = DraftKeyPrefix + (string.IsNullOrEmpty(owner) ? "?" : owner);

        // 「재요청」으로 들어왔으면 그쪽이 먼저다 — 오래된 임시본이 덮으면 엉뚱한 글이 뜬다. 임시본은 지우지 않는다(지켜보기가 곧 덮어쓴다).
        var seeded = await SeedFromSourceAsync();

        if (seeded)
        {
            _editorKey++;
            _bound = false;
            _draftWatched = false;
        }

        try
        {
            _draftJs ??= await JS.InvokeAsync<IJSObjectReference>(
                "import", "./_content/JSini.Web.HelpDesk/js/request-draft.js");

            var saved = seeded ? null : await _draftJs.InvokeAsync<SavedDraft?>("read", _draftKey);

            if (saved is not null)
            {
                _title = saved.Title;
                _content = saved.Html ?? string.Empty;
                _restoredAt = DateTimeOffset.FromUnixTimeMilliseconds((long)saved.SavedAt).UtcDateTime;

                // 편집기가 되살린 글을 화면에 그릴 수 있도록 다시 세운다.
                _editorKey++;
                _bound = false;
                _draftWatched = false;
            }
        }
        catch (JSException)
        {
            // 저장소를 못 쓰는 브라우저다. 빈 화면으로 시작한다.
        }
        catch (InvalidOperationException)
        {
            // 회로가 닫히는 중이다.
        }

        StateHasChanged();
    }

    /// <summary>제목 칸과 편집기를 지켜보게 한다. 이때부터 치는 글이 적힌다.</summary>
    private async Task WatchDraftAsync()
    {
        if (_draftWatched || _draftStopped || _draftJs is null)
        {
            return;
        }

        try
        {
            _self ??= DotNetObjectReference.Create(this);

            // 칸이 아직 안 섰어도 걸린다 — JS 가 문서에서 사건을 받고 적을 때 칸을 찾는다.
            _draftWatched = await _draftJs.InvokeAsync<bool>(
                "attach", _draftKey, TitleSelector, EditorSelector, _self);
        }
        catch (JSException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    /// <summary>적어 둔 것을 버린다. andStop 이면 더 적지도 않는다.
    /// 등록 성공 뒤가 andStop 인 자리다 — 글은 이미 서버에 있어 다시 적히면 같은 요청이 두 번 들어간다.</summary>
    private async Task ForgetDraftAsync(bool andStop)
    {
        _restoredAt = null;

        if (_draftJs is null)
        {
            return;
        }

        try
        {
            if (andStop)
            {
                _draftStopped = true;
                await _draftJs.InvokeVoidAsync("stop", _draftKey, true);
            }
            else
            {
                await _draftJs.InvokeVoidAsync("forget", _draftKey);
            }
        }
        catch (JSException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    /// <summary>「지우고 새로 쓰기」. 적어 둔 것과 화면에 얹힌 것을 함께 버린다(편집기는 빈 Markup 을 받아 스스로 비운다).
    /// 되묻지 않는다 — 방금 되살린 직후 누르는 단추라 쓰던 글이 아니라는 것이 분명하다.</summary>
    /// <summary>되묻는 창. 쓰던 글을 버리는 단추가 그것을 쓴다.</summary>
    private ConfirmDialog? _confirm;

    /// <summary>지울 것이 있는가. 없으면 단추를 꺼 둔다 — 눌러도 아무 일 없는 단추는 고장으로 읽힌다.
    /// _content 는 편집기가 늦게 알려 주므로 방금 친 글자에는 비어 있을 수 있으나, 한 박자 뒤에 켜지므로 충분하다.</summary>
    private bool HasSomethingToClear =>
        !string.IsNullOrWhiteSpace(_title) || !RequestContentHtml.IsBlank(_content);

    /// <summary>쓰던 글을 버린다. 되묻고 나서 버린다 — 늘 손에 닿는 자리라 실수로 누르기 쉽고, 되돌릴 수 없어(임시 보관까지 지운다) 한 번 더 묻는다.</summary>
    private async Task ClearAsync()
    {
        if (_confirm is null)
        {
            return;
        }

        var ok = await _confirm.AskAsync(
            "쓰던 제목과 내용을 지우고 새로 씁니다. 되돌릴 수 없습니다.",
            "지우고 새로 쓰기",
            "지우기");

        if (!ok)
        {
            return;
        }

        await DiscardDraftAsync();
    }

    private async Task DiscardDraftAsync()
    {
        _title = null;
        _content = string.Empty;
        _editorKey++;
        _bound = false;
        _draftWatched = false;

        await ForgetDraftAsync(andStop: false);
    }

    /// <summary>
    /// 브라우저가 적기를 거절했다고 JS 가 알려 온다. <b>한 번만 온다.</b>
    /// </summary>
    [JSInvokable]
    public void OnDraftBlocked()
    {
        if (_draftBlocked)
        {
            return;
        }

        _draftBlocked = true;
        StateHasChanged();
    }

    /// <summary>적어 둔 한 벌. <c>request-draft.js</c> 의 <c>read</c> 가 주는 모양이다.</summary>
    private sealed class SavedDraft
    {
        [System.Text.Json.Serialization.JsonPropertyName("title")]
        public string? Title { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("html")]
        public string? Html { get; set; }

        /// <summary>
        /// 골라 두었던 요청자(고객) 번호. 고른 적이 없으면 <c>null</c> 이다.
        /// </summary>
        [System.Text.Json.Serialization.JsonPropertyName("requester")]
        public string? Requester { get; set; }

        /// <summary>적은 때. 브라우저가 주는 값이라 <b>1970 년부터의 밀리초</b>다.</summary>
        [System.Text.Json.Serialization.JsonPropertyName("savedAt")]
        public double SavedAt { get; set; }
    }

    // ── 붙여넣은 그림 ────────────────────────────────────────

    /// <summary>가로챈 그림이 도착했다. 올리고 그 자리에 img 를 꽂는다. 들어오는 길은 셋(붙여넣기 · 끌어놓기 · 그림 단추)이고 JS 가 한 칸으로 몬다.
    /// 한 장이 실패해도 나머지는 올린다 — 첫 장 때문에 나머지를 버리면 사람은 무엇이 들어갔는지 모른 채 다시 넣는다.</summary>
    private async Task OnPastedAsync(InputFileChangeEventArgs e)
    {
        List<IBrowserFile> files;

        try
        {
            files = [.. e.GetMultipleFiles(MaxPastedImages)];
        }
        catch (InvalidOperationException)
        {
            Say($"한 번에 그림 {MaxPastedImages}장까지 넣을 수 있습니다.", NoticeTone.Warning);
            return;
        }

        foreach (var file in files)
        {
            if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                Say($"{file.Name} 은(는) 그림이 아닙니다. 첨부 칸으로 올려 주십시오.", NoticeTone.Warning);
                continue;
            }

            if (file.Size > MaxImageBytes)
            {
                Say($"{file.Name} 이(가) 너무 큽니다 — 본문 그림은 한 장 {MaxImageBytes / 1024 / 1024}MB 까지입니다.",
                    NoticeTone.Warning);
                continue;
            }

            _pasting++;
            StateHasChanged();

            try
            {
                // 브라우저 스트림은 이 상호작용 동안에만 읽을 수 있다 — 여기서 다 받아 둔다(상한 20MB).
                using var buffer = new MemoryStream();
                await using (var source = file.OpenReadStream(MaxImageBytes))
                {
                    await source.CopyToAsync(buffer);
                }

                buffer.Position = 0;

                var uploaded = await UploadImageAsync(buffer, file.Name, file.ContentType);

                if (uploaded is null)
                {
                    continue;
                }

                await InsertImageAsync(uploaded, file.Name);
            }
            catch (IOException ex)
            {
                // 브라우저가 파일을 못 읽는다(상한을 넘겼거나 원본이 사라졌다).
                Say($"{file.Name} 을(를) 읽지 못했습니다 — {ex.Message}", NoticeTone.Warning);
            }
            catch (TimeoutException)
            {
                // 바이트가 끝내 안 왔다 — 여기서 새어 나가면 회로가 끊기고 쓰던 글이 날아가므로 한 장 못 넣는 것으로 끝낸다.
                Say($"{file.Name} 을(를) 받는 도중 끊겼습니다. 다시 넣어 주십시오.", NoticeTone.Warning);
            }
            catch (JSException ex)
            {
                // 여기서 새어 나가면 회로가 끊긴다 — 그림 한 장 때문에 쓰던 글이 날아가지 않게 이 자리에서 말하고 넘긴다.
                Say($"{file.Name} 을(를) 다루지 못했습니다 — {ex.Message}", NoticeTone.Warning);
            }
            finally
            {
                _pasting--;
                StateHasChanged();
            }
        }
    }

    /// <summary>그림 하나를 올린다. 돌려주는 것은 본문에 박을 주소, 실패하면 null.
    /// 서버 정본 주소(/api/file/download/id/…)는 포털에 /api 가 없어 깨지므로 셸 중계 주소로 넣고, 저장할 때 정본으로 되돌린다(RequestContentHtml.ToStored).</summary>
    private async Task<string?> UploadImageAsync(Stream content, string fileName, string contentType)
    {
        var uploaded = await Images.UploadAsync(content, fileName, contentType);

        if (uploaded.Url is null)
        {
            Say(uploaded.Error ?? $"{fileName} 을(를) 올리지 못했습니다.", NoticeTone.Error);
        }

        return uploaded.Url;
    }

    /// <summary>올린 그림을 편집기의 <b>고르던 자리</b>에 꽂는다.</summary>
    private async Task InsertImageAsync(string url, string alt)
    {
        try
        {
            if (_editorJs is not null)
            {
                await _editorJs.InvokeAsync<bool>("insertImage", EditorSelector, url, alt);
            }
        }
        catch (JSException)
        {
            // 꽂지 못했다 — 파일은 이미 올라가 있으니 주소를 알려 준다. 아무 말도 안 하면 그림이 사라진 줄 안다.
            Say($"그림을 글에 넣지 못했습니다. 주소: {url}", NoticeTone.Warning);
        }
        catch (InvalidOperationException)
        {
        }
    }

    // ── 등록 ──

    private async Task SubmitAsync()
    {
        if (_saving)
        {
            return;
        }

        _saving = true;
        StateHasChanged();
        await Task.Yield(); // UI 업데이트를 위해 제어권 양보 (화면 멈춤 방지)

        try
        {
            // 편집기가 아직 알려 주지 않은 마지막 줄까지 여기서 받아 온다.
            await FlushContentAsync();

            // 브라우저의 required 만 믿지 않는다 — 서버도 보지만 여기서 막으면 왕복을 아끼고 빠진 칸을 바로 알려 준다.
            if (RequestContentHtml.IsBlank(_content))
            {
                Say("내용을 채워 주십시오.", NoticeTone.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(_title))
            {
                var extracted = RequestContentHtml.ExtractTitle(_content);
                if (string.IsNullOrWhiteSpace(extracted))
                {
                    Say("제목을 채워 주십시오.", NoticeTone.Warning);
                    return;
                }
                _title = extracted;
                StateHasChanged();
            }

            // 가로채기를 지나온 그림이 남아 있으면 여기서 파일로 바꾼다.
            var content = await AbsorbPendingImagesAsync(_content);

            if (content is null)
            {
                return;
            }

            var created = await CreateAsync(RequestContentHtml.ToStored(content));
            if (created is null)
            {
                return;
            }

            // 요청이 들어간 자리에서 적어 둔 것을 버린다 — 첨부가 뒤에서 실패해도 글은 이미 서버에 있어, 남기면 되살아나 두 번 들어간다.
            await ForgetDraftAsync(andStop: true);

            var failed = await UploadAsync(created.Value);

            if (failed == 0)
            {
                if (_picker is not null)
                {
                    await _picker.ClearAsync();
                }

                // 목록으로 보낸다 — 등록 화면에 남기면 같은 요청을 두 번 넣기 쉽다.
                // 고객으로 들어오면 「요청 처리」가 「요청자 = 나」로 서서 방금 넣은 것이 맨 위에 있다.
                Navigation.NavigateTo("/helpdesk/request/manage");
                return;
            }

            // 목록으로 보내지 않는다 — 요청은 이미 들어갔으니 다시 등록하면 안 되고, 첨부는 상세 화면에서 올린다.
            Say($"요청은 등록했지만 첨부 {failed}개를 올리지 못했습니다. "
                + $"요청 {created} 번 상세에서 다시 올려 주십시오.", NoticeTone.Warning);
        }
        finally
        {
            _saving = false;
        }
    }

    /// <summary>편집기에서 지금 화면에 있는 글을 읽어 _content 에 담는다. 읽지 못하면 있던 값을 그대로 둔다(비우면 잘 쓴 본문까지 날아간다).</summary>
    private async Task FlushContentAsync(bool keepIfBlank = false)
    {
        try
        {
            if (_editorJs is null)
            {
                return;
            }

            var markup = await _editorJs.InvokeAsync<string?>("readMarkup", EditorSelector);

            if (markup is not null && !(keepIfBlank && RequestContentHtml.IsBlank(markup)))
            {
                _content = markup;
            }
        }
        catch (JSException)
        {
            // 브라우저에서 못 읽었다. 파라미터로 올라온 값으로 보낸다.
        }
        catch (InvalidOperationException)
        {
            // 회로가 닫히는 중이다.
        }
    }

    /// <summary>임시 보관에게 지금 화면을 다시 보라고 알린다. 지켜보기는 input 이벤트에 걸려 있어 화면이 값을 직접 넣은 때(AI 제목 고르기)는 이벤트가 안 난다.</summary>
    private async Task TouchDraftAsync()
    {
        if (_draftJs is null || _draftStopped)
        {
            return;
        }

        try
        {
            await _draftJs.InvokeVoidAsync("touch", _draftKey);
        }
        catch (JSException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }


    /// <summary>지금 화면의 본문을 AI 도우미에게 넘긴다. 먼저 편집기에서 읽어 오는 것이 요점이다 — _content 만 넘기면 마지막 줄이 빠진다.</summary>
    private async Task<string?> FlushedContentAsync()
    {
        await FlushContentAsync();
        return _content;
    }

    /// <summary>AI 가 지은 제목 중 사람이 고른 것을 제목 칸에 넣는다. 입력 이벤트가 안 나므로 임시 보관에도 알린다 — 아니면 새로고침에 고른 제목이 사라진다.</summary>
    private async Task ApplyTitleAsync(string title)
    {
        _title = title;
        StateHasChanged();

        await TouchDraftAsync();
    }

    /// <summary>AI 가 간추린 덩이를 본문 맨 위에 꽂는다. _content 를 고쳐 편집기에 되돌리면 글이 통째로 다시 그려져 커서와 실행취소가 날아가므로 DOM 을 직접 만진다(prependHtml).</summary>
    private async Task InsertSummaryAsync(string html)
    {
        try
        {
            if (_editorJs is null)
            {
                return;
            }

            await _editorJs.InvokeAsync<bool>("prependHtml", EditorSelector, html);

            // 꽂은 것을 C# 쪽 _content 에도 들여놓는다 — 아니면 등록이 편집기를 읽기 전까지 요약 없는 옛 글이다.
            await FlushContentAsync();
            await TouchDraftAsync();
        }
        catch (JSException)
        {
            Say("요약을 본문에 넣지 못했습니다.", NoticeTone.Error);
        }
        catch (InvalidOperationException)
        {
        }
    }

    /// <summary>본문에 남은 data URI 그림을 파일로 바꾼다. 하나라도 실패하면 null 이고 등록하지 않는다.
    /// base64 본문을 그대로 저장하면 DB 와 목록·메일이 무거워지고 서버가 그 data URI 를 배포 장비 디스크에 떨군다 — 한 번 더 누르게 하는 편이 낫다.</summary>
    private async Task<string?> AbsorbPendingImagesAsync(string content)
    {
        var absorbed = await Images.AbsorbAsync(content);

        if (absorbed.Url is null)
        {
            Say(absorbed.Error ?? "본문에 든 그림을 올리지 못했습니다.", NoticeTone.Error);
            return null;
        }

        // 화면에도 반영해 둔다 — 등록이 뒤에서 실패했을 때 편집기에 base64 가 남으면 누를 때마다 같은 일을 되풀이한다.
        _content = absorbed.Url;

        return absorbed.Url;
    }

    /// <summary>요청을 만든다. 성공하면 새 요청 번호. 폼으로 보낸다 — 칸 이름(Title · Description)이 서버가 읽는 그대로여야 한다. 모르는 칸은 말없이 버려져 빈 값으로 저장된다.</summary>
    private async Task<int?> CreateAsync(string description)
    {
        int? id = null;

        var ok = await RunAsync(async () =>
        {
            using var form = new MultipartFormDataContent
            {
                { new StringContent(_title!.Trim()), "Title" },
                { new StringContent(description), "Description" },
            };

            // 고객 번호는 보내지 않는다 — 요청자는 글을 쓴 사람으로 고정이고, 서버는 안 보내면 그렇게 한다(RequesterProvisioner.ResolveAsync).
            // HelpdeskUserId 를 실으면 담당자 번호(admin.id)와 겹치는 고객 이름으로 요청이 들어간다.

            var created = await Api.PostMultipartAsync<ImprovementRequest>("requests", form);
            id = created?.Id;
        }, "요청을 등록했습니다.", "요청을 등록하지 못했습니다");

        return ok ? id : null;
    }

    /// <summary>첨부를 한 번에 올린다. 돌려주는 값은 실패한 개수. 하나라도 실패하면 서버가 502 로 어느 것인지 담아 준다.</summary>
    private async Task<int> UploadAsync(int requestId)
    {
        if (_files.Count == 0)
        {
            return 0;
        }

        var streams = new List<Stream>(_files.Count);
        try
        {
            using var form = new MultipartFormDataContent
            {
                { new StringContent("ImprovementRequest"), "entityType" },
                { new StringContent(requestId.ToString()), "entityId" },
            };

            foreach (var file in _files)
            {
                var stream = file.OpenRead();
                streams.Add(stream);

                var part = new StreamContent(stream);
                part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);

                // 칸 이름이 `files` 여야 한다 — 서버가 그 이름으로 묶어 받는다.
                form.Add(part, "files", file.Name);
            }

            await Api.PostMultipartAsync<JsonElement?>("files/upload", form);
            return 0;
        }
        catch (ApiException)
        {
            // 어느 것이 실패했는지는 여기서 쓰지 않는다 — 개수만 세서 부르는 쪽이 상세에서 다시 올리라고 안내한다.
            return _files.Count;
        }
        finally
        {
            foreach (var stream in streams)
            {
                await stream.DisposeAsync();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        // 적어 둔 것은 지우지 않는다 — 화면을 떠나는 것이 곧 버리는 뜻이 아니라 다녀와서 이어 쓰게 둔다. 지켜보기만 거둔다.
        if (_draftJs is not null)
        {
            try
            {
                await _draftJs.InvokeVoidAsync("stop", _draftKey, false);
                await _draftJs.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
            catch (JSException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        _self?.Dispose();

        if (_editorJs is null)
        {
            return;
        }

        try
        {
            await _editorJs.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // 회로가 이미 끊겼다. 브라우저 쪽은 함께 사라졌다.
        }
        catch (JSException)
        {
        }
    }
}
