using DevExpress.Blazor;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Globalization;
using System.Net;
using System.Text.Json;
using JSini.Web.Abstractions;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.HelpDesk.Api;
using JSini.Web.Http;

namespace JSini.Web.HelpDesk.Components.Pages;

public partial class RequestDetail : IDisposable
{
    [Inject] private HelpDeskApi Api { get; set; } = default!;
    [Inject] private HelpDeskContext Context { get; set; } = default!;
    [Inject] private PortalTabs Tabs { get; set; } = default!;
    [Inject] private GatewayClient Gateway { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>주소의 요청 키.</summary>
    [Parameter] public string Id { get; set; } = string.Empty;

    // 주소에 요청 번호가 박혀 있어 권한표 열쇠(DB 메뉴 path)를 직접 박아 둔다.
    private const string MenuPath = "/helpdesk/request/detail/:id";

    private JsonElement? _request;

    /// <summary>서버가 404 로 답했다 — 「못 읽었다」와 갈라 「없다」고 말한다.</summary>
    private bool _notFound;

    /// <summary>접수·완료를 묻는 창. 화면 맨 아래에 하나만 둔다.</summary>
    private ConfirmDialog? _confirm;

    /// <summary>뿌리 댓글들. 그 아래 답글이 나무로 달려 있다.</summary>
    private IReadOnlyList<CommentNode> _roots = [];

    /// <summary>받아 온 댓글 수(답글까지 전부). 머리글에 적는다.</summary>
    private int _commentCount;

    /// <summary>답글 칸이 열린 댓글 번호. 편집기가 여럿 살지 않게 하나만 연다.</summary>
    private int? _replyTo;

    /// <summary>이 화면의 탭 주소. 요청마다 다르므로 탭도 따로 선다.</summary>
    private string Href => $"/helpdesk/request/detail/{Id}";

    /// <summary>탭과 머리에 적을 이름. 제목이 오기 전에는 번호다.</summary>
    private string TabTitle =>
        Value("title") is { Length: > 0 } title ? $"#{Id} {title}" : $"요청 #{Id}";

    /// <summary>완료 또는 종료된 요청인가 — 댓글 작성, 삭제가 제한된다.</summary>
    private bool IsFinished => Status is "Completed" or "UserCompleted";

    /// <summary>댓글을 남길 수 있는가. 댓글 칸과 답글 단추가 이 값 하나를 본다.</summary>
    private bool CanWrite => !IsFinished && Context.IsLinked && Permissions.Can(MenuPath, MenuAction.Create);

    /// <summary>시스템관리자이고 아직 안 끝난 요청이면 지울 수 있다.</summary>
    private bool CanDeleteRequest => Context.IsSystemAdmin && !IsFinished;

    /// <summary>
    /// 접수·완료를 누를 수 있는가. 연결이 아니라 담당자 여부를 본다 —
    /// 담당자 줄은 서버가 접수할 때 만든다.
    /// </summary>
    private bool CanHandle => Context.IsAdmin && Permissions.Can(MenuPath, MenuAction.Update);

    /// <summary>보고 있는 사람이 글쓴이인가. <c>createdBy</c> 를 먼저, 옛 글은 고객 번호로 본다.</summary>
    private bool IsAuthor
    {
        get
        {
            var me = Context.Identity?.JsiniUserId;

            if (!string.IsNullOrWhiteSpace(me)
                && string.Equals(Value("createdBy"), me, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return Context.CustomerId is { } mine
                && int.TryParse(Value("customerId") ?? NestedValue("customer", "id"), out var owner)
                && owner == mine;
        }
    }

    /// <summary>
    /// 접수자가 완료로 닫은 내 글에만 종료를 내놓는다.
    /// 누를 사람이 대개 고객이라 메뉴 권한은 보지 않는다.
    /// </summary>
    private bool CanClose => IsAuthor && Status is "Completed";

    /// <summary>아직 아무도 안 맡은 글인가.</summary>
    private bool IsPending => Status is "Pending";

    /// <summary>아직 안 끝난 글인가. 「대기」에서 바로 완료하면 서버가 접수까지 반영한다.</summary>
    private bool IsOpen =>
        Status is "Pending" or "InProgress" or "Consultation" or "Negotiation";

    /// <summary>상태의 열거형 이름. 숫자로 오면 이름으로 푼다.</summary>
    private string? Status => Value("status") switch
    {
        null => null,
        var raw when int.TryParse(raw, out var code) => StatusName(code),
        var raw => raw,
    };

    /// <summary>
    /// 상태만 보내고 접수자는 서버가 정한다.
    /// 반영했는지를 돌려준다 — 재요청이 그것으로 갈린다.
    /// </summary>
    private async Task<bool> ChangeStatusAsync(string status, string label, string? ask = null)
    {
        if (!int.TryParse(Id, out _))
        {
            Say("요청 번호를 읽지 못했습니다.", NoticeTone.Error);
            return false;
        }

        ask ??= (status, IsPending) switch
        {
            // 「대기」에서 바로 닫는 길이라 무슨 일이 한꺼번에 일어나는지 적는다.
            ("Completed", true) => $"「{TabTitle}」 을(를) 접수하고 바로 완료로 닫습니다.",

            // 글 주인의 마지막 걸음이라 **무엇을 확인하는 것인지** 적는다.
            // 여기서 「예」를 누르면 담당자 전원에게 종료 알림이 나간다.
            ("UserCompleted", _) => $"「{TabTitle}」 의 처리 결과를 확인했습니다. 요청을 종료합니다.",

            _ => $"「{TabTitle}」 을(를) {label} 처리합니다.",
        };

        if (_confirm is not null && !await _confirm.AskAsync(ask, "요청 처리", label, ButtonRenderStyle.Primary))
        {
            return false;
        }

        var done = await RunAsync(
            () => Api.PutAsync($"requests/accept/{Id}", new { status }),
            $"{label} 처리했습니다.", $"{label} 처리하지 못했습니다");

        if (done)
        {
            await ReloadAsync();
        }

        StateHasChanged();

        return done;
    }

    /// <summary>「접수」 — 내가 맡는다. 접수자와 접수일자가 박힌다.</summary>
    private Task AcceptAsync() => ChangeStatusAsync("InProgress", "접수");

    /// <summary>「완료」 — 「대기」에서 누르면 접수까지 함께 반영된다.</summary>
    private Task CompleteAsync() => ChangeStatusAsync("Completed", "완료");

    /// <summary>「지시작업으로보내기」 — AI 작업 지시로 넘긴다.</summary>
    private async Task SendToAiTaskAsync()
    {
        if (!int.TryParse(Id, out var requestId))
        {
            Say("요청 번호를 읽지 못했습니다.", NoticeTone.Error);
            return;
        }

        var ask = $"「{TabTitle}」 을(를) AI 작업 지시로 보냅니다.";
        if (_confirm is not null && !await _confirm.AskAsync(ask, "작업지시", "보내기", ButtonRenderStyle.Info))
        {
            return;
        }

        var title = Value("title");
        var htmlContents = Value("description") ?? Value("content") ?? "";
        var contents = ConvertHtmlToPlainTextWithImages(htmlContents);

        var payload = new
        {
            title = $"[요청 #{requestId}] {title}",
            contents = contents,
            contentFormat = "text",
            taskStatus = "idle",
            requestFlag = "none"
        };

        await RunAsync(
            () => Gateway.PostAsync("projmng/ai-tasks", payload),
            "AI 작업 지시로 보냈습니다.", "AI 작업 지시로 보내지 못했습니다.");
    }

    /// <summary>「삭제」 — 시스템관리자가 요청을 지운다.</summary>
    private async Task DeleteAsync()
    {
        if (!CanDeleteRequest)
        {
            Say("완료되거나 종료된 요청은 삭제할 수 없습니다.", NoticeTone.Warning);
            return;
        }

        if (!int.TryParse(Id, out _))
        {
            Say("요청 번호를 읽지 못했습니다.", NoticeTone.Error);
            return;
        }

        if (_confirm is not null && !await _confirm.AskAsync($"「{TabTitle}」 을(를) 삭제하시겠습니까?", "요청 삭제", "삭제", ButtonRenderStyle.Danger))
        {
            return;
        }

        var done = await RunAsync(
            () => Api.DeleteAsync($"requests/{Id}"),
            "삭제했습니다.", "삭제하지 못했습니다");

        if (done)
        {
            var fallback = Tabs.Close(Href);
            Navigation.NavigateTo(fallback ?? "/helpdesk/request/manage");
        }
    }

    /// <summary>「종료(최종확인)」 — 글 주인이 결과를 보고 닫는다. 접수자는 바뀌지 않는다.</summary>
    private Task CloseAsync() => ChangeStatusAsync("UserCompleted", "종료");

    /// <summary>「재요청」 — 이 글을 닫고, 닫혔을 때만 같은 내용으로 등록 화면을 연다.</summary>
    private async Task ResubmitAsync()
    {
        var ask = $"「{TabTitle}」 을(를) 종료하고, 같은 내용으로 새 요청을 작성합니다.";

        if (!await ChangeStatusAsync("UserCompleted", "재요청", ask))
        {
            return;
        }

        Navigation.NavigateTo($"/helpdesk/request/new?resubmit={Id}");
    }

    /// <summary>같은 화면에서 다른 요청으로 옮겨도 부품이 재사용되므로 여기서 읽는다.</summary>
    protected override async Task OnParametersSetAsync()
    {
        // 탭을 먼저 열고, 제목이 오면 이름만 갈아 준다.
        Tabs.Open(Href, TabTitle, standalone: true);

        await ReloadAsync();
    }

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _notFound = false;

        // 내가 누구인지 먼저 안다 — 댓글을 남길 수 있는지가 그것으로 갈린다.
        await Context.LoadIdentityAsync();

        // 본문과 댓글을 나란히. 서로 기다릴 이유가 없다.
        var request = FetchRequestAsync();
        var comments = Api.GetListAsync<ImprovementComment>($"requests/{Id}/comments");

        await Task.WhenAll(request, comments);

        _request = request.Result;
        _commentCount = comments.Result.Count;
        _roots = CommentTree.Build(comments.Result);

        // 제목이 왔으니 탭 이름을 갈아 준다.
        Tabs.Open(Href, TabTitle, standalone: true);

        return _request is null ? 0 : 1;
    }, NotFoundMessage, "요청을 읽지 못했습니다");

    // 남이 단 댓글이 보이도록 열려 있는 동안 댓글 목록만 되읽는다.

    /// <summary>댓글을 되읽는 사이.</summary>
    private static readonly TimeSpan CommentPoll = TimeSpan.FromSeconds(20);

    /// <summary>들여다보기를 멈추는 손잡이. 화면을 떠날 때 당긴다.</summary>
    private CancellationTokenSource? _watch;

    private bool _shouldScrollToHash = true;

    // 들여다보기는 붙은 회로에서만 건다 — prerender 에서는 아무도 안 본다.
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _watch = new CancellationTokenSource();
            _ = WatchCommentsAsync(_watch.Token);
        }

