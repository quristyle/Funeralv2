using Microsoft.AspNetCore.Components;
using System.Text.Json;
using JSini.Web.Abstractions;
using JSini.Web.Components.Layout;
using JSini.Web.HelpDesk.Api;

namespace JSini.Web.HelpDesk.Components.Pages;

public partial class RequestDetail
{
    [Inject] private HelpDeskApi Api { get; set; } = default!;
    [Inject] private HelpDeskContext Context { get; set; } = default!;
    [Inject] private PortalTabs Tabs { get; set; } = default!;

    /// <summary>주소의 요청 키.</summary>
    [Parameter] public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 권한표에서 이 화면을 가리키는 열쇠 — <b>DB 메뉴의 <c>path</c></b> 다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>PermissionView</c> 를 그냥 놓으면 <b>지금 열려 있는 주소</b>로 묻는데
    /// (<c>PermissionPath.Current</c>), 이 화면의 주소에는 요청 번호가 박혀
    /// 있다(<c>/helpdesk/request/detail/123</c>). 권한표의 열쇠는
    /// <c>/helpdesk/request/detail/:id</c> 라 <b>영원히 안 맞고</b>, 안 맞으면
    /// 「권한 없음」이라 <b>댓글 칸이 말없이 통째로 사라진다.</b> 사이드바는
    /// 멀쩡하므로 「메뉴는 보이는데 댓글을 못 쓴다」로 나타난다.
    /// </para>
    /// <para>
    /// 그래서 열쇠를 박아 둔다. 자료 번호가 주소에 들어가는 화면은 전부 같은
    /// 사정이다(요청 수정도 그렇다).
    /// </para>
    /// </remarks>
    private const string MenuPath = "/helpdesk/request/detail/:id";

    private JsonElement? _request;

    /// <summary>뿌리 댓글들. 그 아래 답글이 나무로 달려 있다.</summary>
    private IReadOnlyList<CommentNode> _roots = [];

    /// <summary>받아 온 댓글 수(답글까지 전부). 머리글에 적는다.</summary>
    private int _commentCount;

    /// <summary>
    /// 답글 칸이 열려 있는 댓글 번호. <b>한 번에 하나만</b> 연다 —
    /// 여럿을 열면 그림을 붙여넣는 편집기가 화면에 여럿 살아 있게 된다.
    /// </summary>
    private int? _replyTo;

    /// <summary>이 화면의 탭 주소. 요청마다 다르므로 탭도 따로 선다.</summary>
    private string Href => $"/helpdesk/request/detail/{Id}";

    /// <summary>탭과 머리에 적을 이름. 제목이 오기 전에는 번호다.</summary>
    private string TabTitle =>
        Value("title") is { Length: > 0 } title ? $"#{Id} {title}" : $"요청 #{Id}";

    /// <summary>
    /// 댓글을 남길 수 있는가 — <b>연결된 계정</b>이 있고 <b>등록 권한</b>이 있는가.
    /// </summary>
    /// <remarks>
    /// 댓글 칸과 답글 단추가 <b>같은 이 값</b>을 본다. 판정이 두 벌이 되면
    /// 「답글 단추는 있는데 누르면 아무 일도 없다」가 생긴다.
    /// </remarks>
    private bool CanWrite => Context.IsLinked && Permissions.Can(MenuPath, MenuAction.Create);

    /// <summary>
    /// 주소가 바뀌면 다시 읽는다.
    ///
    /// <c>OnInitializedAsync</c> 가 아니라 <c>OnParametersSetAsync</c> 인 이유는,
    /// 같은 화면에서 다른 요청으로 이동할 때 Blazor 가 컴포넌트를 다시 만들지
    /// 않기 때문이다 — 초기화에만 걸어 두면 주소만 바뀌고 내용이 그대로다.
    /// </summary>
    protected override async Task OnParametersSetAsync()
    {
        // 받아 오기 전에 먼저 연다. 탭이 늦게 서면 **화면은 바뀌었는데 탭 줄은
        // 직전 화면을 켜 놓은** 상태가 눈에 띈다. 제목이 오면 이름만 갈린다.
        Tabs.Open(Href, TabTitle, standalone: true);

        await ReloadAsync();
    }

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        // 내가 누구인지 먼저 안다 — 댓글을 남길 수 있는지가 그것으로 갈린다.
        await Context.LoadIdentityAsync();

        // 본문과 댓글을 나란히. 서로 기다릴 이유가 없다.
        var request = Api.GetAsync<JsonElement>($"requests/{Id}");
        var comments = Api.GetListAsync<ImprovementComment>($"requests/{Id}/comments");

        await Task.WhenAll(request, comments);

        _request = request.Result;
        _commentCount = comments.Result.Count;
        _roots = CommentTree.Build(comments.Result);

