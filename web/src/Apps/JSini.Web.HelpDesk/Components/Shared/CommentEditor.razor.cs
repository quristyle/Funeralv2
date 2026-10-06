using DevExpress.Blazor;
using JSini.Web.Components.Layout;
using JSini.Web.HelpDesk.Api;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace JSini.Web.HelpDesk.Components.Shared;

public partial class CommentEditor
{
    [Inject] private ContentImages Images { get; set; } = default!;
    [Inject] private Toasts Toasts { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>
    /// 이 칸을 남들과 가르는 이름. 화면에 칸이 둘 이상 뜨므로
    /// <b>반드시 서로 달라야 한다</b>(파일 머리말).
    /// </summary>
    /// <remarks>
    /// CSS 이름과 DOM 아이디에 그대로 들어간다 — 글자·숫자·<c>-</c> 만 쓴다.
    /// </remarks>
    [Parameter, EditorRequired] public string Id { get; set; } = "new";

    /// <summary>칸 위에 적을 이름표. 비우면 이름표를 안 그린다.</summary>
    [Parameter] public string? Label { get; set; }

    /// <summary>보내기 단추에 적을 글자.</summary>
    [Parameter] public string SubmitText { get; set; } = "남기기";

    /// <summary>
    /// 다 쓴 글을 받는 쪽. <b>저장할 모양의 HTML</b> 이 넘어간다 —
    /// 그림은 이미 파일이 되어 있고 주소도 백엔드 정본으로 되돌려 두었다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="EventCallback"/> 이 아니라 값을 돌려주는 대리자다.
    /// <b>들어갔는지를 알아야 칸을 비울 수 있기 때문</b>이다 — 실패했는데
    /// 비우면 방금 쓴 글이 사라지고, 성공했는데 안 비우면 같은 말이 두 번
    /// 들어간다. 대신 <b>부르는 쪽이 스스로 다시 그려야 한다</b>(Blazor 가
    /// 자동으로 그려 주는 것은 <c>EventCallback</c> 뿐이다).
    /// </para>
    /// </remarks>
    [Parameter] public Func<string, Task<bool>>? OnSubmit { get; set; }

    /// <summary>「취소」. 대리자가 없으면 단추도 안 그린다(뿌리 댓글 칸이 그렇다).</summary>
    [Parameter] public EventCallback OnCancel { get; set; }

    private string _markup = string.Empty;
    private bool _saving;

    /// <summary>
    /// 이 칸에 글을 쓰는 중인가 — <b>도구줄을 세울지</b>를 가른다.
    /// </summary>
    /// <remarks>
    /// 한 번 참이 되면 **그 글을 보낼 때까지 거짓으로 안 돌아간다.** 포커스가
    /// 빠질 때 접으면 도구줄을 쓰는 순간에 접힌다 — 글꼴·정렬 단추가 띄우는
    /// 목록은 문서 끝에 따로 그려져 포커스가 칸 밖으로 나가기 때문이다.
    /// 까닭은 화면 머리말에 적어 두었다.
    /// </remarks>
    private bool _writing;

    /// <summary>지금 올리고 있는 그림 수. 0 이 아니면 보내기를 잠근다.</summary>
    private int _pasting;

    private IJSObjectReference? _editorJs;

    /// <summary>붙여넣기 처리기를 걸었는가. <see cref="Id"/> 가 바뀌면 다시 건다.</summary>
    private bool _bound;

    /// <summary>지금 거는 중인가. 그리기마다 겹쳐 도는 것을 막는다.</summary>
    private bool _binding;

    private string _boundId = string.Empty;

    /// <summary>
    /// 편집기가 화면에 설 때까지 기다리는 횟수와 사이 간격.
    /// </summary>
    /// <remarks>
    /// <b>재어 보고 정한 값이다</b>(헤드리스 크롬 · DevExpress 26.1.4). 아래
    /// <see cref="BindEditorAsync"/> 머리말 참고 — 5초면 넉넉하고, 그 안에
    /// 안 서면 편집기가 아예 안 뜬 것이라 더 기다려도 소용이 없다.
    /// </remarks>
    private const int BindAttempts = 20;

    private static readonly TimeSpan BindWait = TimeSpan.FromMilliseconds(250);

    /// <summary>한 번에 붙여넣을 수 있는 장수.</summary>
    private const int MaxPastedImages = 10;

    /// <summary>편집기 껍데기를 짚는 이름. JS 와 CSS 가 같은 글자를 본다.</summary>
    private string EditorClass => $"hd-cmt-editor hd-cmt-editor--{Id}";

    private string EditorSelector => $".hd-cmt-editor--{Id}";

    /// <summary>숨겨 둔 파일 칸의 id. JS 가 이 이름으로 찾아 그림을 밀어 넣는다.</summary>
    private string PasteInputId => $"hd-cmt-paste-{Id}";

    /// <summary>도구줄 「그림」 단추에 붙이는 이름표. JS 가 이 이름으로 알아본다.</summary>
    private string PictureButtonClass => $"hd-pick-image--{Id}";

    protected override void OnParametersSet()
    {
        // 같은 자리에서 답글 대상만 바뀌면 Blazor 는 이 부품을 다시 만들지
        // 않는다. 그때 `_bound` 를 그대로 두면 **옛 이름에 걸린 처리기**를
        // 걸려 있는 것으로 알고 새 칸에는 아무것도 안 건다.
        if (_boundId != Id)
        {
            _boundId = Id;
            _bound = false;
            _markup = string.Empty;

            // 다른 댓글에 답하는 칸이 되었다. 쓰던 글이 비워졌으므로
            // 도구줄도 처음 모습으로 되돌린다.
            _writing = false;
        }
    }

    /// <summary>
    /// 칸 안 어딘가가 포커스를 받았다 — <b>이제 글을 쓰는 중</b>이다.
    /// </summary>
    /// <remarks>
    /// <c>focusin</c> 은 거품처럼 올라오므로 글칸이든 도구줄 단추든
    /// 「남기기」든 다 여기로 온다. 이미 참이면 다시 그리지 않는다 —
    /// 글자를 칠 때마다 화면을 새로 그리면 커서가 흔들린다.
    /// </remarks>
    private void StartWriting() => _writing = true;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await BindEditorAsync();
    }

