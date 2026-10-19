using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.Admin.Api;
using JSini.Web.Http;

namespace JSini.Web.Admin.Components.Pages;

public partial class MailSendPage
{
    [Inject] private MailClient Mails { get; set; } = default!;

    /// <summary>계정 목록을 읽으려고만 쓴다 — 「시스템 사용자 중에서 고르기」. 통로를 따로 둔 까닭은 <see cref="MailClient"/> 머리말에 있다.</summary>
    [Inject] private AdminClient Api { get; set; } = default!;

    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ── 화면이 둘이다 ──────────────────────────────────────
    // 새 메일과 보낸 메일함을 한 화면의 탭으로 둔다 — 보낸함이 고쳐 다시 보내는 되돌아오는 자리이고, 최근 주소도 거기서 나온다.

    private const int TabCompose = 0;
    private const int TabSentBox = 1;

    private int _tab = TabCompose;

    // ── 새 메일 ────────────────────────────────────────────

    private string _to = string.Empty;
    private string _cc = string.Empty;
    private string _subject = string.Empty;
    private string _body = string.Empty;

    private IReadOnlyList<PickedFile> _files = [];

    private bool _sending;

    /// <summary>서식 편집기를 다시 세우는 열쇠. DxHtmlEditor 는 글을 제 안에 들고 있어 코드가 본문을 갈아 끼워도 따라오지 않으므로, 값이 바뀌면 부품을 새로 만든다.
    /// 사람이 치는 동안에는 바꾸지 않는다 — 바꾸면 초점과 커서가 빠진다.</summary>
    private int _editorKey;

    private IJSObjectReference? _editorJs;

    // ── 간편 작성 틀 ───────────────────────────────────────
    // 편집기 앞에 두는 디딤돌이다. 틀은 본문을 대신하지 않는다 — 「본문에 넣기」 뒤로는 편집기가 정본이다.

    private string _tplHello = string.Empty;
    private string _tplBody = string.Empty;
    private string _tplClosing = string.Empty;

    /// <summary>틀 판을 펴 두었나. 처음에는 펴 둔다 — 안 보이면 쓰지 않는다.</summary>
    private bool _tplOpen = true;

    // ── AI 요약 ────────────────────────────────────────────

    private string? _summary;
    private bool _summarizing;

    /// <summary>요약을 본문 머리에 실제로 넣었나. 보낼 때 서버에 알린다.</summary>
    private bool _summaryInserted;

    // ── 받는 사람 고르기 ───────────────────────────────────

    /// <summary>고른 주소를 어느 칸에 넣을지. 창 하나를 둘이 나눠 쓴다.</summary>
    private enum PickTarget { To, Cc }

    private bool _pickerOpen;
    private PickTarget _pickerTarget = PickTarget.To;
    private string _pickerFilter = string.Empty;

    private IReadOnlyList<AccountDto> _accounts = [];
    private bool _accountsLoaded;

    private readonly HashSet<string> _pickedIds = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>재직 중인 계정의 상태 값 (계정 관리 화면과 같은 말).</summary>
    private const string StatusActive = "ACTIVE";

    /// <summary>고를 수 있는 사람 — 메일 주소가 있는 재직자. 주소가 없는 사람은 아예 안 보인다(이 화면은 메일 하나만 보낸다).</summary>
    private IReadOnlyList<AccountDto> PickerRows =>
        [.. _accounts
            .Where(a => !string.IsNullOrWhiteSpace(a.Email))
            .Where(a => string.Equals(a.Status, StatusActive, StringComparison.OrdinalIgnoreCase))
            .Where(a => string.IsNullOrWhiteSpace(_pickerFilter)
                        || $"{a.UserName} {a.LoginId} {a.Email} {a.DeptName} {a.CompanyName}"
                             .Contains(_pickerFilter, StringComparison.OrdinalIgnoreCase))];

    // ── 최근 주소 ──────────────────────────────────────────

    private IReadOnlyList<MailAddressUse> _recent = [];

    // ── 보낸 메일함 ────────────────────────────────────────

    /// <summary>조회 기간. 기본은 최근 한 달이다 — 보낸 메일은 「요전에 보낸 그것」을 찾는 일이 많다.</summary>
    private DateTime? _sentFrom = AppTime.TodayDate.AddMonths(-1);
    private DateTime? _sentTo = AppTime.TodayDate;

    private string _sentKeyword = string.Empty;

    private IReadOnlyList<MailListItem> _sent = [];
    private bool _sentLoaded;

