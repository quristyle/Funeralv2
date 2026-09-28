using DevExpress.Blazor;
using Microsoft.AspNetCore.Components;
using System.Globalization;
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

    /// <summary>접수·완료를 묻는 창. 화면 맨 아래에 하나만 둔다.</summary>
    private ConfirmDialog? _confirm;

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
    /// 접수·완료를 누를 수 있는가 — <b>담당자</b>이고 이 화면에 수정 권한이 있는가.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="HelpDeskContext.IsLinked"/> 가 아니라
    /// <see cref="HelpDeskContext.IsAdmin"/> 을 본다. 포털 역할로만 담당자인
    /// 사람(헬프데스크에 줄이 없는 관리자)도 접수할 수 있어야 하고, 그 사람의
    /// <c>admin</c> 줄은 <b>서버가 접수하는 그 순간 만든다</b>
    /// (<c>IAssigneeProvisioner</c>). 연결을 먼저 요구하면 새 DB 에서는
    /// <b>아무도 아무것도 못 맡는다</b> — 담당자가 0명이기 때문이다.
    /// </para>
    /// <para>
    /// 고객은 여기 들어오지 못한다. 자기 글을 스스로 「완료」로 닫는 길은
    /// 따로 있다(<c>UserCompleted</c> — 종료).
    /// </para>
    /// </remarks>
    private bool CanHandle => Context.IsAdmin && Permissions.Can(MenuPath, MenuAction.Update);

    /// <summary>아직 아무도 안 맡은 글인가.</summary>
    private bool IsPending => Status is "Pending";

    /// <summary>
    /// 아직 끝나지 않은 글인가 — 「완료」를 내놓을지 가른다.
    ///
    /// <para>
    /// <b>「대기」도 들어간다.</b> 묻고 답하다 그 자리에서 끝난 글을
    /// 「접수」부터 두 번 누르게 하지 않는다 — 서버가 접수와 완료를 한 번에
    /// 반영한다.
    /// </para>
    /// </summary>
    private bool IsOpen =>
        Status is "Pending" or "InProgress" or "Consultation" or "Negotiation";

    /// <summary>
    /// 지금 상태의 <b>열거형 이름</b>. 서버는 <c>status</c> 에 이름을 싣는다
    /// (<c>JsonStringEnumConverter</c>) — 숫자로 올 때를 대비해 그것도 푼다.
    /// </summary>
    private string? Status => Value("status") switch
    {
        null => null,
        var raw when int.TryParse(raw, out var code) => StatusName(code),
        var raw => raw,
    };

    /// <summary>
    /// 접수·완료를 서버에 반영한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>상태만 보낸다.</b> 접수자는 서버가 지금 부른 사람으로 정한다 —
    /// 이 화면이 아는 번호(<see cref="HelpDeskContext.HelpdeskUserId"/>)는
    /// 담당자로 이어 둔 계정일 때만 <c>admin.id</c> 라, 그대로 실어 보내면
    /// <b>번호가 겹치는 남이 접수자로 박힌다.</b>
    /// </para>
    /// <para>
    /// 「완료」는 접수일자까지 한 번에 넣으므로 <b>「대기」에서 바로 눌러도</b>
    /// 접수자·접수일자·완료일자가 모두 남는다.
    /// </para>
    /// </remarks>
    private async Task ChangeStatusAsync(string status, string label)
    {
        if (!int.TryParse(Id, out _))
        {
            Say("요청 번호를 읽지 못했습니다.", NoticeTone.Error);
            return;
        }

        var ask = status == "Completed" && IsPending
            // 「대기」에서 바로 닫는 길이라 무슨 일이 한꺼번에 일어나는지 적는다.
            ? $"「{TabTitle}」 을(를) 접수하고 바로 완료로 닫습니다."
            : $"「{TabTitle}」 을(를) {label} 처리합니다.";

        if (_confirm is not null && !await _confirm.AskAsync(ask, "요청 처리", label, ButtonRenderStyle.Primary))
        {
            return;
        }

        var done = await RunAsync(
            () => Api.PutAsync($"requests/accept/{Id}", new { status }),
            $"{label} 처리했습니다.", $"{label} 처리하지 못했습니다");

        if (done)
        {
            await ReloadAsync();
        }

        StateHasChanged();
    }

    /// <summary>「접수」 — 내가 맡는다. 접수자와 접수일자가 박힌다.</summary>
    private Task AcceptAsync() => ChangeStatusAsync("InProgress", "접수");

    /// <summary>
    /// 「완료」 — 끝났다. 「대기」에서 눌렀으면 접수까지 함께 반영된다.
    /// </summary>
    private Task CompleteAsync() => ChangeStatusAsync("Completed", "완료");

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

    /// <summary>
    /// 사람 하나를 <b>「이름(계정)」 한 덩이</b>로 적는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 전에는 「요청자」와 「작성자(계정)」이 <b>따로 선 두 줄</b>이었다. 그 둘은
    /// 거의 언제나 같은 사람이라(글을 쓴 포털 계정이 그대로 요청자가 된다 —
    /// 서버의 <c>IRequesterProvisioner</c>) 줄만 하나 더 먹으면서, 읽는 사람이
    /// 위아래를 짝지어 봐야 「이순열이 곧 quristyle」임을 알 수 있었다.
    /// </para>
    /// <para>
    /// <b>이름과 계정이 같으면 한 번만 적는다.</b> 헬프데스크 고객 줄은 이름을
    /// 모를 때 로그인 아이디를 이름 자리에 넣으므로(<c>RequesterProvisioner</c>),
    /// 그대로 이으면 <c>quristyle(quristyle)</c> 이 된다.
    /// </para>
    /// <para>
    /// 한쪽만 있으면 있는 쪽을 적는다. 둘 다 없을 때만 <c>-</c> 다 —
    /// 괄호만 남은 <c>(quristyle)</c> 같은 글자를 내놓지 않는다.
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// 요청자 — <b>이름과 계정을 함께</b> 적는다.
    /// </summary>
    /// <remarks>
    /// 계정은 <c>createdBy</c> 가 먼저다. 그것이 <b>실제로 이 글을 쓴 포털
    /// 계정</b>이고(서버가 폼 값이 아니라 로그인 신원에서 박는다), 담당자가
    /// 남을 대신해 올린 글에서는 고객 줄의 아이디와 갈린다.
    /// </remarks>
    private string RequesterName() =>
        Who(Value("requesterName") ?? NestedValue("customer", "userName"),
            Value("createdBy") ?? NestedValue("customer", "loginId"));

    /// <summary>
    /// 고객사 — <b>글을 쓴 사람의 소속 회사</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 이름은 서버가 풀어 <c>companyName</c> 에 담아 준다. 헬프데스크는 회사를
    /// 스스로 관리하지 않아서, 고객 줄에 박혀 있는 것은 <b>포털 회사 아이디</b>
    /// 뿐이다(<c>customer.companyId</c> — 글을 쓸 때 포털 토큰의 회사가 그대로
    /// 들어간다). 그 아이디만 내려오던 동안 이 자리는 <b>늘 <c>-</c></b> 였다.
    /// </para>
    /// <para>
    /// 서버가 포털을 못 불렀으면 아이디가 대신 온다. 그래도 여기서 한 번 더
    /// 물러서는 것은 <b>옛 응답</b>(아직 안 올라간 백엔드) 때문이다 — 회사를
    /// 통째로 감추는 것보다 아이디라도 보이는 편이 낫다.
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// 접수자 이름. 아직 아무도 안 맡았으면 <b>그렇다고 말한다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>칸 이름이 <c>adminName</c> 이 아니다.</b> 여기서 오래 그 이름을 읽고
    /// 있었는데 서버 응답에 그런 칸이 없어서 담당자 자리가 <b>늘 <c>-</c></b>
    /// 였다 — 서버는 <c>Include(r =&gt; r.Admin)</c> 한 것을 그대로 내려주므로
    /// 이름은 <c>admin.userName</c> 에 있다. 옛 이름도 함께 본다.
    /// </para>
    /// <para>
    /// 비었을 때 <c>-</c> 가 아니라 「미배정」인 것은, 이 칸이 <b>「접수」
    /// 단추를 눌러야 하는지</b>를 말해 주는 자리이기 때문이다. 목록 화면도
    /// 같은 말을 쓴다(<c>RequestManage</c>).
    /// </para>
    /// <para>
    /// 요청자와 <b>같은 모양</b>으로 이름과 계정을 함께 적는다(<see cref="Who"/>).
    /// 담당자는 이름이 겹치는 일이 있어(같은 이름의 계정 둘) 계정이 없으면
    /// 누구에게 물어야 할지 가려지지 않는다.
    /// </para>
    /// </remarks>
    private string AssigneeName()
    {
        var name = NestedValue("admin", "userName") ?? Value("adminName");
        var loginId = NestedValue("admin", "loginId");

        return name is null && loginId is null ? "미배정" : Who(name, loginId);
    }

    /// <summary>상태를 사람이 읽는 말로. 서버가 준 <c>statusName</c> 이 먼저다.</summary>
    private string StatusText() =>
        Value("statusName") is { Length: > 0 } given ? given : StatusLabel(Status);

    /// <summary>
    /// 날짜 칸 하나. <b>없으면 <c>-</c> 다</b> — 아직 안 일어난 일이라는 뜻이다.
    /// </summary>
    /// <remarks>
    /// 서버는 UTC 로 담고 ISO 로 내려준다. 그대로 적으면 <c>T</c> 와 밀리초가
    /// 보이므로 분까지만 끊어 적는다. 읽지 못하는 값은 <b>온 그대로</b> 둔다 —
    /// 서식을 못 맞췄다고 값을 감추면 무엇이 잘못됐는지 알 수 없다.
    /// </remarks>
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

    /// <summary>
    /// 상태 <b>순번</b> → 열거형 이름. 서버가 숫자로 줄 때를 위한 것이다.
    ///
    /// <para>
    /// 번호는 <c>ImprovementStatus</c> 의 선언 차례이고 <b>중간이 비지 않는다</b> —
    /// 그 열거형에 값을 끼워 넣으면 여기도 함께 고친다.
    /// </para>
    /// </summary>
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