        if (_shouldScrollToHash && _roots.Count > 0 && !Loading)
        {
            _shouldScrollToHash = false;
            await ScrollToHashAsync();
        }
    }

    private async Task ScrollToHashAsync()
    {
        var uri = Navigation.ToAbsoluteUri(Navigation.Uri);
        if (!string.IsNullOrEmpty(uri.Fragment) && uri.Fragment.StartsWith("#comment-"))
        {
            var elementId = uri.Fragment.Substring(1);
            try
            {
                await JS.InvokeVoidAsync("eval", $@"
                    setTimeout(() => {{
                        const el = document.getElementById('{elementId}');
                        if (el) {{
                            el.scrollIntoView({{ behavior: 'smooth', block: 'center' }});
                            el.classList.remove('hd-cmt--highlight');
                            void el.offsetWidth;
                            el.classList.add('hd-cmt--highlight');
                        }}
                    }}, 300);
                ");
            }
            catch
            {
                // 스크롤 실패는 무시한다.
            }
        }
    }

    /// <summary>
    /// 댓글 수가 달라졌을 때만 다시 그린다.
    /// 답글을 쓰는 중이면 건너뛰고, 실패는 다음 바퀴로 넘긴다.
    /// </summary>
    private async Task WatchCommentsAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(CommentPoll);

        try
        {
            while (await timer.WaitForNextTickAsync(token))
            {
                if (_notFound || _replyTo is not null)
                {
                    continue;
                }

                try
                {
                    var comments = await Api.GetListAsync<ImprovementComment>($"requests/{Id}/comments");

                    if (comments.Count == _commentCount)
                    {
                        continue;
                    }

                    _commentCount = comments.Count;
                    _roots = CommentTree.Build(comments);

                    await InvokeAsync(StateHasChanged);
                }
                catch (Exception ex) when (ex is ApiException or HttpRequestException or JsonException)
                {
                    // 다음 바퀴에 다시 본다.
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 화면을 떠났다.
        }
    }

    /// <summary>떠날 때 들여다보기를 멈춘다.</summary>
    public void Dispose()
    {
        _watch?.Cancel();
        _watch?.Dispose();
        _watch = null;
    }

    /// <summary>요청 하나를 읽는다. 404 만 <c>null</c> 로 바꾸고 나머지 실패는 그대로 올린다.</summary>
    private async Task<JsonElement?> FetchRequestAsync()
    {
        try
        {
            return await Api.GetAsync<JsonElement>($"requests/{Id}");
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _notFound = true;
            return null;
        }
    }

    /// <summary>없는 요청 안내. 숫자 뒤 조사가 갈리지 않게 「…번 요청을」로 잇는다.</summary>
    private string NotFoundMessage =>
        $"{Id}번 요청을 찾지 못했습니다. 지워졌거나 주소가 잘못되었습니다.";

    /// <summary>답글 칸을 열고 닫는다. <c>null</c> 이면 닫기다.</summary>
    private void ToggleReply(int? commentId) => _replyTo = commentId;

    /// <summary>요청글에 바로 다는 댓글.</summary>
    private Task<bool> AddRootAsync(string html, IReadOnlyList<PickedFile> files) => AddAsync(null, html, files);

    /// <summary>댓글에 다는 답글. 깊이는 따지지 않는다 — 대댓글도 같은 길이다.</summary>
    private Task<bool> AddReplyAsync(int parentId, string html, IReadOnlyList<PickedFile> files) => AddAsync(parentId, html, files);

    /// <summary>댓글을 남긴다. 돌려주는 값이 편집기를 비울지를 정한다.</summary>
    private async Task<bool> AddAsync(int? parentCommentId, string html, IReadOnlyList<PickedFile> files)
    {
        if (IsFinished)
        {
            Say("완료되거나 종료된 요청에는 댓글을 남길 수 없습니다.", NoticeTone.Warning);
            return false;
        }

        if (!int.TryParse(Id, out var requestId))
        {
            Say("요청 번호를 읽지 못했습니다.", NoticeTone.Error);
            return false;
        }

        ImprovementComment? created = null;
        var added = await RunAsync(
            async () =>
            {
                created = await Api.PostAsync<ImprovementComment>("comments", new
                {
                    requestId,
                    commentText = html,

                    // 칸 이름이 어긋나면 조용히 뿌리 댓글로 들어간다.
                    parentCommentId,

                    // 담당자와 고객은 번호 체계가 달라 종류를 함께 보낸다.
                    authorType = Context.Identity?.LoginType ?? "admin",
                    authorId = Context.HelpdeskUserId ?? 0,
                });
            },
            "남겼습니다.", "남기지 못했습니다");

        if (added && created is not null)
        {
            var commentId = created.Id;
            if (files.Count > 0)
            {
                var failedUploads = await UploadFilesAsync(commentId, files);
                if (failedUploads > 0)
                {
                    Say($"{failedUploads}개 첨부파일을 올리지 못했습니다.", NoticeTone.Warning);
                }
            }

            _replyTo = null;
            await ReloadAsync();
        }

        // 값을 돌려주는 대리자라 Blazor 가 스스로 다시 그리지 않는다.
        StateHasChanged();

        return added && created is not null;
    }

    /// <summary>댓글 첨부파일을 올리고 실패한 개수를 돌려준다.</summary>
    private async Task<int> UploadFilesAsync(int commentId, IReadOnlyList<PickedFile> files)
    {
        if (files.Count == 0)
        {
            return 0;
        }

        var streams = new List<Stream>(files.Count);
        try
        {
            using var form = new MultipartFormDataContent
            {
                { new StringContent("ImprovementComment"), "entityType" },
                { new StringContent(commentId.ToString()), "entityId" },
            };

            foreach (var file in files)
            {
                var stream = file.OpenRead();
                streams.Add(stream);

                var part = new StreamContent(stream);
                part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);

                form.Add(part, "files", file.Name);
            }

            await Api.PostMultipartAsync<JsonElement?>("files/upload", form);
            return 0;
        }
        catch (ApiException)
        {
            return files.Count;
        }
        finally
        {
            foreach (var stream in streams)
            {
                await stream.DisposeAsync();
            }
        }
    }

    /// <summary>댓글을 지운다(논리 삭제).</summary>
    private async Task<bool> DeleteCommentAsync(int commentId)
    {
        if (IsFinished)
        {
            Say("완료되거나 종료된 요청의 댓글은 삭제할 수 없습니다.", NoticeTone.Warning);
            return false;
        }

        if (_confirm is not null && !await _confirm.AskAsync("댓글을 삭제하시겠습니까?", "댓글 삭제", "삭제", ButtonRenderStyle.Danger))
        {
            return false;
        }

        var deleted = await RunAsync(
            () => Api.DeleteAsync($"comments/{commentId}"),
            "삭제했습니다.", "삭제하지 못했습니다.");

        if (deleted)
        {
            await ReloadAsync();
        }
        
        StateHasChanged();
        
        return deleted;
    }

    /// <summary>응답에서 칸 하나를 글자로 꺼낸다. 없으면 <c>-</c>.</summary>
    private string Text(string name) => Value(name) ?? "-";

    /// <summary>「이름(계정)」으로 적는다. 둘이 같으면 한 번만, 한쪽만 있으면 있는 쪽을.</summary>
    private static string Who(string? name, string? loginId)
    {
        var hasName = !string.IsNullOrWhiteSpace(name);
        var hasId = !string.IsNullOrWhiteSpace(loginId);

        return (hasName, hasId) switch
        {
            (true, true) when !string.Equals(name, loginId, StringComparison.Ordinal) => $"{name}({loginId})",
            (true, _) => name!,
            (_, true) => loginId!,
            _ => "-",
        };
    }

    /// <summary>요청자. 계정은 실제로 글을 쓴 <c>createdBy</c> 가 먼저다.</summary>
    private string RequesterName() =>
        Who(Value("requesterName") ?? NestedValue("customer", "userName"),
            Value("createdBy") ?? NestedValue("customer", "loginId"));

    /// <summary>고객사 이름(서버가 푼다). 못 풀었으면 회사 아이디를 보인다.</summary>
    private string CompanyName() =>
        Value("companyName")
        ?? NestedValue("customer", "companyId")
        ?? "-";

    /// <summary>상태 배지의 색. 목록 화면(<c>RequestManage</c>)과 같은 표다.</summary>
    private string StatusClass() => Status switch
    {
        "Completed" or "UserCompleted" => "jsini-badge--on",
        "InProgress" or "Consultation" or "Negotiation" => "jsini-badge--warn",
        "Rejected" => "jsini-badge--off",
        _ => string.Empty,
    };

    /// <summary>접수자. 이름은 <c>admin.userName</c> 에 있고, 없으면 「미배정」이다.</summary>
    private string AssigneeName()
    {
        var name = NestedValue("admin", "userName") ?? Value("adminName");
        var loginId = NestedValue("admin", "loginId");

        return name is null && loginId is null ? "미배정" : Who(name, loginId);
    }

    /// <summary>상태를 사람이 읽는 말로. 서버가 준 <c>statusName</c> 이 먼저다.</summary>
    private string StatusText() =>
        Value("statusName") is { Length: > 0 } given ? given : StatusLabel(Status);

    /// <summary>날짜 칸을 분까지 적는다. 없으면 <c>-</c>, 못 읽으면 온 그대로.</summary>
    private string When(string name)
    {
        var raw = Value(name);

        if (string.IsNullOrWhiteSpace(raw))
        {
            return "-";
        }

        return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at)
            ? at.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
            : raw;
    }

    /// <summary>열거형 이름 → 사람이 읽는 말. 목록 화면과 같은 표다.</summary>
    private static string StatusLabel(string? status) => status switch
    {
        "Pending" => "대기",
        "InProgress" => "진행",
        "Rejected" => "반려",
        "Completed" => "완료",
        "UserCompleted" => "종료",
        "Consultation" => "협의",
        "Negotiation" => "논의",
        _ => status ?? "-",
    };

    /// <summary>상태 순번 → 열거형 이름. <c>ImprovementStatus</c> 선언 차례와 맞춘다.</summary>
    private static string? StatusName(int code) => code switch
    {
        0 => "Pending",
        1 => "InProgress",
        2 => "Rejected",
        3 => "Completed",
        4 => "Delete",
        5 => "Consultation",
        6 => "Negotiation",
        7 => "UserCompleted",
        _ => null,
    };

    private string? NestedValue(string parentName, string name) =>
        _request is { ValueKind: JsonValueKind.Object } obj
        && obj.TryGetProperty(parentName, out var parent)
        && parent.ValueKind == JsonValueKind.Object
        && parent.TryGetProperty(name, out var value)
        && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
            ? value.ToString()
            : null;

    /// <summary>본문(<c>description</c>)을 걸러 넣는다 — 고객이 쓴 HTML 이다.</summary>
    private MarkupString Body()
    {
        var html = Value("description") ?? Value("content");
        return new MarkupString(NoticeHtml.Sanitize(html));
    }

    /// <summary>응답에서 칸 하나를 꺼낸다. 없으면 <c>null</c>.</summary>
    private string? Value(string name) =>
        _request is { ValueKind: JsonValueKind.Object } obj
        && obj.TryGetProperty(name, out var value)
        && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
            ? value.ToString()
            : null;

    /// <summary>HTML 본문을 글자로 바꾸되 그림 주소는 남긴다.</summary>
    private static string ConvertHtmlToPlainTextWithImages(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var text = System.Text.RegularExpressions.Regex.Replace(html, @"<(br|p|div)[^>]*>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        text = System.Text.RegularExpressions.Regex.Replace(text, @"<img\b[^>]*?\bsrc\s*=\s*(?:""(?<src>[^""]*)""|'(?<src>[^']*)')[^>]*>", match =>
        {
            var src = match.Groups["src"].Value;
            return $"\n[이미지: {src}]\n";
        }, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        text = System.Text.RegularExpressions.Regex.Replace(text, @"<[^>]+>", string.Empty);
        text = System.Net.WebUtility.HtmlDecode(text);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\n{3,}", "\n\n").Trim();

        return text;
    }
}