    /// <summary>지금 열어 보고 있는 메일. <c>null</c> 이면 창이 닫혀 있다.</summary>
    private MailDetail? _opened;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string SentSummary => SchSummary.Of(
        SchSummary.Period(_sentFrom, _sentTo),
        SchSummary.Or(_sentKeyword));

    // ── 주소 다루기 ────────────────────────────────────────

    /// <summary>주소 칸을 하나씩으로 가른다. 쉼표·세미콜론·줄바꿈을 받으며 서버와 같은 규칙이다 — 보낼 때는 쉼표로 다시 잇는다.</summary>
    private static IEnumerable<string> SplitAddresses(string? text) =>
        (text ?? string.Empty)
            .Split([',', ';', '\n', '\r', '\t'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase);

    /// <summary>주소 꼴인가. <b>서버와 같은 판정</b>을 쓴다.</summary>
    private static bool IsAddress(string value) =>
        System.Net.Mail.MailAddress.TryCreate(value, out _);

    private IReadOnlyList<string> ToAddresses => [.. SplitAddresses(_to).Where(IsAddress)];

    private IReadOnlyList<string> CcAddresses => [.. SplitAddresses(_cc).Where(IsAddress)];

    /// <summary>주소 꼴이 아닌 것들. 조용히 빼지 않는다 — 서버가 나머지를 이미 보낸 뒤라 고쳐 다시 보내면 같은 메일을 두 번 받는다.</summary>
    private IReadOnlyList<string> BadAddresses =>
        [.. SplitAddresses(_to).Concat(SplitAddresses(_cc))
            .Where(a => !IsAddress(a))
            .Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>칸에 주소를 더한다. 이미 있으면 그대로 둔다.</summary>
    private static string AddAddress(string field, string address)
    {
        var have = SplitAddresses(field).ToList();

        if (have.Contains(address, StringComparer.OrdinalIgnoreCase))
        {
            return field;
        }

        have.Add(address);
        return string.Join(", ", have);
    }

    // ── 수명 주기 ──────────────────────────────────────────

    /// <summary>최근 주소를 미리 읽어 둔다. 프리렌더에서는 안 읽는다 — 첫 진입마다 화면이 두 번 만들어져 왕복도 두 번이 된다.</summary>
    protected override Task OnInitializedAsync() =>
        RendererInfo.IsInteractive ? LoadRecentAsync() : Task.CompletedTask;

    /// <summary>받는 사람으로 미리 넣을 주소(<c>?to=</c>). 아바타 카드의 「이메일 쓰기」가 준다.</summary>
    [SupplyParameterFromQuery(Name = "to")] public string? ToQuery { get; set; }

    /// <summary>이미 넣은 <see cref="ToQuery"/>. 탭이 살아 있는 채로 다시 와도 쓰던 받는 사람 칸을 건드리지 않게 둔다.</summary>
    private string? _toApplied;

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        var to = ToQuery?.Trim();
        if (string.IsNullOrEmpty(to) || string.Equals(to, _toApplied, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _toApplied = to;
        _tab = TabCompose;
        _to = AddAddress(_to, to);
    }

    /// <summary>최근 주소를 읽는다. 조용히 실패한다 — 없어도 주소를 쳐서 보낼 수 있으니 화면을 막지 않는다.</summary>
    private async Task LoadRecentAsync()
    {
        try
        {
            _recent = await Mails.GetRecentAddressesAsync();
        }
        catch (ApiException)
        {
            _recent = [];
        }
    }

    // ── 본문 읽어 오기 ─────────────────────────────────────

    /// <summary>서식 편집기에서 지금 화면의 글을 읽어 _body 에 담는다. 편집기는 값 변화를 늦게 알리므로 직접 묻는다. 읽지 못하면 있던 값을 그대로 둔다.</summary>
    private async Task FlushBodyAsync()
    {
        try
        {
            _editorJs ??= await JS.InvokeAsync<IJSObjectReference>(
                "import", "./_content/JSini.Web.Admin/js/mail-editor.js");

            var markup = await _editorJs.InvokeAsync<string?>("readMarkup", ".ad-mailsend-editor");

            if (markup is not null)
            {
                _body = markup;
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

    /// <summary>본문을 통째로 갈아 끼운다. 편집기를 다시 세운다(_editorKey).</summary>
    private void ReplaceBody(string html)
    {
        _body = html;
        _editorKey++;
    }

    // ── 간편 작성 틀 ───────────────────────────────────────

    /// <summary>틀에 적은 것을 서식 본문으로 만들어 편집기에 넣는다. 본문을 덮어쓰고, 사람이 친 글은 HTML 로 이스케이프한다.</summary>
    private void ApplyTemplate()
    {
        if (string.IsNullOrWhiteSpace(_tplBody) && string.IsNullOrWhiteSpace(_tplHello))
        {
            Say("틀에 적은 내용이 없습니다.", NoticeTone.Warning);
            return;
        }

        // 제목이 비면 틀의 첫 줄을 제목으로 쓴다.
        if (string.IsNullOrWhiteSpace(_subject))
        {
            _subject = FirstLine(_tplBody) ?? FirstLine(_tplHello) ?? string.Empty;
        }

        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(_tplHello)) parts.Add(Paragraphs(_tplHello));
        if (!string.IsNullOrWhiteSpace(_tplBody)) parts.Add(Paragraphs(_tplBody));
        if (!string.IsNullOrWhiteSpace(_tplClosing)) parts.Add(Paragraphs(_tplClosing));

        ReplaceBody(string.Join("\n", parts));

        Say("본문에 넣었습니다. 이제 편집기에서 다듬으십시오.");
    }

    /// <summary>빈 줄로 나눠 문단으로 만든다. 한 줄 바꿈은 줄바꿈 그대로 둔다.</summary>
    private static string Paragraphs(string text) =>
        string.Join("\n", text
            .Replace("\r\n", "\n")
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(block => $"<p>{Escape(block).Replace("\n", "<br>")}</p>"));

    private static string? FirstLine(string? text) =>
        text?.Replace("\r\n", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

    /// <summary>HTML 에 그대로 실릴 글자를 안전하게 바꾼다.</summary>
    private static string Escape(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    // ── AI 요약 ────────────────────────────────────────────

    /// <summary>본문을 AI 로 추린다. 넣지는 않는다 — 엉뚱한 요약을 보고 버릴 자리가 있어야 하고, 넣으면 편집기가 다시 세워져 치던 자리를 잃는다.</summary>
    private async Task SummarizeAsync()
    {
        if (_summarizing) return;

        await FlushBodyAsync();

        var text = PlainText(_body);

        if (string.IsNullOrWhiteSpace(text))
        {
            Say("요약할 본문이 없습니다. 내용을 먼저 적으십시오.", NoticeTone.Warning);
            return;
        }

        // 너무 짧은 글은 부르지 않는다 — 한도는 똑같이 든다.
        if (text.Length < 80)
        {
            Say("본문이 짧아 요약할 것이 없습니다.", NoticeTone.Warning);
            return;
        }

        _summarizing = true;
        _summary = null;

        try
        {
            _summary = (await Mails.SummarizeAsync(text))?.Trim();

            if (string.IsNullOrWhiteSpace(_summary))
            {
                _summary = null;
                Say("요약을 받지 못했습니다.", NoticeTone.Warning);
            }
        }
        catch (ApiException ex)
        {
            // 서버가 준 말을 그대로 보여 준다 — 뭉뚱그리면 설정을 고칠 사람이 못 고친다.
            Say($"요약하지 못했습니다 — {ex.Message}", NoticeTone.Error);
        }
        finally
        {
            _summarizing = false;
        }
    }

    /// <summary>요약을 본문 맨 위에 넣는다 — 요약은 먼저 읽히려고 있다.</summary>
    private async Task InsertSummaryAsync()
    {
        if (string.IsNullOrWhiteSpace(_summary)) return;

        // 지금 편집기에 있는 글 위에 얹는다 — 파라미터 값에 얹으면 마지막에 친 줄이 빠진다.
        await FlushBodyAsync();

        var lines = _summary
            .Replace("\r\n", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var box =
            "<div style=\"border-left:3px solid #4263eb;background:#f1f3f5;"
            + "padding:10px 14px;margin:0 0 14px 0;\">"
            + "<div style=\"font-weight:600;margin-bottom:4px;\">요약</div>"
            + string.Join("", lines.Select(l => $"<div>{Escape(l)}</div>"))
            + "</div>";

        ReplaceBody(box + _body);

        _summaryInserted = true;

        Say("본문 맨 위에 요약을 넣었습니다.");
    }

    /// <summary>HTML 에서 글자만 뽑는다. 블록 태그는 줄바꿈으로 바꿔 문단 경계를 살린다.</summary>
    private static string PlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;

        var text = System.Text.RegularExpressions.Regex.Replace(
            html, @"<(br|/p|/div|/li|/h[1-6]|/tr)[^>]*>", "\n",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        text = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", " ");

        text = System.Net.WebUtility.HtmlDecode(text);

        // 줄바꿈은 살리고 같은 줄의 빈칸만 줄인다.
        text = System.Text.RegularExpressions.Regex.Replace(text, @"[ \t\f\v]+", " ");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\n\s*\n\s*", "\n\n");

        return text.Trim();
    }

    // ── 받는 사람 고르기 ───────────────────────────────────

    private async Task OpenPickerAsync(PickTarget target)
    {
        _pickerTarget = target;
        _pickerFilter = string.Empty;
        _pickedIds.Clear();
        _pickerOpen = true;

        if (_accountsLoaded) return;

        // 계정은 이 화면을 쓰는 동안 안 바뀌므로 한 번만 읽는다.
        await LoadAsync(async () =>
        {
            _accounts = await Api.GetAccountsAsync();
            _accountsLoaded = true;
            return _accounts.Count;
        }, "계정이 없습니다.", "계정을 읽지 못했습니다");
    }

    /// <summary>고른 사람들의 주소를 칸에 더하고 창을 닫는다.</summary>
    private void ApplyPicked()
    {
        var picked = _accounts
            .Where(a => _pickedIds.Contains(a.LoginId))
            .Select(a => a.Email!.Trim())
            .Where(e => e.Length > 0);

        foreach (var address in picked)
        {
            if (_pickerTarget == PickTarget.To) _to = AddAddress(_to, address);
            else _cc = AddAddress(_cc, address);
        }

        _pickerOpen = false;
    }

    private void TogglePicked(string loginId, bool on)
    {
        if (on) _pickedIds.Add(loginId);
        else _pickedIds.Remove(loginId);
    }

    /// <summary>최근 주소 하나를 받는 사람 칸에 더한다.</summary>
    private void UseRecent(string address) => _to = AddAddress(_to, address);

    /// <summary>최근 주소 하나를 참조 칸에 더한다.</summary>
    private void UseRecentAsCc(string address) => _cc = AddAddress(_cc, address);

    // ── 보내기 ─────────────────────────────────────────────

    private async Task SendAsync()
    {
        if (_sending) return;

        var bad = BadAddresses;

        if (bad.Count > 0)
        {
            Say($"주소 꼴이 아닙니다 — {string.Join(" · ", bad.Take(5))}{(bad.Count > 5 ? " 외" : "")}",
                NoticeTone.Warning);
            return;
        }

        if (ToAddresses.Count == 0)
        {
            Say("받는 사람 주소를 적으십시오.", NoticeTone.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(_subject))
        {
            Say("메일 제목을 적으십시오.", NoticeTone.Warning);
            return;
        }

        // 본문은 여기서 편집기에게 직접 묻는다 — 까닭은 FlushBodyAsync 머리말.
        await FlushBodyAsync();

        if (string.IsNullOrWhiteSpace(PlainText(_body)))
        {
            Say("메일 본문을 적으십시오.", NoticeTone.Warning);
            return;
        }

        _sending = true;

        try
        {
            List<EmailAttachmentDto> files;

            try
            {
                files = await ReadAttachmentsAsync();
            }
            catch (Exception ex)
            {
                // 파일을 못 읽었으면 보내지 않는다 — 첨부가 빠진 채 나가면 보낸 사람도 성공만 본다.
                Say($"첨부를 읽지 못했습니다 — {ex.Message}", NoticeTone.Error);
                return;
            }

            MailSendResult? result = null;

            var ok = await RunAsync(
                async () => result = await Mails.SendAsync(new MailComposeRequest
                {
                    To = string.Join(",", ToAddresses),
                    Cc = CcAddresses.Count > 0 ? string.Join(",", CcAddresses) : null,
                    Subject = _subject,
                    Body = _body,
                    Attachments = files,
                    HasAiSummary = _summaryInserted,
                }),
                okMessage: string.Empty,
                failMessage: "메일을 보내지 못했습니다");

            if (!ok || result is null)
            {
                // 실패해도 보낸함에는 들어가 있다 — 그 사실을 말해야 쓴 사람이 글을 다시 치지 않는다.
                if (!ok)
                {
                    await ReloadSentAsync();
                }

                return;
            }

            Say(Summary(result));

            ClearCompose();

            // 보낸 뒤 최근 주소가 달라지므로 다시 읽는다.
            await LoadRecentAsync();
            await ReloadSentAsync();
        }
        finally
        {
            _sending = false;
        }
    }

    /// <summary>보낸 결과를 사람 말로. <b>몇 곳으로 갔는지가 먼저다.</b></summary>
    private static string Summary(MailSendResult r)
    {
        var parts = new List<string> { $"{r.ToCount}곳으로 보냈습니다" };

        if (r.CcCount > 0) parts.Add($"참조 {r.CcCount}곳");
        if (r.AttachmentCount > 0) parts.Add($"첨부 {r.AttachmentCount}개");

        return string.Join(" · ", parts) + ".";
    }

    /// <summary>첨부를 바이트로 읽어 base64 로 싣는다. 파일 서버에 올리지 않는다 — 보낸 뒤에 쓸 일이 없어 쌓이기만 한다.</summary>
    private async Task<List<EmailAttachmentDto>> ReadAttachmentsAsync()
    {
        var files = new List<EmailAttachmentDto>(_files.Count);

        foreach (var pick in _files)
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

    /// <summary>보내고 나면 쓰는 칸을 비운다. 첨부 고르개는 제 목록을 들고 있어 부품을 새로 세워 함께 비운다.</summary>
    private void ClearCompose()
    {
        _to = string.Empty;
        _cc = string.Empty;
        _subject = string.Empty;
        _summary = null;
        _summaryInserted = false;
        _files = [];
        _filePickerKey++;

        ReplaceBody(string.Empty);
    }

    /// <summary>첨부 고르개를 다시 세우는 열쇠 (<see cref="ClearCompose"/> 머리말).</summary>
    private int _filePickerKey;

    // ── 보낸 메일함 ────────────────────────────────────────

    /// <summary>보낸함은 탭을 처음 열 때 한 번만 읽는다. 보낸 뒤에는 SendAsync 가 다시 읽는다.</summary>
    private async Task TabChangedAsync(int index)
    {
        _tab = index;

        if (index == TabSentBox && !_sentLoaded)
        {
            await ReloadSentAsync();
        }
    }

    private Task ReloadSentAsync() => LoadAsync(async () =>
    {
        _sent = await Mails.GetSentAsync(_sentFrom, _sentTo, _sentKeyword);
        _sentLoaded = true;
        return _sent.Count;
    }, "보낸 메일이 없습니다.", "보낸 메일함을 읽지 못했습니다");

    private Task ResetSentAsync()
    {
        _sentFrom = AppTime.TodayDate.AddMonths(-1);
        _sentTo = AppTime.TodayDate;
        _sentKeyword = string.Empty;

        return ReloadSentAsync();
    }

    /// <summary>보낸 메일 한 통을 펼친다. 본문 전문은 이때 받는다.</summary>
    private Task OpenAsync(MailListItem row) => LoadOneAsync(
        () => Mails.GetAsync(row.Id),
        mail => _opened = mail,
        "메일을 찾지 못했습니다.",
        "메일을 열지 못했습니다");

    /// <summary>보낸함에서 치운다. 발송 기록은 남는다(서버가 표시만 한다).</summary>
    private async Task DeleteAsync(MailListItem row)
    {
        if (await RunAsync(() => Mails.DeleteAsync(row.Id),
                "보낸 메일함에서 치웠습니다.", "치우지 못했습니다"))
        {
            if (_opened?.Id == row.Id) _opened = null;

            await ReloadSentAsync();
        }
    }

    /// <summary>보낸 메일을 새 메일로 옮겨 담는다 — 고쳐서 다시 보내는 자리다. 첨부는 따라오지 않는다(이름만 있고 파일이 없다).</summary>
    private void Resend(MailDetail mail)
    {
        _to = mail.To;
        _cc = mail.Cc ?? string.Empty;
        _subject = mail.Subject ?? string.Empty;
        _summary = null;
        _summaryInserted = false;

        ReplaceBody(mail.Body ?? string.Empty);

        _opened = null;
        _tab = TabCompose;

        Say(mail.AttachmentCount > 0
            ? $"새 메일로 옮겼습니다. 첨부 {mail.AttachmentCount}개는 다시 골라야 합니다."
            : "새 메일로 옮겼습니다.");
    }

    /// <summary>주소 칸에 몇 곳이 들었나. 표의 「참조」 칸이 그 수를 적는다.</summary>
    private static int SplitCount(string? text) => SplitAddresses(text).Count();

    /// <summary>표에 적을 받는 사람. 길면 줄여 적고 전체는 풍선말로 준다.</summary>
    private static string ShortAddresses(string? text, int max = 2)
    {
        var all = SplitAddresses(text).ToList();

        if (all.Count == 0) return "-";
        if (all.Count <= max) return string.Join(", ", all);

        return $"{string.Join(", ", all.Take(max))} 외 {all.Count - max}";
    }

    /// <summary>파일 크기를 사람 말로.</summary>
    private static string Size(long bytes) => bytes switch
    {
        < 1024 => $"{bytes}B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#}KB",
        _ => $"{bytes / 1024.0 / 1024.0:0.#}MB",
    };
}
