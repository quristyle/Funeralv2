using Microsoft.AspNetCore.Components;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using DevExpress.Blazor;
using JSini.Web.Http;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.HelpDesk.Api;

namespace JSini.Web.HelpDesk.Components.Pages;

public partial class RequestNew
{
    [Inject] private HelpDeskApi Api { get; set; } = default!;
    [Inject] private HelpDeskContext Context { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>편집기를 짚는 이름. JS 와 CSS 가 같은 글자를 본다.</summary>
    private const string EditorSelector = ".hd-content-editor";

    /// <summary>숨겨 둔 파일 칸의 id. JS 가 이 이름으로 찾아 그림을 밀어 넣는다.</summary>
    private const string PasteInputId = "hd-paste-input";

    /// <summary>제목 칸을 짚는 이름. 쓰던 글을 적어 두는 JS 가 이 안에서 입력칸을 찾는다.</summary>
    private const string TitleSelector = ".hd-title-field";

    /// <summary>
    /// 쓰던 글을 적어 두는 열쇠의 앞부분. 뒤에 <b>포털 로그인 아이디</b>가 붙는다 —
    /// 그것이 없으면 공용 PC 에서 남이 쓰다 만 글이 내 화면에 뜬다.
    /// </summary>
    private const string DraftKeyPrefix = "jsini-hd-request-draft:";

    /// <summary>
    /// 도구줄 「그림」 단추에 붙이는 이름표. <b>JS 가 이 이름으로 그 단추를
    /// 알아본다</b> — 좁은 화면에서는 그 단추가 넘침 메뉴로 옮겨 가 편집기
    /// 바깥에 그려지므로, 자리로 찾을 수가 없다.
    /// </summary>
    private const string PictureButtonClass = "hd-pick-image";

    /// <summary>
    /// 본문에 붙이는 그림 한 장의 상한. <b>서버와 같은 값이다</b>
    /// (<c>FileUploadEndpoints.MaxImageBytes</c>) — 여기서 막는 것은 먼저
    /// 말해 주려는 것이고, 정작 막는 쪽은 서버다.
    /// </summary>
    private const long MaxImageBytes = 20L * 1024 * 1024;

    /// <summary>한 번에 붙여넣을 수 있는 장수.</summary>
    private const int MaxPastedImages = 10;

    private string? _title;
    private string _content = string.Empty;
    private bool _saving;

    /// <summary>
    /// 담당자가 고른 <b>요청자(고객) 번호</b>. 고객으로 연결된 계정은 쓰지 않는다 —
    /// 그쪽은 자기 자신이 요청자다(<see cref="RequesterId"/>).
    /// </summary>
    private string? _requester;

    /// <summary>지금 올리고 있는 그림 수. 0 이 아니면 「등록」을 잠근다.</summary>
    private int _pasting;

    private FilePicker? _picker;
    private IReadOnlyList<PickedFile> _files = [];

    private IJSObjectReference? _editorJs;

    /// <summary>붙여넣기 처리기를 걸었는가.</summary>
    private bool _bound;

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

    /// <summary>
    /// 지켜보기를 <b>영영 거두었는가</b>(등록에 성공했다).
    /// </summary>
    /// <remarks>
    /// 이 표시가 없으면 거둔 바로 다음 그리기에 <b>다시 걸린다</b> —
    /// <see cref="WatchDraftAsync"/> 는 「아직 안 걸렸으면 건다」이기 때문이다.
    /// 첨부가 실패해 이 화면에 남는 길이 실제로 그렇고, 그러면 이미 등록한
    /// 글이 다시 적혀 다음에 되살아난다. 헤드리스로 몰아 보고 잡았다.
    /// </remarks>
    private bool _draftStopped;

    /// <summary>
    /// 되살린 글이 적힌 때. <c>null</c> 이면 되살린 것이 없다 —
    /// 안내 줄과 「지우고 새로 쓰기」 단추가 이 값 하나를 본다.
    /// </summary>
    private DateTime? _restoredAt;

    /// <summary>
    /// 브라우저가 적기를 거절했다(사생활 보호 모드 · 용량 초과 · 본문이 너무 큼).
    /// <b>화면이 이것을 말해야 한다</b> — 임시 보관이 안 되는 것을 모르고 믿는
    /// 편이 아예 없는 것보다 나쁘다.
    /// </summary>
    private bool _draftBlocked;

    /// <summary>
    /// 이 요청의 주인으로 <b>보낼 고객 번호</b>. 안 보내면 <c>null</c>.
    /// </summary>
    /// <remarks>
    /// 고객으로 연결된 계정은 자기 자신, 담당자는 위에서 고른 사람이다.
    /// <b>비어 있어도 된다</b> — 서버가 글을 쓴 사람 자신을 요청자로 삼는다.
    /// <b><see cref="HelpDeskContext.HelpdeskUserId"/> 를 그대로 쓰지 않는다</b> —
    /// 담당자에게 그 값은 <c>admin.id</c> 라 고객 번호가 아니다.
    /// </remarks>
    private int? RequesterId =>
        Context.CustomerId
        ?? (int.TryParse(_requester, out var picked) && picked > 0 ? picked : null);

    protected override async Task OnInitializedAsync()
    {
        await Context.LoadIdentityAsync();

        // 담당자만 요청자를 고른다. 고객에게는 고를 것이 없으므로 목록도 안 받는다.
        if (!Context.IsCustomer)
        {
            await Context.LoadOrganizationsAsync();
        }
    }

    /// <summary>
    /// 도구줄의 「그림」 단추에 이름표를 붙인다. <b>바꾸는 것은 이름표뿐이다</b> —
    /// 아이콘·설명·자리는 편집기가 정한 그대로 두고, 누를 때 하는 일만
    /// <c>request-editor.js</c> 가 브라우저 쪽에서 갈아 끼운다.
    /// </summary>
    /// <remarks>
    /// 단추를 빼고 우리 것을 새로 다는 길도 있지만 그러지 않는다. 아이콘이
    /// <c>ImageSource</c> 라 CSS 이름으로 옮겨 붙일 수 없어 도구줄에 글자
    /// 단추 하나만 튄다. 그리고 <b>가로채기가 어느 브라우저에서 안 걸려도
    /// 단추가 죽지는 않는다</b> — 그때는 편집기 제 판이 열려 예전 그대로다.
    /// </remarks>
    private static void MarkPictureButton(DevExpress.Blazor.Office.IToolbar toolbar)
    {
        var picture = toolbar.Groups[HtmlEditorToolbarGroupNames.InsertElement]
            ?.Items[HtmlEditorToolbarItemNames.ShowInsertPictureDialog];

        if (picture is not null)
        {
            picture.CssClass = PictureButtonClass;
        }
    }

    /// <summary>
    /// 편집기에 붙여넣기 처리기를 건다.
    /// </summary>
    /// <remarks>
    /// <b>첫 번째 그리기에만 걸지 않는다.</b> 계정 연결 여부를 읽고 나서야
    /// 편집기가 화면에 나타나므로(<c>Context.CanUse</c>), 첫 그리기에는 걸
    /// 자리가 아직 없다. 걸릴 때까지 그리기마다 다시 보고, <b>걸리고 나면
    /// 그만 본다</b> — 그리기마다 브라우저를 한 번씩 부르는 것은 글 한 자
    /// 칠 때마다 왕복이 하나 느는 일이다.
    /// </remarks>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!Context.CanUse)
        {
            return;
        }

        // **적어 둔 것부터 읽는다.** 읽고 나면 화면을 한 번 더 그려야 그 글이
        // 편집기에 들어가므로, 얹는 일은 다음 차례로 미룬다.
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
        if (_bound)
        {
            return;
        }

        try
        {
            _editorJs ??= await JS.InvokeAsync<IJSObjectReference>(
                "import", "./_content/JSini.Web.HelpDesk/js/request-editor.js");

            // 거짓이면 편집기가 아직 화면에 없다는 뜻이다. 다음 그리기에 다시 본다.
            _bound = await _editorJs.InvokeAsync<bool>(
                "bind", EditorSelector, PasteInputId, "." + PictureButtonClass);
        }
        catch (JSException)
        {
            // 브라우저에서 못 걸었다. 붙여넣기는 편집기 기본 동작(data URI)으로
            // 떨어지고, 등록 직전의 훑기가 그것을 파일로 바꾼다. 화면은 산다.
        }
        catch (InvalidOperationException)
        {
            // 회로가 닫히는 중이다(화면을 옮기는 순간 등).
        }
    }

