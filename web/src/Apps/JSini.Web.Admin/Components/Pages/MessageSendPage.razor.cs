using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using JSini.Web.Models;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.Admin.Api;

namespace JSini.Web.Admin.Components.Pages;

public partial class MessageSendPage
{
    [Inject] private AdminClient Api { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>알림구분 목록. 정본은 공통코드(<c>NOTI_CATEGORY</c>)다.</summary>
    [Inject] private PushCategoryClient Categories { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        SchSummary.Or(_filter),
        SchSummary.NameOf(ScopeOptions, o => o.Value, o => o.Text, _scope));

    /// <summary>보낼 수 있는 사람들. 한 번 읽어 두고 거르기는 브라우저에서 한다.</summary>
    private IReadOnlyList<AccountDto> _accounts = [];

    /// <summary>
    /// 고른 사람들의 <b>로그인 아이디</b>. 사람의 내부 번호가 아니다 —
    /// 푸시 구독이 그 값으로 붙어 있다(<see cref="PushOwnerDto"/>).
    /// </summary>
    private readonly HashSet<string> _picked = new(StringComparer.OrdinalIgnoreCase);

    private string _filter = string.Empty;
    private bool _busy;

    // ── 목록에 누구까지 보일까 ──────────────────────────────────

    /// <summary>재직 중인 계정의 상태 값.</summary>
    private const string StatusActive = "ACTIVE";

    /// <summary>
    /// 가입 신청이 아직 승인되지 않은 계정의 상태 값
    /// (AuthServer 의 <c>SignupService.StatusPending</c>). 그 사람은 아직
    /// 로그인하지 못하지만 <b>계정과 이메일 주소는 이미 있다.</b>
    /// </summary>
    private const string StatusPending = "PENDING";

    /// <summary>재직자만 보는 조건 값.</summary>
    private const string ScopeActive = StatusActive;

    /// <summary>재직자에 승인 대기까지 더해 보는 조건 값.</summary>
    private const string ScopeWithPending = "ACTIVE_PENDING";

    /// <summary>
    /// 「상태」 고르개. 한동안 <b>재직자만 / 전부</b> 두 갈래(체크 하나)였다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 가운데 칸을 하나 더 둔 까닭은 <b>승인 대기</b> 때문이다. 가입 신청을
    /// 받아 두고 「소속을 적어 다시 보내 달라」·「승인했으니 들어와 보시라」
    /// 같은 말을 메일로 해야 하는데, 그 사람은 아직 재직자가 아니라 기본
    /// 목록에 없다. 그렇다고 체크를 풀어 전부 보이게 하면 <b>퇴사·잠금까지
    /// 함께 올라와</b> 그만둔 사람에게 메일이 나갈 길이 열린다.
    /// </para>
    /// <para>
    /// 「전체」는 그대로 남긴다 — 잠긴 계정에 안내를 보내는 일이 있다.
    /// </para>
    /// </remarks>
    private static readonly IReadOnlyList<SchOption> ScopeOptions =
    [
        new(ScopeActive, "재직자만"),
        new(ScopeWithPending, "재직자 + 승인 대기"),
        new(null, SchSummary.Any),
    ];

    private string? _scope = ScopeActive;

    /// <summary>거르개에 걸린 사람들. 고르기와 「모두 고르기」가 이것을 본다.</summary>
    private IReadOnlyList<AccountDto> Matched =>
        [.. _accounts.Where(a =>
              InScope(a)
              && (string.IsNullOrWhiteSpace(_filter)
                  || $"{a.UserName} {a.LoginId} {a.DeptName} {a.CompanyName}"
                       .Contains(_filter, StringComparison.OrdinalIgnoreCase)))];

    /// <summary>「상태」 조건에 걸리는가. 모르는 값(<c>null</c>)은 전체다.</summary>
    private bool InScope(AccountDto a) => _scope switch
    {
        ScopeActive => IsActive(a),
        ScopeWithPending => IsActive(a) || IsPending(a),
        _ => true,
    };

    private static bool IsActive(AccountDto a) =>
        string.Equals(a.Status, StatusActive, StringComparison.OrdinalIgnoreCase);

    private static bool IsPending(AccountDto a) =>
        string.Equals(a.Status, StatusPending, StringComparison.OrdinalIgnoreCase);

    /// <summary>계정 관리 화면과 같은 말로 적는다.</summary>
    private static string StatusText(string? status) => status?.ToUpperInvariant() switch
    {
        StatusActive => "정상",
        "LOCKED" => "잠금",
        "RESIGNED" => "퇴사",
        StatusPending => "승인 대기",
        null or "" => "-",
        var other => other,
    };

    private static string StatusClass(string? status) => status?.ToUpperInvariant() switch
    {
        StatusActive => "jsini-badge--on",
        "LOCKED" or StatusPending => "jsini-badge--warn",
        _ => "jsini-badge--off",
    };

    /// <summary>
    /// 고른 사람들의 실제 계정. 아이디만 들고 있으면 주소를 알 수 없다.
    ///
    /// <para>
    /// <b>표의 체크도 이 값을 본다.</b> 표에 넘길 때는 보이는 줄과 겹치는
    /// 것만 쓰이므로(DevExpress 가 자기 자료에 없는 것은 무시한다) 여기서
    /// 따로 거르지 않는다 — 거르면 「안 보이는 사람은 그대로 둔다」는
    /// <see cref="OnPickedChangedAsync"/> 의 규칙과 어긋난다.
    /// </para>
    /// </summary>
    private IReadOnlyList<AccountDto> Picked =>
        [.. _accounts.Where(a => _picked.Contains(a.LoginId))];

    /// <summary>메일을 보낼 수 있는 사람. 주소가 없으면 보낼 데가 없다.</summary>
    private IReadOnlyList<AccountDto> MailTargets =>
        [.. Picked.Where(a => !string.IsNullOrWhiteSpace(a.Email))];

    /// <summary>고른 사람 중 메일 주소가 없는 사람의 이름. 몇 명인지 화면이 말한다.</summary>
    private IReadOnlyList<string> NoEmail =>
        [.. Picked.Where(a => string.IsNullOrWhiteSpace(a.Email)).Select(a => a.UserName)];

    private IReadOnlyList<string> NoPhone =>
        [.. Picked.Where(a => string.IsNullOrWhiteSpace(a.Phone)).Select(a => a.UserName)];

    /// <summary>
    /// 고른 사람 중 아직 승인되지 않은 사람의 이름. 메일 칸이 그 수를 적는다 —
    /// <b>일부러 고른 것</b>이라 막지는 않고 「알고 보내는 것이 맞나」만 묻는다.
    /// </summary>
    private IReadOnlyList<string> PickedPending =>
        [.. Picked.Where(IsPending).Select(a => a.UserName)];

    // ── 메일 받는 사람 ──────────────────────────────────────────

    /// <summary>
    /// 사람이 손으로 고친 받는 사람 칸. <c>null</c> 이면 <b>아직 안 고쳤다</b>는
    /// 뜻이고, 그때는 왼쪽에서 고른 사람의 주소가 그대로 보인다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 「고쳤다/안 고쳤다」를 <c>bool</c> 로 따로 들지 않는다. 값 하나로 두면
    /// <b>맞출 일이 없다</b> — 왼쪽에서 사람을 더 고르거나 조건을 바꾸면
    /// <see cref="MailToText"/> 가 그때그때 다시 계산되고, 손으로 고친 뒤에는
    /// 그 값이 그대로 남는다. 고를 때마다 칸을 따라 고쳐 주는 길로 가면
    /// 고르기·모두 고르기·비우기·다시 읽기 <b>네 곳을 전부 기억해야</b> 하고,
    /// 한 곳만 잊으면 「고쳐 놓은 주소가 가끔 되돌아간다」가 된다.
    /// </para>
    /// </remarks>
    private string? _mailTo;

    /// <summary>왼쪽에서 고른 사람의 주소를 이은 것. 중복은 한 번만.</summary>
    private string PickedMailTo =>
        string.Join(", ", MailTargets
            .Select(a => a.Email!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase));

    /// <summary>받는 사람 칸에 지금 들어 있는 글자.</summary>
    private string MailToText => _mailTo ?? PickedMailTo;

    /// <summary>사람이 손으로 고쳤는가. 고쳤으면 화면이 그 사실을 적는다.</summary>
    private bool MailToEdited => _mailTo is not null;

    /// <summary>
    /// 받는 사람 칸을 주소 하나씩으로 가른다. 쉼표·세미콜론에 <b>줄바꿈까지</b>
    /// 받는다 — 어디선가 복사해 붙이면 줄로 오는 일이 흔하다. 서버는 쉼표·
    /// 세미콜론만 가르므로 <b>보낼 때 쉼표로 다시 잇는다.</b>
    /// </summary>
    private static IEnumerable<string> SplitAddresses(string? text) =>
        (text ?? string.Empty)
            .Split([',', ';', '\n', '\r', '\t'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase);

    /// <summary>주소 꼴인가. <b>서버와 같은 판정</b>을 쓴다(<c>EmailEndpoints</c>).</summary>
    private static bool IsAddress(string value) =>
        System.Net.Mail.MailAddress.TryCreate(value, out _);

    /// <summary>실제로 보낼 주소들.</summary>
    private IReadOnlyList<string> MailRecipients =>
        [.. SplitAddresses(MailToText).Where(IsAddress)];

    /// <summary>
    /// 주소 꼴이 아닌 것들. <b>조용히 빼지 않는다</b> — 서버도 걸러 주지만
    /// 그때는 이미 나머지에게 나간 뒤라, 오타 하나를 고쳐 다시 보내면
    /// 받은 사람은 같은 메일을 두 번 받는다.
    /// </summary>
    private IReadOnlyList<string> MailBadAddresses =>
        [.. SplitAddresses(MailToText).Where(v => !IsAddress(v))];

    /// <summary>받는 사람 칸 옆에 적는 한 줄.</summary>
    private string MailToHint =>
        MailToEdited
            ? $"주소 {MailRecipients.Count}곳 · 직접 고친 값이라 왼쪽에서 더 골라도 채우지 않습니다."
            : $"주소 {MailRecipients.Count}곳 · 왼쪽에서 고른 사람으로 채웁니다.";

    /// <summary>손으로 고친 것을 버리고 고른 사람의 주소로 되돌린다.</summary>
    private Task ResetMailToAsync()
    {
        _mailTo = null;
        return Task.CompletedTask;
    }

    /// <summary>문자 내용의 바이트 수. 한글은 두 바이트라 글자 수로는 못 센다.</summary>
    private int SmsBytes => System.Text.Encoding.UTF8.GetByteCount(_sms.Body ?? string.Empty);

    /// <summary>
    /// 푸시 칸이 들고 있는 값.
    /// </summary>
    /// <remarks>
    /// <b>알림구분의 바닥값을 여기서 준다</b>(<see cref="PushCategoryClient.Notice"/>).
    /// 비운 채로 보낼 수 있게 두면 사람이 손으로 보낸 알림이 구분 없는 줄로
    /// 쌓이고, 그것이 곧 「어디서 빠뜨렸나」를 못 찾게 만든다.
    /// </remarks>
    private readonly PushMessageDto _push = new() { Category = PushCategoryClient.Notice };

    /// <summary>알림구분 고르개의 항목들. <b>「전체」는 없다</b> — 보내는 자리다.</summary>
    private IReadOnlyList<SchOption> _categoryOptions = [];

    private readonly EmailSendRequest _mail = new();

    /// <summary>
    /// 메일에 붙일 파일. <b><see cref="FilePicker"/> 가 자기 임시 파일과 함께
    /// 들고 있고 여기는 참조만 둔다</b> — 부품이 사라지면 그 파일도 지워진다.
    /// </summary>
    private IReadOnlyList<PickedFile> _mailFiles = [];

    /// <summary>메일 본문을 읽어 오는 JS 조각. 처음 보낼 때 한 번만 받는다.</summary>
    private IJSObjectReference? _mailEditorJs;
    private readonly SmsDraft _sms = new();
    private readonly KakaoDraft _kakao = new();

    /// <summary>
    /// 문자 칸이 들고 있는 값. <b>서버로 가지 않는다</b> — 통로가 정해지면
    /// 이 자리가 요청 DTO 로 바뀐다.
    /// </summary>
    private sealed class SmsDraft
    {
        public string? From { get; set; }
        public string? Body { get; set; }
    }

    /// <summary>카카오 칸이 들고 있는 값. 위와 같은 이유로 아직 화면 안에만 있다.</summary>
    private sealed class KakaoDraft
    {
        public string? Channel { get; set; }
        public string? TemplateCode { get; set; }
        public string? Body { get; set; }
        public bool SmsFallback { get; set; } = true;
    }

    /// <summary>
    /// 아이콘 빠른 선택. <b>포털 안의 주소만</b> 둔다 — 바깥 주소를 넣으면
    /// 알림이 뜰 때마다 그쪽으로 요청이 나간다(셸의 매니페스트가 쓰는 것과 같은 파일).
    /// </summary>
    private static readonly (string Label, string Url)[] Icons =
    [
        ("기본", "/icons/icon-192.png"),
        ("큰 아이콘", "/icons/icon-512.png"),
    ];

    protected override async Task OnInitializedAsync()
    {
        // **사람 목록 조회와 묶지 않는다.** 공통코드를 못 읽어도 발송은 되어야
        // 한다 — 그때는 고르개가 비고 바닥값(`NOTICE`)이 그대로 실려 간다.
        _categoryOptions = await Categories.SendOptionsAsync();

        await LoadAsync();
    }

    private Task LoadAsync() => LoadAsync(async () =>
    {
        _accounts = await Api.GetAccountsAsync();

        // 목록에서 사라진 사람이 고른 채로 남지 않게 한다 — 남으면 보낼 때
        // 서버가 모르는 주인 키가 섞인다.
        _picked.RemoveWhere(id => !_accounts.Any(a =>
            string.Equals(a.LoginId, id, StringComparison.OrdinalIgnoreCase)));

        return _accounts.Count;
    }, "보낼 수 있는 사람이 없습니다.", "사람 목록을 읽지 못했습니다");

    /// <summary>
    /// 표에서 체크가 바뀌었다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>표가 준 목록을 그대로 담지 않는다.</b> 표는 <b>지금 보이는 줄</b>만
    /// 가지고 있어서, 조건줄로 목록을 좁히면 안 보이게 된 사람이 체크에서
    /// 빠진 것처럼 온다. 그대로 담으면 <b>부서를 바꿔 찾는 것만으로 앞서 고른
    /// 사람이 조용히 지워진다.</b>
    /// </para>
    ///
    /// <para>
    /// 그래서 <b>지금 보이는 사람의 몫만</b> 갈아 끼운다 — 보이는 줄에서
    /// 빠진 사람은 빼고, 체크된 사람은 넣고, <b>안 보이는 사람은 건드리지
    /// 않는다.</b>
    /// </para>
    /// </remarks>
    private Task OnPickedChangedAsync(IReadOnlyList<AccountDto> items)
    {
        var shown = Matched.Select(a => a.LoginId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var on = items.Select(a => a.LoginId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        _picked.RemoveWhere(id => shown.Contains(id) && !on.Contains(id));

        foreach (var id in on)
        {
            _picked.Add(id);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// <b>보이는 사람만</b> 고른다. 거르개를 무시하고 전부 고르면, 부서 하나를
    /// 찾아 놓고 누른 사람이 회사 전체에 보내게 된다.
    /// </summary>
    private Task PickAllAsync()
    {
        foreach (var account in Matched)
        {
            _picked.Add(account.LoginId);
        }

        return Task.CompletedTask;
    }

    private Task ClearPickedAsync()
    {
        _picked.Clear();
        return Task.CompletedTask;
    }

    // ── 보내기 ──────────────────────────────────────────────────

    /// <summary>
    /// 웹푸시를 보낸다.
    ///
    /// <para>
    /// <b>보낸 것이 0 이어도 실패로 말하지 않는다</b> — 구독이 없거나 본인이
    /// 꺼 둔 것이고, 그 수를 서버가 세어 준다. 「보냈습니다」로만 말하면
    /// 안 받은 사람이 자기 기기를 의심한다.
    /// </para>
    /// </summary>
    private async Task SendPushAsync()
    {
        if (_busy || _picked.Count == 0) return;

        if (string.IsNullOrWhiteSpace(_push.Title))
        {
            Say("알림 제목을 적으십시오.", NoticeTone.Warning);
            return;
        }

        _busy = true;
        try
        {
            PushSendResultDto? result = null;

            var ok = await RunAsync(
                async () => result = await Api.SendPushAsync(new PushSendRequest
                {
                    Owners = [.. _picked.Select(id => new PushOwnerDto { OwnerKey = id })],
                    Message = _push,
                }),
                // 결과를 보고 아래에서 가려 말한다. 빈 문구는 토스트가 넘긴다.
                okMessage: string.Empty, "푸시를 보내지 못했습니다");

            if (!ok) return;

            if (result is { Sent: > 0 })
            {
                Say(Summary(result), NoticeTone.Info);
            }
            else
            {
                Say(result is null
                        ? "보낸 알림이 없습니다."
                        : $"보낸 알림이 없습니다. {Summary(result)}",
                    NoticeTone.Warning);
            }
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>
    /// 고른 파일을 base64 로 옮긴다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>파일 서버에 올리지 않는다.</b> 공지 첨부와 다른 점이 이것이다 —
    /// 공지는 나중에 누가 다시 내려받지만, 메일 첨부는 보내고 나면 쓸 일이
    /// 없다. 올려 두면 <b>아무도 지우지 않는 파일이 보낼 때마다 쌓인다.</b>
    /// </para>
    ///
    /// <para>
    /// 그래서 여기서 읽어 요청에 싣는다. base64 라 실제 크기의 1.33배가
    /// 오가고, 그만큼 상한을 넉넉히 두지 않았다(다섯 개 · 합쳐 15MB).
    /// </para>
    /// </remarks>
    private async Task<List<EmailAttachmentDto>> ReadAttachmentsAsync()
    {
        var files = new List<EmailAttachmentDto>(_mailFiles.Count);

        foreach (var pick in _mailFiles)
        {
            await using var stream = pick.OpenRead();
            using var buffer = new MemoryStream();

            await stream.CopyToAsync(buffer);

            files.Add(new EmailAttachmentDto
            {
                FileName = pick.Name,
                ContentType = pick.ContentType,
                Content = Convert.ToBase64String(buffer.ToArray()),
            });
        }

        return files;
    }

    /// <summary>
    /// 발송 결과를 사람 말로. <b>못 받은 까닭을 함께 적는다</b> — 그것이
    /// 이 화면에서 가장 자주 묻는 질문이다.
    /// </summary>
    private static string Summary(PushSendResultDto r)
    {
        var parts = new List<string> { $"기기 {r.Sent}대로 보냈습니다" };

        if (r.OwnersWithoutSubscription > 0) parts.Add($"등록된 기기가 없는 사람 {r.OwnersWithoutSubscription}명");
        if (r.OptedOut > 0) parts.Add($"푸시를 꺼 둔 사람 {r.OptedOut}명");
        if (r.Failed > 0) parts.Add($"실패 {r.Failed}대");
        if (r.Removed > 0) parts.Add($"죽은 구독 {r.Removed}대를 정리했습니다");

        return string.Join(" · ", parts) + ".";
    }

    /// <summary>
    /// 메일 본문 편집기에서 <b>지금 화면에 있는 글</b>을 읽어 <c>_mail.Body</c> 에 담는다.
    ///
    /// <para>
    /// <c>DxHtmlEditor</c> 에는 「지금 값을 내놔라」가 없다 — <c>MarkupChanged</c> 로
    /// 알려 줄 뿐이고 그 알림이 <c>InputDelay</c>(기본 500ms) 만큼 늦는다.
    /// </para>
    ///
    /// <para>
    /// <b>읽지 못하면 있던 값을 그대로 둔다.</b> 여기서 비우면 잘 들어와 있던
    /// 본문까지 날아가고, 증상이 「가끔 빈 메일이 간다」로만 보인다.
    /// </para>
    /// </summary>
    private async Task FlushBodyAsync()
    {
        try
        {
            _mailEditorJs ??= await JS.InvokeAsync<IJSObjectReference>(
                "import", "./_content/JSini.Web.Admin/js/mail-editor.js");

            var markup = await _mailEditorJs.InvokeAsync<string?>("readMarkup", ".ad-mail-editor");

            if (markup is not null)
            {
                _mail.Body = markup;
            }
        }
        catch (JSException)
        {
            // 브라우저에서 못 읽었다. 파라미터로 올라온 값으로 보낸다.
        }
        catch (InvalidOperationException)
        {
            // 회로가 닫히는 중이다(화면을 옮기는 순간 등). 같은 처리.
        }
    }

    /// <summary>
    /// 메일을 <b>SMTP 로 바로</b> 보낸다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>「받는 사람」 칸이 정본이다 — 고른 사람이 아니라.</b> 그 칸은 왼쪽에서
    /// 고른 사람의 주소로 채워지지만 손으로 고칠 수 있다. 명단에 없는 사람
    /// (거래처 · 아직 계정이 없는 사람)에게 같은 글을 함께 보내는 일이 있고,
    /// 그때까지는 그 사람의 계정을 만들거나 다른 메일 프로그램을 여는 수밖에
    /// 없었다.
    /// </para>
    /// </remarks>
    private async Task SendEmailAsync()
    {
        if (_busy) return;

        var bad = MailBadAddresses;

        if (bad.Count > 0)
        {
            // **보내기 전에 막는다.** 그냥 빼고 보내면 오타 하나가 조용히
            // 사라지고, 나머지에게는 이미 나간 뒤라 되돌릴 수가 없다.
            Say($"주소 꼴이 아닙니다 — {string.Join(" · ", bad.Take(5))}{(bad.Count > 5 ? " 외" : "")}",
                NoticeTone.Warning);
            return;
        }

        var targets = MailRecipients;

        if (targets.Count == 0)
        {
            Say("받는 사람 주소를 적으십시오. 왼쪽에서 사람을 고르면 그 주소로 채워집니다.",
                NoticeTone.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(_mail.Subject))
        {
            Say("메일 제목을 적으십시오.", NoticeTone.Warning);
            return;
        }

        // **본문은 여기서 편집기에게 직접 묻는다.** 파라미터로 올라오는 값은
        // 「친 다음 잠깐 뒤」에 오므로(`InputDelay`), 마지막 줄을 치고 바로 누르면
        // 그 줄이 빠진 채로 나간다 — 단추가 편집기와 **같은 판 안**이라 초점이
        // 빠지지도 않는다.
        await FlushBodyAsync();

        if (string.IsNullOrWhiteSpace(_mail.Body))
        {
            // 서버도 막지만 그쪽 문구는 「받는 사람 · 제목 · 본문이 모두
            // 필요합니다」라 **무엇이 빠졌는지 짚어 주지 못한다.**
            Say("메일 본문을 적으십시오.", NoticeTone.Warning);
            return;
        }

        _busy = true;
        try
        {
            // **받는 사람은 쉼표로 잇는다**(서버 규약). 중복은 가를 때 이미
            // 걸렀다(`SplitAddresses`) — 같은 주소를 두 번 적으면 같은 메일이
            // 두 통 간다.
            var to = string.Join(",", targets);

            List<EmailAttachmentDto> files;

            try
            {
                files = await ReadAttachmentsAsync();
            }
            catch (Exception ex)
            {
                // 파일을 못 읽었으면 **보내지 않는다.** 첨부가 빠진 채로 나가면
                // 받은 사람은 그 사실을 모르고, 보낸 사람도 성공만 본다.
                Say($"첨부를 읽지 못했습니다 — {ex.Message}", NoticeTone.Error);
                return;
            }

            var ok = await RunAsync(
                () => Api.SendEmailAsync(new EmailSendRequest
                {
                    To = to,
                    Subject = _mail.Subject,

                    // **본문은 늘 HTML 이다.** 서식 편집기가 만든 글을 평문으로
                    // 보내면 태그가 그대로 보인다(화면의 그 칸 주석 참고).
                    Body = _mail.Body,
                    Html = true,
                    Attachments = files,
                }),
                okMessage: string.Empty, "메일을 보내지 못했습니다");

            if (!ok) return;

            Say(files.Count > 0
                    ? $"{targets.Count}곳으로 보냈습니다. (첨부 {files.Count}개)"
                    : $"{targets.Count}곳으로 보냈습니다.");
        }
        finally
        {
            _busy = false;
        }
    }
}