        // 제목이 왔다. 같은 주소면 `PortalTabs` 가 이름만 갈아 준다.
        Tabs.Open(Href, TabTitle, standalone: true);

        return _request is null ? 0 : 1;
    }, "그런 요청을 찾지 못했습니다.", "요청을 읽지 못했습니다");

    /// <summary>답글 칸을 열고 닫는다. <c>null</c> 이면 닫기다.</summary>
    private void ToggleReply(int? commentId) => _replyTo = commentId;

    /// <summary>요청글에 바로 다는 댓글.</summary>
    private Task<bool> AddRootAsync(string html) => AddAsync(null, html);

    /// <summary>댓글에 다는 답글. 깊이는 따지지 않는다 — 대댓글도 같은 길이다.</summary>
    private Task<bool> AddReplyAsync(int parentId, string html) => AddAsync(parentId, html);

    /// <summary>
    /// 댓글 하나를 남긴다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 작성자를 <b>헬프데스크 내부 아이디</b>로 가리킨다. 포털 아이디가
    /// 아니다 — 그래서 계정 연결이 없으면 남길 수가 없고, 화면이 미리 막는다.
    /// </para>
    /// <para>
    /// 돌려주는 값이 <b>편집기를 비울지</b>를 정한다. 실패했는데 비우면 방금
    /// 쓴 글이 사라지고, 성공했는데 안 비우면 같은 말이 두 번 들어간다.
    /// </para>
    /// </remarks>
    private async Task<bool> AddAsync(int? parentCommentId, string html)
    {
        if (!int.TryParse(Id, out var requestId))
        {
            Say("요청 번호를 읽지 못했습니다.", NoticeTone.Error);
            return false;
        }

        var added = await RunAsync(
            () => Api.PostAsync("comments", new
            {
                requestId,
                commentText = html,

                // 뿌리 댓글이면 null 이다. 서버 모델의 칸 이름이 그대로
                // `parentCommentId` 라 바꿔 적으면 **조용히 뿌리로 들어간다.**
                parentCommentId,

                // 서버가 이 둘로 이름을 찾는다. 담당자와 고객은 번호 체계가
                // 달라서 종류를 함께 보내야 한다.
                authorType = Context.Identity?.LoginType ?? "admin",
                authorId = Context.HelpdeskUserId ?? 0,
            }),
            "남겼습니다.", "남기지 못했습니다");

        if (added)
        {
            _replyTo = null;
            await ReloadAsync();
        }

        // 편집기가 부르는 길은 `EventCallback` 이 아니라 값을 돌려주는
        // 대리자다 — Blazor 가 스스로 다시 그려 주지 않으므로 여기서 부른다.
        StateHasChanged();

        return added;
    }

    /// <summary>
    /// 응답에서 칸 하나를 글자로 꺼낸다.
    ///
    /// DTO 를 두지 않은 이유는 <c>JsonTable</c> 주석과 같다 — 이 응답의 칸이
    /// 백엔드 사정으로 늘고 줄어서, 박아 두면 새 칸이 조용히 사라진다.
    /// </summary>
    private string Text(string name) => Value(name) ?? "-";

    private string RequesterName() =>
        Value("requesterName")
        ?? NestedValue("customer", "userName")
        ?? "-";

    private string? NestedValue(string parentName, string name) =>
        _request is { ValueKind: JsonValueKind.Object } obj
        && obj.TryGetProperty(parentName, out var parent)
        && parent.ValueKind == JsonValueKind.Object
        && parent.TryGetProperty(name, out var value)
        && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
            ? value.ToString()
            : null;

    /// <summary>
    /// 본문을 화면에 넣을 수 있는 HTML 로 만든다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>칸 이름은 <c>description</c> 이다.</b> 여기서 오래 <c>content</c> 를 읽고
    /// 있었는데 서버 응답에 그런 칸이 없어서 본문 자리가 늘 <c>-</c> 였다
    /// (서버는 <c>ImprovementRequest.Description</c> 을 camelCase 로 내려준다).
    /// 옛 이름도 함께 본다 — 서버가 칸을 늘리는 일이 있고, 그때 둘 중 하나만
    /// 보고 있으면 본문이 조용히 사라진다.
    /// </para>
    /// <para>
    /// <c>MarkupString</c> 은 <b>거른 다음에만</b> 쓴다. 본문을 쓰는 사람은
    /// 고객이므로 그 글이 그대로 실행되면 이 포털을 보는 담당자 쪽에서 터진다.
    /// 거르는 규칙은 공지 본문과 같은 한 벌을 쓴다(<c>NoticeHtml</c>) — 거기에
    /// <c>/api/file/…</c> 주소를 셸 중계 경로로 옮기는 일까지 들어 있어,
    /// 본문에 박힌 그림이 포털(:5557)에서도 그대로 보인다.
    /// </para>
    /// </remarks>
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
}