    // ── 쓰던 글 ──────────────────────────────────────────────
    //
    // 무엇을 고친 것인지는 이 파일 머리말에, 어디에 어떻게 적는지는
    // `request-draft.js` 에 있다. 여기 있는 것은 **언제 읽고 언제 버리는가**다.

    /// <summary>
    /// 적어 둔 것을 읽어 제목·본문에 얹는다. <b>화면마다 한 번</b>이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>읽지 못해도 화면을 세우지 않는다.</b> 임시 보관은 없어도 등록은 되는
    /// 기능이다 — 여기서 던지면 글을 쓸 자리 자체가 사라진다.
    /// </para>
    ///
    /// <para>
    /// <b>편집기에는 값을 넘기기만 한다.</b> 첫 그리기 뒤에 <c>Markup</c> 을
    /// 바꿔도 편집기가 그것을 받아 그린다 — 헤드리스로 띄워 되살리기와
    /// 「지우고 새로 쓰기」를 몰아 보고 잰 것이다(DevExpress 26.1.4). 한때
    /// 브라우저 쪽에서 직접 얹는 길을 함께 두었는데, 그쪽이 <b>한 번도 돌지
    /// 않아</b> 걷었다. 값을 <b>읽어 오는</b> 쪽만 편집기를 못 믿는다
    /// (<see cref="FlushContentAsync"/>) — 그쪽은 늦게 알려 주기 때문이고,
    /// 넣는 쪽과는 사정이 다르다.
    /// </para>
    ///
    /// <para>
    /// 끝에 <c>StateHasChanged</c> 를 <b>언제나</b> 부른다. 되살릴 것이 없어도
    /// 그렇다 — 이 자리에서 그리기가 끊기면 다음
    /// <c>OnAfterRenderAsync</c> 가 오지 않아 <b>붙여넣기와 지켜보기가 영영
    /// 걸리지 않는다.</b>
    /// </para>
    /// </remarks>
    private async Task ReadDraftAsync()
    {
        _draftRead = true;

        var owner = Context.Identity?.JsiniUserId?.Trim();
        _draftKey = DraftKeyPrefix + (string.IsNullOrEmpty(owner) ? "?" : owner);

        try
        {
            _draftJs ??= await JS.InvokeAsync<IJSObjectReference>(
                "import", "./_content/JSini.Web.HelpDesk/js/request-draft.js");

            var saved = await _draftJs.InvokeAsync<SavedDraft?>("read", _draftKey);

            if (saved is not null)
            {
                _title = saved.Title;
                _content = saved.Html ?? string.Empty;
                _restoredAt = DateTimeOffset.FromUnixTimeMilliseconds((long)saved.SavedAt).LocalDateTime;
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

            // 거짓이면 칸이 아직 화면에 없다. 다음 그리기에 다시 본다.
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

    /// <summary>
    /// 적어 둔 것을 버린다. <paramref name="andStop"/> 이면 더 적지도 않는다.
    /// </summary>
    /// <remarks>
    /// 등록에 성공한 뒤가 <paramref name="andStop"/> 인 자리다. 그 글은 이미
    /// 서버에 있으므로 <b>다시 적히면 안 된다</b> — 남으면 다음에 이 화면을
    /// 열 때 되살아나 같은 요청이 두 번 들어간다.
    /// </remarks>
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

    /// <summary>
    /// 「지우고 새로 쓰기」. 적어 둔 것도, 화면에 얹혀 있는 것도 함께 버린다
    /// (편집기는 빈 <c>Markup</c> 을 받아 스스로 비운다).
    /// </summary>
    /// <remarks>
    /// <b>묻지 않는다.</b> 여기서 버리는 것은 아직 어디에도 저장되지 않은
    /// 글이지만, 되살린 직후의 사람이 누르는 단추라 「내가 쓰던 것이 아니다」가
    /// 이미 분명하다. 되살린 안내 줄과 함께 사라지므로 눌렀다는 것도 보인다.
    /// </remarks>
    private async Task DiscardDraftAsync()
    {
        _title = null;
        _content = string.Empty;

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
        public string? Title { get; set; }

        public string? Html { get; set; }

        /// <summary>적은 때. 브라우저가 주는 값이라 <b>1970 년부터의 밀리초</b>다.</summary>
        public double SavedAt { get; set; }
    }

    // ── 붙여넣은 그림 ────────────────────────────────────────

    /// <summary>
    /// 가로챈 그림이 도착했다. 올리고 그 자리에 <c>&lt;img&gt;</c> 를 꽂는다.
    /// </summary>
    /// <remarks>
    /// 들어오는 길이 셋이다 — 붙여넣기 · 끌어놓기 · <b>도구줄의 「그림」 단추</b>.
    /// 셋 다 <c>request-editor.js</c> 가 같은 파일 칸으로 몰아 주므로 여기는
    /// 어느 길로 왔는지 알 필요가 없다.
    ///
    /// <b>한 장이 실패해도 나머지는 올린다.</b> 여러 장을 한 번에 넣는 일이
    /// 있고, 그때 첫 장이 크다는 이유로 나머지까지 버리면 사람은 무엇이 들어갔는지
    /// 모른 채 다시 넣는다.
    /// </remarks>
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
                // 브라우저 스트림은 **이 상호작용 동안에만** 읽을 수 있다.
                // 그대로 HttpClient 에 물리지 않고 여기서 다 받아 둔다 —
                // 상한이 20MB 라 메모리에 들고 있어도 된다.
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
                // 바이트가 끝내 안 왔다. **여기서 새어 나가면 회로가 끊긴다** —
                // 쓰던 글이 통째로 날아간다. 한 장 못 넣는 것으로 끝낸다.
                Say($"{file.Name} 을(를) 받는 도중 끊겼습니다. 다시 넣어 주십시오.", NoticeTone.Warning);
            }
            catch (JSException ex)
            {
                // **여기서 새어 나가면 회로가 끊긴다** — 그림 한 장 때문에
                // 쓰던 글이 통째로 날아간다. 이 자리에서 말하고 넘긴다.
                Say($"{file.Name} 을(를) 다루지 못했습니다 — {ex.Message}", NoticeTone.Warning);
            }
            finally
            {
                _pasting--;
                StateHasChanged();
            }
        }
    }

