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

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        SchSummary.Or(_filter),
        _activeOnly ? "재직자만" : "그만둔 사람까지");

    /// <summary>보낼 수 있는 사람들. 한 번 읽어 두고 거르기는 브라우저에서 한다.</summary>
    private IReadOnlyList<AccountDto> _accounts = [];

    /// <summary>
    /// 고른 사람들의 <b>로그인 아이디</b>. 사람의 내부 번호가 아니다 —
    /// 푸시 구독이 그 값으로 붙어 있다(<see cref="PushOwnerDto"/>).
    /// </summary>
    private readonly HashSet<string> _picked = new(StringComparer.OrdinalIgnoreCase);

    private string _filter = string.Empty;
    private bool _activeOnly = true;
    private bool _busy;

    /// <summary>거르개에 걸린 사람들. 고르기와 「모두 고르기」가 이것을 본다.</summary>
    private IReadOnlyList<AccountDto> Matched =>
        [.. _accounts.Where(a =>
              (!_activeOnly || string.Equals(a.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
              && (string.IsNullOrWhiteSpace(_filter)
                  || $"{a.UserName} {a.LoginId} {a.DeptName} {a.CompanyName}"
                       .Contains(_filter, StringComparison.OrdinalIgnoreCase)))];

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

    /// <summary>문자 내용의 바이트 수. 한글은 두 바이트라 글자 수로는 못 센다.</summary>
    private int SmsBytes => System.Text.Encoding.UTF8.GetByteCount(_sms.Body ?? string.Empty);

    private readonly PushMessageDto _push = new();
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

    protected override Task OnInitializedAsync() => LoadAsync();

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
    /// 메일을 <b>SMTP 로 바로</b> 보낸다. 주소가 없는 사람은 뺀다 — 그 수는
    /// 위 안내가 미리 말해 두었다.
    /// </summary>
    private async Task SendEmailAsync()
    {
        if (_busy) return;

        var targets = MailTargets;

        if (targets.Count == 0)
        {
            Say("메일 주소가 있는 사람을 고르십시오.", NoticeTone.Warning);
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
            // **받는 사람은 쉼표로 잇는다**(서버 규약). 중복 주소는 한 번만 —
            // 같은 주소를 두 번 적으면 같은 메일이 두 통 간다.
            var to = string.Join(",", targets
                .Select(a => a.Email!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase));

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
                    ? $"{targets.Count}명에게 보냈습니다. (첨부 {files.Count}개)"
                    : $"{targets.Count}명에게 보냈습니다.");
        }
        finally
        {
            _busy = false;
        }
    }
}
