using DevExpress.Blazor;
using Microsoft.AspNetCore.Components;
using System.Globalization;
using System.Net;
using System.Text.Json;
using JSini.Web.Abstractions;
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

    /// <summary>
    /// 그런 요청이 <b>없다</b>(서버가 404 로 답했다).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 「못 읽었다」와 <b>다른 것</b>이라 따로 든다. 못 읽은 것은 다시 눌러
    /// 볼 일이지만 없는 것은 다시 눌러도 없다 — 화면이 그 둘을 같은 말로
    /// 하면 읽는 사람이 새로고침만 반복한다.
    /// </para>
    /// <para>
    /// <b>드물지 않다.</b> 이 화면으로 오는 길 하나가 알림(앱푸시·알림함)인데,
    /// 알림은 글보다 오래 산다 — 글을 지워도 알림함의 줄은 남아 그 주소를
    /// 계속 가리킨다. 그것을 누르면 여기로 와서 404 를 받는다.
    /// </para>
    /// </remarks>
    private bool _notFound;

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
    /// 고객은 여기 들어오지 못한다. 자기 글을 스스로 닫는 길은 따로 있다 —
    /// <see cref="CanClose"/>(<c>UserCompleted</c> — 종료).
    /// </para>
    /// </remarks>
    private bool CanHandle => Context.IsAdmin && Permissions.Can(MenuPath, MenuAction.Update);

    /// <summary>
    /// 이 글을 <b>쓴 사람</b>이 지금 보고 있는 사람인가.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>createdBy</c> 가 먼저다. 그 칸에는 <b>글을 쓸 때 로그인해 있던 포털
    /// 계정</b>이 들어간다 — 서버가 폼 값이 아니라 신원에서 박고
    /// (<c>HttpContext.AuditUser()</c>), 그 값은 신원 조회가 주는
    /// <see cref="HelpdeskIdentity.JsiniUserId"/> 와 같은 것이다.
    /// </para>
    /// <para>
    /// 그 다음이 <b>고객 번호</b>다. 옛 글(옛 헬프데스크에서 들어온 것)은
    /// <c>createdBy</c> 에 포털 계정이 아니라 내부 값이 들어 있거나 아예
    /// 비어 있어서, 그것만 보면 <b>제 글을 쓴 사람이 제 글을 못 닫는다.</b>
    /// 고객으로 이어 둔 계정일 때만 쓸 수 있는 값이라
    /// (<see cref="HelpDeskContext.CustomerId"/>) 담당자 연결에서는 null 이고,
    /// 그때는 첫째 잣대만 선다.
    /// </para>
    /// <para>
    /// <b>담당자인지는 보지 않는다.</b> 자기가 쓴 글이면 담당자여도 그 사람이
    /// 글 주인이다. 반대로 남의 글은 담당자라도 이 값이 거짓이다.
    /// </para>
    /// </remarks>
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
    /// 「종료(최종확인)」을 내놓을 자리인가 — <b>접수자가 완료로 닫아 둔 내 글</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>Completed</c> 하나만 본다. 「대기」·「진행」에서 내놓으면 <b>고친
    /// 것을 보지도 않고 닫는</b> 길이 열리고, 이미 <c>UserCompleted</c> 인
    /// 글에 또 내놓으면 같은 알림이 두 번 나간다.
    /// </para>
    /// <para>
    /// <b>메뉴 권한(<c>Update</c>)을 보지 않는다</b> — <see cref="CanHandle"/>
    /// 과 다른 점이다. 이 단추를 누를 사람은 대개 고객이고, 고객 역할에
    /// 「요청 상세」의 수정 권한이 있으리라고 기대할 수 없다. 요구했다가
    /// 꺼지면 <b>단추가 말없이 사라져</b> 글 주인이 제 글을 영영 못 닫는다.
    /// 무엇을 바꾸는지가 제 글 하나뿐이라 권한으로 가를 것이 없다.
    /// </para>
    /// </remarks>
    private bool CanClose => IsAuthor && Status is "Completed";

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
    /// <para>
    /// <b>반영했는지를 돌려준다.</b> 부르는 쪽이 그 뒤에 할 일이 갈리기
    /// 때문이다 — 「재요청」은 상태가 실제로 넘어갔을 때만 등록 화면으로
    /// 건너가야 한다(<see cref="ResubmitAsync"/>). 안 넘어갔는데 건너가면
    /// <b>닫히지 않은 글이 남은 채 같은 내용이 하나 더</b> 들어간다.
    /// </para>
    /// </remarks>
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

    /// <summary>「완료」 — 끝났다. 「대기」에서 눌렀으면 접수까지 함께 반영된다.
    /// </summary>
    private Task CompleteAsync() => ChangeStatusAsync("Completed", "완료");

    /// <summary>
    /// 「지시작업으로보내기」 — AI 작업지시(AiTask)로 전달한다.
    /// </summary>
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

    /// <summary>
    /// 「삭제」 — 시스템 관리자가 요청을 삭제한다.
    /// </summary>
    private async Task DeleteAsync()
    {
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

    /// <summary>
    /// 「종료(최종확인)」 — <b>글 주인이 결과를 보고 닫는다.</b>
    /// </summary>
    /// <remarks>
    /// 서버는 이 상태에서만 다르게 움직인다(<c>PUT requests/accept/{id}</c>) —
    /// 사용자완료일자(<c>UserCompletededAt</c>)를 박고 <b>접수자는 건드리지
    /// 않는다.</b> 그래서 글 주인이 눌러도 접수자 자리에 그 사람이 들어가지
    /// 않는다. 알림은 담당자 전원에게 나간다.
    /// </remarks>
    private Task CloseAsync() => ChangeStatusAsync("UserCompleted", "종료");

    /// <summary>
    /// 「재요청」 — <b>이 글을 닫으면서 같은 내용으로 새 글을 시작한다.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// 하는 일의 앞쪽 절반은 <see cref="CloseAsync"/> 와 <b>글자 그대로 같다</b> —
    /// 같은 상태(<c>UserCompleted</c>)를 같은 길로 보낸다. 고친 결과가 성에 차지
    /// 않아 다시 부탁하는 자리라도 <b>이 건은 끝난 건이다.</b> 닫지 않고 새 글만
    /// 만들면 같은 이야기가 두 곳에서 동시에 열려 있게 되고, 담당자 쪽 목록에서
    /// 어느 것이 살아 있는 것인지 가릴 길이 없어진다.
    /// </para>
    /// <para>
    /// 뒤쪽 절반은 <b>등록 화면으로 건너가는 것</b>이다. 여기서 새 글을 대신
    /// 만들어 주지 않는다 — 다시 부탁하는 사람에게는 <b>덧붙일 말</b>이 있고
    /// (「이 화면은 아직 그대로입니다」), 그것을 적을 자리가 등록 화면이다.
    /// 그래서 <b>등록까지 하지 않고 「작성 중」인 모습</b>까지만 만들어 준다.
    /// 제목·본문을 채우는 쪽은 <c>RequestNew</c> 다(<c>?resubmit={번호}</c>).
    /// </para>
    /// <para>
    /// <b>상태가 실제로 넘어갔을 때만 건너간다.</b> 서버가 막았는데 건너가면
    /// 닫히지 않은 글이 남은 채 같은 내용이 하나 더 들어간다.
    /// </para>
    /// </remarks>
    private async Task ResubmitAsync()
    {
        var ask = $"「{TabTitle}」 을(를) 종료하고, 같은 내용으로 새 요청을 작성합니다.";

        if (!await ChangeStatusAsync("UserCompleted", "재요청", ask))
        {
            return;
        }

        Navigation.NavigateTo($"/helpdesk/request/new?resubmit={Id}");
    }

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

        // 제목이 왔다. 같은 주소면 `PortalTabs` 가 이름만 갈아 준다.
        Tabs.Open(Href, TabTitle, standalone: true);

        return _request is null ? 0 : 1;
    }, NotFoundMessage, "요청을 읽지 못했습니다");

    // ── 댓글이 달렸는지 **스스로 들여다본다** (2026-10-06) ──────
    //
    // 이 화면은 한 번 읽고 가만히 있었다. 댓글은 **남이 단다** — 담당자가
    // 답을 달아도 열어 둔 사람의 화면에는 아무 일도 안 일어나서, 새로고침을
    // 하기 전에는 온 줄을 모른다. 앱푸시가 가기는 하지만(그 설정은
    // `docs/helpdesk-comment-notify.md`) **지금 이 글을 보고 있는 사람**에게는
    // 알림이 오히려 늦고, 알림을 꺼 둔 사람에게는 오지 않는다.
    //
    // 그래서 열려 있는 동안 댓글 목록만 되읽는다. 본문은 안 읽는다 —
    // 바뀌는 것은 댓글뿐이고, 본문까지 읽으면 화면 전체가 다시 그려져
    // 보던 자리가 흔들린다.

    /// <summary>
    /// 댓글을 다시 읽는 사이. <b>스무 초</b>다.
    /// </summary>
    /// <remarks>
    /// 더 짧게 잡을 까닭이 없다 — 사람이 글을 읽는 동안 몇 초 늦게 뜨는 것은
    /// 아무도 못 느끼지만, 열어 둔 화면 수만큼 게이트웨이를 두드리는 것은
    /// 그대로 쌓인다. 더 길게 잡으면 「지금 답을 기다리는 중」인 사람이
    /// 새로고침을 누르게 되어 들여다보는 뜻이 없어진다.
    /// </remarks>
    private static readonly TimeSpan CommentPoll = TimeSpan.FromSeconds(20);

    /// <summary>들여다보기를 멈추는 손잡이. 화면을 떠날 때 당긴다.</summary>
    private CancellationTokenSource? _watch;

    /// <summary>
    /// 화면이 처음 그려지고 나서 들여다보기를 건다.
    /// </summary>
    /// <remarks>
    /// <b><c>OnParametersSetAsync</c> 가 아니다.</b> 그쪽은 미리 그리기
    /// (prerender)에서도 한 번 돌아서, 거기 걸면 아무도 안 보는 회로가
    /// 타이머를 하나 들고 돈다. 첫 렌더 뒤는 **붙은 회로**에서만 온다.
    ///
    /// 다른 요청으로 옮겨 가도 다시 걸지 않는다 — 돌고 있는 고리가 그때그때
    /// <see cref="Id"/> 를 읽으므로 주소가 바뀌면 다음 바퀴부터 새 글의
    /// 댓글을 본다.
    /// </remarks>
    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _watch = new CancellationTokenSource();
            _ = WatchCommentsAsync(_watch.Token);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 댓글 목록만 되읽어, 수가 달라졌으면 다시 그린다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>수가 같으면 그리지 않는다.</b> 스무 초마다 멀쩡한 화면을 다시
    /// 그리면 읽던 자리가 미세하게 흔들리고, 펼쳐 둔 답글 칸도 함께 흔들린다.
    /// 수로만 보므로 **글자만 고친 댓글**은 놓치지만, 여기서 알아야 하는 것은
    /// 「새로 달렸는가」 하나다.
    /// </para>
    /// <para>
    /// <b>답글 칸이 열려 있으면 그 바퀴는 건너뛴다.</b> 되읽으면 나무가
    /// 통째로 다시 서서 쓰던 글이 날아간다 — 답을 쓰는 중에 벌어지면
    /// 가장 나쁜 일이다. 닫거나 보내고 나면 다음 바퀴부터 다시 본다.
    /// </para>
    /// <para>
    /// <b>실패는 삼킨다.</b> 이것은 사람이 시킨 일이 아니라 화면이 혼자 하는
    /// 일이라, 끊긴 그물 한 번에 토스트가 뜨면 영문 모를 오류로 읽힌다.
    /// 다음 바퀴에 다시 해 보면 된다.
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// 요청 하나를 읽는다. <b>없으면 <c>null</c></b> — 예외로 올리지 않는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 서버는 없는 번호에 <b>404 + <c>"Resource not found."</c></b> 로 답한다
    /// (<c>ApiResponseBuilder.CreateAsync</c> — 결과가 <c>null</c> 이면 그 길이다).
    /// 그대로 두면 <see cref="HelpDeskApi"/> 가 그 글귀를 담아 예외를 던지고,
    /// 화면에는 <b>「요청을 읽지 못했습니다 — Resource not found.」</b> 가 떴다.
    /// 영어인 데다 <b>서버가 터진 것과 구별이 안 된다.</b>
    /// </para>
    /// <para>
    /// 404 만 여기서 삼켜 <c>null</c> 로 바꾼다. 그러면 <c>LoadAsync</c> 가
    /// 「없다」쪽(<see cref="NotFoundMessage"/>)으로 흐르고, 화면도 빈 껍데기
    /// 대신 그 사실을 적는다. <b>나머지 실패는 그대로 올린다</b> — 401·500 을
    /// 「없는 글」로 바꿔 버리면 로그인이 풀린 것이 글이 지워진 것처럼 보인다.
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// 없는 요청을 열었을 때 할 말. 토스트와 화면이 <b>같은 것</b>을 쓴다.
    /// </summary>
    /// <remarks>
    /// 번호를 <b>앞에</b> 두고 「…번 요청을」로 잇는다. 「요청 #9 을(를)」처럼
    /// 숫자 뒤에 조사를 붙이면 읽는 소리에 따라 을·를이 갈려
    /// (9→구<b>를</b> · 8→팔<b>을</b>) 어느 쪽도 늘 맞지가 않는다.
    /// </remarks>
    private string NotFoundMessage =>
        $"{Id}번 요청을 찾지 못했습니다. 지워졌거나 주소가 잘못되었습니다.";

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

    /// <summary>
    /// HTML 본문을 텍스트로 바꾸되 그림 주소는 남긴다.
    /// </summary>
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