    /// <summary>
    /// 그림 하나를 올린다. 돌려주는 것은 <b>본문에 박을 주소</b>, 실패하면 <c>null</c>.
    /// </summary>
    /// <remarks>
    /// 서버가 주는 것은 백엔드 정본 주소(<c>/api/file/download/id/{guid}</c>)다.
    /// 브라우저가 보는 것은 포털(:5557)이고 거기에는 <c>/api</c> 가 없으므로
    /// 그대로 걸면 깨진 네모가 된다 — 셸 중계 주소로 옮겨서 넣고, 저장할 때
    /// 다시 정본으로 되돌린다(<c>RequestContentHtml.ToStored</c>).
    /// </remarks>
    private async Task<string?> UploadImageAsync(Stream content, string fileName, string contentType)
    {
        try
        {
            using var form = new MultipartFormDataContent();
            var part = new StreamContent(content);

            // **종류만 떼어 쓴다.** `MediaTypeHeaderValue` 는 뒤에 딸린 것
            // (`image/png; charset=…`)이 있으면 던지는데, 그 예외가 여기서
            // 새어 나가면 회로가 끊긴다 — 붙여넣기 한 번에 화면이 죽는다.
            part.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue(contentType.Split(';')[0].Trim());

            // 칸 이름이 `file` 이어야 한다 — 서버가 그 이름으로 찾는다.
            form.Add(part, "file", fileName);

            var uploaded = await Api.PostMultipartAsync<UploadedImage>("files/image", form);

            if (uploaded is null || string.IsNullOrWhiteSpace(uploaded.FileId))
            {
                Say($"{fileName} 을(를) 올렸는데 주소를 받지 못했습니다.", NoticeTone.Error);
                return null;
            }

            return FileDownload.UrlFor(uploaded.FileId);
        }
        catch (ApiException ex)
        {
            Say($"{fileName} 을(를) 올리지 못했습니다 — {ex.Message}", NoticeTone.Error);
            return null;
        }
        catch (FormatException)
        {
            // 브라우저가 준 종류를 읽지 못했다. 여기서 던지면 회로가 끊긴다.
            Say($"{fileName} 의 파일 종류를 읽지 못했습니다.", NoticeTone.Warning);
            return null;
        }
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
            // 꽂지 못했다. 파일은 이미 올라가 있으므로 주소를 알려 준다 —
            // 아무 말도 안 하면 사람은 그림이 사라진 줄 안다.
            Say($"그림을 글에 넣지 못했습니다. 주소: {url}", NoticeTone.Warning);
        }
        catch (InvalidOperationException)
        {
        }
    }

    // ── 등록 ─────────────────────────────────────────────────

    private async Task SubmitAsync()
    {
        if (_saving)
        {
            return;
        }

        // 편집기가 아직 알려 주지 않은 마지막 줄까지 여기서 받아 온다.
        await FlushContentAsync();

        // 브라우저의 required 만 믿지 않는다. 서버도 다시 보지만, 여기서 막으면
        // 왕복 한 번을 아끼고 무엇이 빠졌는지 바로 알려 줄 수 있다.
        if (string.IsNullOrWhiteSpace(_title) || RequestContentHtml.IsBlank(_content))
        {
            Say("제목과 내용을 채워 주십시오.", NoticeTone.Warning);
            return;
        }

        _saving = true;
        try
        {
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

            // **요청이 들어간 그 자리에서** 적어 둔 것을 버린다. 첨부가 뒤에서
            // 실패해 이 화면에 남더라도 이 글은 이미 서버에 있으므로, 남겨 두면
            // 다음에 화면을 열 때 되살아나 같은 요청이 두 번 들어간다.
            await ForgetDraftAsync(andStop: true);

            var failed = await UploadAsync(created.Value);

            if (failed == 0)
            {
                if (_picker is not null)
                {
                    await _picker.ClearAsync();
                }

                // 목록으로 보낸다. 등록 화면에 남겨 두면 같은 요청을 두 번 넣기 쉽다.
                Navigation.NavigateTo("/helpdesk/request/list");
                return;
            }

            // **목록으로 보내지 않는다.** 요청은 이미 들어갔으므로 다시 등록하면
            // 안 되고, 첨부만 다시 올릴 자리는 상세 화면이다. 그 말을 해 준다.
            Say($"요청은 등록했지만 첨부 {failed}개를 올리지 못했습니다. "
                + $"요청 {created} 번 상세에서 다시 올려 주십시오.", NoticeTone.Warning);
        }
        finally
        {
            _saving = false;
        }
    }

    /// <summary>
    /// 편집기에서 <b>지금 화면에 있는 글</b>을 읽어 <c>_content</c> 에 담는다.
    /// </summary>
    /// <remarks>
    /// <b>읽지 못하면 있던 값을 그대로 둔다.</b> 여기서 비우면 잘 써 둔 본문까지
    /// 날아가고, 증상이 「가끔 빈 글이 등록된다」로만 보인다.
    /// </remarks>
    private async Task FlushContentAsync()
    {
        try
        {
            if (_editorJs is null)
            {
                return;
            }

            var markup = await _editorJs.InvokeAsync<string?>("readMarkup", EditorSelector);

            if (markup is not null)
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

    /// <summary>
    /// 본문에 남아 있는 data URI 그림을 파일로 바꾼다.
    /// 하나라도 실패하면 <c>null</c> — 그때는 등록하지 않는다.
    /// </summary>
    /// <remarks>
    /// <b>실패해도 그냥 저장하지 않는 이유.</b> base64 한 장이 든 본문은 수 MB 짜리
    /// 글자다. 그것이 DB 에 들어가면 목록·검색·메일이 그 뒤로 계속 무거워지고,
    /// 서버는 그 data URI 를 배포 장비 디스크에 떨군다(결정 D5-B 가 그만두기로 한
    /// 방식이라 컨테이너에서는 재배포 때 사라진다). 한 번 더 누르게 하는 편이 낫다.
    /// </remarks>
    private async Task<string?> AbsorbPendingImagesAsync(string content)
    {
        var pending = RequestContentHtml.PendingImages(content);

        if (pending.Count == 0)
        {
            return content;
        }

        var uploaded = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var image in pending)
        {
            if (image.Bytes.LongLength > MaxImageBytes)
            {
                Say($"본문에 든 그림 한 장이 너무 큽니다 — 한 장 {MaxImageBytes / 1024 / 1024}MB 까지입니다.",
                    NoticeTone.Warning);
                return null;
            }

            using var buffer = new MemoryStream(image.Bytes, writable: false);
            var url = await UploadImageAsync(buffer, image.FileName, image.ContentType);

            if (url is null)
            {
                return null;
            }

            uploaded[image.Source] = url;
        }

        var replaced = RequestContentHtml.ReplaceAll(content, uploaded);

        // 화면에도 반영해 둔다. 등록이 뒤에서 실패했을 때 편집기에 남아 있는
        // 것이 다시 base64 이면 누를 때마다 같은 일을 되풀이한다.
        _content = replaced;

        return replaced;
    }

    /// <summary>
    /// 요청을 만든다. 성공하면 새 요청 번호.
    ///
    /// <b>폼으로 보낸다</b> — 서버가 <c>ReadFormAsync</c> 로 읽기 때문이다.
    /// 칸 이름은 서버가 꺼내 쓰는 그대로여야 한다(<c>Title</c> · <c>Description</c>).
    /// ASP.NET Core 는 모르는 칸을 말없이 버리므로, 틀려도 오류가 아니라
    /// **빈 값으로 저장**된다.
    /// </summary>
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

            // 고객으로 연결된 계정이면 서버가 자기 것으로 고정하므로 이 값을
            // 무시한다. 담당자가 대신 등록하는 길만 이 값을 쓴다.
            // 안 보내도 된다 — 서버가 글을 쓴 사람 자신을 요청자로 삼는다.
            //
            // **`Context.HelpdeskUserId` 를 보내면 안 된다** — 담당자에게 그 값은
            // `admin.id` 다. 보내던 시절에는 번호가 겹치는 고객이 있으면 남의
            // 이름으로 요청이 들어갔고, 없으면 저장이 터졌다.
            if (RequesterId is { } customerId)
            {
                form.Add(new StringContent(customerId.ToString()), "CustomerId");
            }

            var created = await Api.PostMultipartAsync<ImprovementRequest>("requests", form);
            id = created?.Id;
        }, "요청을 등록했습니다.", "요청을 등록하지 못했습니다");

        return ok ? id : null;
    }

    /// <summary>
    /// 첨부를 올린다. 돌려주는 값은 <b>실패한 개수</b>다.
    ///
    /// 한 번에 다 보낸다 — 서버가 <c>IFormFileCollection</c> 을 받고, 하나라도
    /// 실패하면 502 에 어느 것이 실패했는지 담아 준다.
    /// </summary>
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
            // 어느 것이 실패했는지는 서버가 메시지에 담아 주지만, 여기서는
            // 개수만 쓴다 — 부르는 쪽이 「상세에서 다시 올리라」고 안내한다.
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
        // **적어 둔 것은 지우지 않는다.** 화면을 떠나는 것이 곧 글을 버리는
        // 뜻은 아니다 — 다른 탭에 다녀오거나 창이 닫힌 뒤 돌아왔을 때
        // 이어서 쓰라고 적어 둔 것이다. 지켜보기만 거둔다.
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