    /// <summary>
    /// 편집기에 붙여넣기·끌어놓기·「그림」 단추 처리기를 건다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>「다음 그리기에 다시 본다」로 두면 안 걸린다 — 재어 보고 잡았다.</b>
    /// </para>
    /// <para>
    /// <c>DxHtmlEditor</c> 는 브라우저 쪽에서 스스로를 세우는 부품이라, 첫
    /// <c>OnAfterRenderAsync</c> 시점에는 글 쓰는 칸(<c>[contenteditable]</c>)이
    /// 아직 없다. 그러면 <c>bind</c> 가 거짓을 주는데, <b>그 뒤로 다시 그릴
    /// 일이 없으면 영영 안 걸린다.</b> 요청 등록 화면(<c>RequestNew</c>)이
    /// 멀쩡한 것은 그쪽이 임시 보관을 읽느라 <c>StateHasChanged</c> 를 한 번 더
    /// 부르기 때문이고, <b>우연히 걸리고 있는 것</b>이다.
    /// </para>
    /// <para>
    /// 증상이 나쁘다 — 화면은 멀쩡하고 붙여넣기도 되는 것처럼 보이는데,
    /// 그림이 <c>data:</c> 로 본문에 박힌다. 캡처 한 장이 회로 수신 한도(4MB)를
    /// 넘기면 <b>쓰던 글째로 회로가 끊긴다.</b> 헤드리스로 띄워 보고
    /// <c>data-hd-paste-bound</c> 가 안 붙는 것으로 잡았다.
    /// </para>
    /// <para>
    /// 그래서 <b>걸릴 때까지 기다린다.</b> 한 번 걸리면 다시 돌지 않는다.
    /// </para>
    /// </remarks>
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

            for (var attempt = 0; attempt < BindAttempts && !_bound; attempt++)
            {
                // 이름은 `Id` 로 만든다. 기다리는 동안 답글 대상이 바뀌면
                // 새 이름으로 건다(`OnParametersSet` 이 `_bound` 를 내린다).
                _bound = await _editorJs.InvokeAsync<bool>(
                    "bind", EditorSelector, PasteInputId, "." + PictureButtonClass);

                if (!_bound)
                {
                    await Task.Delay(BindWait);
                }
            }
        }
        catch (JSException)
        {
            // 브라우저에서 못 걸었다. 붙여넣기는 편집기 기본 동작(data URI)으로
            // 떨어지고, 보내기 직전의 훑기가 그것을 파일로 바꾼다. 화면은 산다.
        }
        catch (InvalidOperationException)
        {
            // 회로가 닫히는 중이다(화면을 옮기는 순간 등).
        }
        catch (TaskCanceledException)
        {
            // 화면을 떠났다. 기다리던 것을 접는다.
        }
        finally
        {
            _binding = false;
        }
    }

    /// <summary>
    /// 도구줄의 「그림」 단추에 이름표를 붙인다. <b>바꾸는 것은 이름표뿐이다</b> —
    /// 누를 때 하는 일만 <c>request-editor.js</c> 가 브라우저 쪽에서 갈아 끼운다.
    /// 까닭은 <c>RequestNew</c> 의 같은 이름 메서드에 적어 두었다.
    /// </summary>
    private void MarkPictureButton(DevExpress.Blazor.Office.IToolbar toolbar)
    {
        var picture = toolbar.Groups[HtmlEditorToolbarGroupNames.InsertElement]
            ?.Items[HtmlEditorToolbarItemNames.ShowInsertPictureDialog];

        if (picture is not null)
        {
            picture.CssClass = PictureButtonClass;
        }
    }

    /// <summary>
    /// 가로챈 그림이 도착했다. 올리고 그 자리에 <c>&lt;img&gt;</c> 를 꽂는다.
    /// </summary>
    /// <remarks>
    /// 들어오는 길이 셋이다 — 붙여넣기 · 끌어놓기 · 도구줄의 「그림」 단추.
    /// <b>한 장이 실패해도 나머지는 올린다.</b>
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
            Toasts.Show($"한 번에 그림 {MaxPastedImages}장까지 넣을 수 있습니다.", NoticeTone.Warning);
            return;
        }

        foreach (var file in files)
        {
            if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                Toasts.Show($"{file.Name} 은(는) 그림이 아닙니다.", NoticeTone.Warning);
                continue;
            }

            if (file.Size > ContentImages.MaxBytes)
            {
                Toasts.Show(
                    $"{file.Name} 이(가) 너무 큽니다 — 본문 그림은 한 장 {ContentImages.MaxText} 까지입니다.",
                    NoticeTone.Warning);
                continue;
            }

            _pasting++;
            StateHasChanged();

            try
            {
                // 브라우저 스트림은 **이 상호작용 동안에만** 읽을 수 있다.
                // 그대로 HttpClient 에 물리지 않고 여기서 다 받아 둔다.
                using var buffer = new MemoryStream();
                await using (var source = file.OpenReadStream(ContentImages.MaxBytes))
                {
                    await source.CopyToAsync(buffer);
                }

                buffer.Position = 0;

                var uploaded = await Images.UploadAsync(buffer, file.Name, file.ContentType);

                if (uploaded.Url is null)
                {
                    Toasts.Show(uploaded.Error ?? "그림을 올리지 못했습니다.", NoticeTone.Error);
                    continue;
                }

                await InsertImageAsync(uploaded.Url, file.Name);
            }
            catch (IOException ex)
            {
                Toasts.Show($"{file.Name} 을(를) 읽지 못했습니다 — {ex.Message}", NoticeTone.Warning);
            }
            catch (TimeoutException)
            {
                // 바이트가 끝내 안 왔다. **여기서 새어 나가면 회로가 끊긴다.**
                Toasts.Show($"{file.Name} 을(를) 받는 도중 끊겼습니다. 다시 넣어 주십시오.", NoticeTone.Warning);
            }
            catch (JSException ex)
            {
                Toasts.Show($"{file.Name} 을(를) 다루지 못했습니다 — {ex.Message}", NoticeTone.Warning);
            }
            finally
            {
                _pasting--;
                StateHasChanged();
            }
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
            Toasts.Show($"그림을 글에 넣지 못했습니다. 주소: {url}", NoticeTone.Warning);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private async Task SubmitAsync()
    {
        if (_saving || OnSubmit is null)
        {
            return;
        }

        // 편집기가 아직 알려 주지 않은 마지막 줄까지 여기서 받아 온다.
        await FlushAsync();

        if (RequestContentHtml.IsBlank(_markup))
        {
            Toasts.Show("남길 내용을 적으십시오.", NoticeTone.Warning);
            return;
        }

        _saving = true;
        try
        {
            // 가로채기를 지나온 그림이 남아 있으면 여기서 파일로 바꾼다.
            var absorbed = await Images.AbsorbAsync(_markup);

            if (absorbed.Url is null)
            {
                Toasts.Show(absorbed.Error ?? "그림을 올리지 못했습니다.", NoticeTone.Error);
                return;
            }

            // 화면에도 반영해 둔다. 저장이 뒤에서 실패했을 때 편집기에 남아
            // 있는 것이 다시 base64 이면 누를 때마다 같은 일을 되풀이한다.
            _markup = absorbed.Url;

            if (await OnSubmit(RequestContentHtml.ToStored(_markup)))
            {
                _markup = string.Empty;

                // 빈 칸으로 돌아왔으니 도구줄도 접는다. 다시 짚으면 또 선다.
                _writing = false;
            }
        }
        finally
        {
            _saving = false;
        }
    }

    /// <summary>
    /// 편집기에서 <b>지금 화면에 있는 글</b>을 읽어 온다.
    /// </summary>
    /// <remarks>
    /// <b>읽지 못하면 있던 값을 그대로 둔다.</b> 여기서 비우면 잘 써 둔 글까지
    /// 날아가고, 증상이 「가끔 빈 댓글이 올라간다」로만 보인다.
    /// </remarks>
    private async Task FlushAsync()
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
                _markup = markup;
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
}
