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

    /// <summary>
    /// 계정 목록을 읽으려고만 쓴다 — 「시스템 사용자 중에서 고르기」.
    /// 통로를 따로 두지 않은 까닭은 <see cref="MailClient"/> 머리말에 있다.
    /// </summary>
    [Inject] private AdminClient Api { get; set; } = default!;

    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ── 화면이 둘이다 ──────────────────────────────────────
    //
    // 「새 메일」과 「보낸 메일함」을 **한 화면의 탭**으로 둔다. 쪽지가 쓰기와
    // 쪽지함을 메뉴 둘로 가른 것과 다른데, 까닭이 둘이다.
    //
    // 하나는 **보낸함이 이 화면의 되돌아오는 자리**라는 것이다. 메일은 쪽지와
    // 달리 「아까 그 사람에게 보냈던 글」을 열어 고쳐 다시 보내는 일이 잦다 —
    // 메뉴를 갈라 두면 그 왕복이 사이드바를 거친다.
    //
    // 또 하나는 **최근 주소가 보낸함에서 나온다**는 것이다. 두 화면이 같은
    // 자료를 보고 있으므로 한 번 읽어 나눠 쓰는 편이 맞다.

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

    /// <summary>
    /// 서식 편집기를 <b>다시 세우는 열쇠</b>. 값이 바뀌면 부품이 새로 만들어진다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>DxHtmlEditor</c> 는 사람이 치는 글을 자기 안에 들고 있어서,
    /// <see cref="_body"/> 를 <b>코드가 갈아 끼워도</b> 화면이 따라오지 않는
    /// 경우가 있다. 틀 넣기 · AI 요약 넣기 · 다시 보내기가 전부 그 경우다 —
    /// 증상은 「넣었다는데 편집기에 안 보인다」 하나라, 넣는 코드가 잘못된
    /// 것처럼 읽힌다.
    /// </para>
    /// <para>
    /// 열쇠를 바꿔 부품을 새로 세우면 새 값으로 다시 그려진다. <b>사람이 치는
    /// 동안에는 절대 안 바꾼다</b> — 바꾸면 초점이 빠지고 커서가 맨 앞으로
    /// 돌아간다. 바꾸는 자리는 「사람이 단추를 눌러 본문을 통째로 고친」
    /// 셋뿐이다.
    /// </para>
    /// </remarks>
    private int _editorKey;

    private IJSObjectReference? _editorJs;

    // ── 간편 작성 틀 ───────────────────────────────────────
    //
    // **서식 편집기 앞에 두는 디딤돌이다.** 편집기를 열면 빈 칸 하나라, 메일을
    // 자주 안 쓰는 사람은 거기서 멈춘다 — 인사말을 어떻게 적고 어디서 끊어야
    // 할지가 빈 칸에는 안 적혀 있다. 칸 셋으로 나눠 물으면 채우기만 하면 된다.
    //
    // **틀이 본문을 대신하지 않는다.** 「본문에 넣기」는 글을 서식 본문으로
    // 바꿔 편집기에 넣고 그것으로 끝이다 — 그 뒤로는 편집기가 정본이다.
    // 양쪽을 계속 맞추려 들면 「틀을 고쳤는데 본문이 안 바뀐다」가 된다.

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

    /// <summary>
    /// 고를 수 있는 사람 — <b>메일 주소가 있는 재직자</b>.
    /// </summary>
    /// <remarks>
    /// 주소가 없는 사람은 아예 안 보인다. 「메시지 발송」은 고르게 하고 몇 명이
    /// 빠졌는지 적어 주는데(그쪽은 푸시·문자도 함께 보내는 화면이라 주소가
    /// 없어도 고를 값어치가 있다), <b>이 화면이 하는 일은 메일 하나</b>라
    /// 고를 수 있는데 보낼 수 없는 줄을 두면 고르는 동안에는 알 수가 없다.
    /// </remarks>
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

    /// <summary>
    /// 조회 기간. <b>기본이 최근 한 달이다</b> — 보낸 메일을 찾는 일은 대개
    /// 「요전에 보낸 그것」이라 이레로는 짧다(쪽지함과 같은 값).
    /// </summary>
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

    /// <summary>
    /// 주소 칸을 하나씩으로 가른다. 쉼표·세미콜론에 <b>줄바꿈까지</b> 받는다 —
    /// 어디선가 복사해 붙이면 줄로 오는 일이 흔하다.
    /// </summary>
    /// <remarks>
    /// <b>서버와 같은 규칙이다</b>(<c>MailEndpoints.SplitAddresses</c>).
    /// 서버는 쉼표로 이어 받으므로 <b>보낼 때 쉼표로 다시 잇는다.</b>
    /// </remarks>
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

    /// <summary>
    /// 주소 꼴이 아닌 것들. <b>조용히 빼지 않는다</b> — 서버도 걸러 주지만
    /// 그때는 이미 나머지에게 나간 뒤라, 오타 하나를 고쳐 다시 보내면
    /// 받은 사람은 같은 메일을 두 번 받는다.
    /// </summary>
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

    /// <summary>
    /// 최근 주소를 미리 읽어 둔다.
    /// </summary>
    /// <remarks>
    /// <b>프리렌더에서는 안 읽는다.</b> 포털은 프리렌더를 켜 두어서 첫
    /// 진입·F5 마다 화면이 두 번 만들어지는데, 그대로 두면 게이트웨이 왕복도
    /// 두 번이다(<c>DataPage</c> 의 <c>CanLoad</c> 머리말). 저쪽은
    /// <see cref="DataPage.LoadAsync"/> 가 막아 주지만 이 조회는 <b>실패해도
    /// 화면이 아무 말도 하지 않아야 해서</b> 그 길을 안 타므로 여기서 막는다.
    /// </remarks>
    protected override Task OnInitializedAsync() =>
        RendererInfo.IsInteractive ? LoadRecentAsync() : Task.CompletedTask;

    /// <summary>
    /// 최근 주소를 읽는다. <b>조용히 실패한다</b> — 이것이 없어도 메일은
    /// 주소를 쳐서 보낼 수 있고, 여기서 화면을 막으면 못 보낼 이유가 없는
    /// 사람이 못 보낸다.
    /// </summary>
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

    /// <summary>
    /// 서식 편집기에서 <b>지금 화면에 있는 글</b>을 읽어 <see cref="_body"/> 에 담는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>DxHtmlEditor</c> 에는 「지금 값을 내놔라」가 없다 — <c>MarkupChanged</c>
    /// 로 알려 줄 뿐이고 그 알림이 <c>InputDelay</c>(기본 500ms) 만큼 늦는다.
    /// 마지막 줄을 치고 바로 「보내기」를 누르면 그 줄이 빠진 채로 나간다.
    /// </para>
    /// <para>
    /// <b>읽지 못하면 있던 값을 그대로 둔다.</b> 여기서 비우면 잘 들어와 있던
    /// 본문까지 날아가고, 증상이 「가끔 빈 메일이 간다」로만 보인다.
    /// </para>
    /// </remarks>
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

    /// <summary>
    /// 본문을 통째로 갈아 끼운다. <b>편집기를 다시 세운다</b>(<see cref="_editorKey"/>).
    /// </summary>
    private void ReplaceBody(string html)
    {
        _body = html;
        _editorKey++;
    }

    // ── 간편 작성 틀 ───────────────────────────────────────

    /// <summary>
    /// 틀에 적은 것을 서식 본문으로 만들어 편집기에 넣는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>본문에 이미 글이 있으면 묻지 않고 덮는다.</b> 단추 이름이 「본문에
    /// 넣기」이고 틀은 글을 시작하는 자리라, 덧붙이면 같은 인사말이 두 번
    /// 들어간다. 실수로 눌러도 틀의 글은 칸에 그대로 남아 있어 다시 만들 수 있다.
    /// </para>
    /// <para>
    /// <b>사람이 친 글자를 HTML 로 그대로 넘기지 않는다</b>(<see cref="Escape"/>).
    /// 메일 본문에 <c>&lt;</c> 를 적는 일은 드물지만, 어딘가에서 복사해 붙인
    /// 글에 태그가 섞여 들면 메일이 통째로 무너진다.
    /// </para>
    /// </remarks>
    private void ApplyTemplate()
    {
        if (string.IsNullOrWhiteSpace(_tplBody) && string.IsNullOrWhiteSpace(_tplHello))
        {
            Say("틀에 적은 내용이 없습니다.", NoticeTone.Warning);
            return;
        }

        // 제목이 비어 있으면 틀의 첫 줄을 제목으로 쓴다. 제목 없는 메일은
        // 받는 쪽 메일함에서 「(제목 없음)」이 되고, 그것을 여는 사람은 드물다.
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

    /// <summary>
    /// 본문을 AI 로 추린다. <b>넣지는 않는다</b> — 사람이 보고 고른다.
    /// </summary>
    /// <remarks>
    /// 받아 오자마자 본문에 넣지 않는 까닭이 둘이다. AI 가 엉뚱한 요약을 내는
    /// 일이 있어 <b>보고 버릴 자리</b>가 있어야 하고, 넣자마자 본문이 바뀌면
    /// 편집기가 다시 세워져 <b>치던 자리를 잃는다</b>(<see cref="_editorKey"/>).
    /// </remarks>
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

        // **너무 짧으면 부르지 않는다.** 두 줄짜리 메일을 세 줄로 요약하는
        // 것은 아무 쓸모가 없는데 한도는 똑같이 든다.
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
            // 대개 「키가 없다」·「하루 한도를 다 썼다」다. 서버가 준 말을
            // 그대로 보여 준다 — 뭉뚱그리면 설정을 고칠 수 있는 사람이 못 고친다.
            Say($"요약하지 못했습니다 — {ex.Message}", NoticeTone.Error);
        }
        finally
        {
            _summarizing = false;
        }
    }

    /// <summary>
    /// 요약을 <b>본문 맨 위</b>에 넣는다.
    /// </summary>
    /// <remarks>
    /// 넣는 자리가 위인 것은 요약이 <b>먼저 읽히려고</b> 있기 때문이다.
    /// 아래에 붙이면 긴 본문을 다 읽은 사람만 보게 되어 뜻이 없다.
    /// </remarks>
    private async Task InsertSummaryAsync()
    {
        if (string.IsNullOrWhiteSpace(_summary)) return;

        // **지금 편집기에 있는 글 위에 얹는다.** 파라미터로 올라온 값에 얹으면
        // 마지막에 친 줄이 빠진 채로 덮인다.
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

    /// <summary>
    /// HTML 에서 글자만 뽑는다. AI 에게 태그를 올려 봐야 토큰만 쓴다.
    /// </summary>
    /// <remarks>
    /// <b>블록 태그는 줄바꿈으로 바꾼다.</b> 그냥 지우면 문단이 한 덩어리로
    /// 붙어 요약이 문장 경계를 못 찾는다.
    /// </remarks>
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

        // **한 번만 읽는다.** 계정은 이 화면을 쓰는 동안 바뀌지 않고, 창을
        // 열 때마다 읽으면 받는 사람을 두 번 고치는 흔한 몸짓이 왕복 둘이 된다.
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
                // 파일을 못 읽었으면 **보내지 않는다.** 첨부가 빠진 채로 나가면
                // 받은 사람은 그 사실을 모르고, 보낸 사람도 성공만 본다.
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
                // **실패해도 보낸함에는 들어가 있다.** 서버가 사람이 쓴 글을
                // 잃지 않으려고 그렇게 한다 — 그 사실을 말해 주지 않으면
                // 쓴 사람은 글이 날아간 줄 알고 처음부터 다시 친다.
                if (!ok)
                {
                    await ReloadSentAsync();
                }

                return;
            }

            Say(Summary(result));

            ClearCompose();

            // 보낸 뒤에 최근 주소가 달라진다. 다시 읽어 두지 않으면 방금 보낸
            // 주소가 「최근」에 없어서, 연달아 보낼 때 또 쳐야 한다.
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

    /// <summary>
    /// 첨부를 바이트로 읽어 base64 로 싣는다.
    /// </summary>
    /// <remarks>
    /// <b>파일 서버에 올리지 않는다.</b> 메일 첨부는 보내고 나면 쓸 일이
    /// 없는데 올려 두면 아무도 지우지 않는 파일이 보낼 때마다 쌓인다
    /// (<c>EmailAttachmentDto</c> 머리말).
    /// </remarks>
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

    /// <summary>
    /// 보내고 나면 쓰는 칸을 비운다.
    /// </summary>
    /// <remarks>
    /// <b>첨부는 화면이 비우지 않는다.</b> <c>FilePicker</c> 가 자기 안에
    /// 고른 목록을 들고 있어서, 여기서 <see cref="_files"/> 만 비우면
    /// 화면에는 파일 이름이 그대로 남은 채 다음 메일에 안 붙는다 — 그 어긋남이
    /// 「첨부가 가끔 빠진다」로 보인다. 부품을 새로 세워 함께 비운다.
    /// </remarks>
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

    /// <summary>
    /// 탭을 옮길 때 보낸함을 <b>처음 한 번만</b> 읽는다.
    /// </summary>
    /// <remarks>
    /// 옮길 때마다 읽으면 메일을 쓰다 보낸함을 흘끗 보고 돌아오는 흔한 몸짓이
    /// 왕복 둘이 된다. 보낸 뒤에는 <see cref="SendAsync"/> 가 직접 다시 읽는다.
    /// </remarks>
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

    /// <summary>
    /// 보낸함에서 치운다. <b>발송 기록은 남는다</b>(서버가 표시만 한다).
    /// </summary>
    private async Task DeleteAsync(MailListItem row)
    {
        if (await RunAsync(() => Mails.DeleteAsync(row.Id),
                "보낸 메일함에서 치웠습니다.", "치우지 못했습니다"))
        {
            if (_opened?.Id == row.Id) _opened = null;

            await ReloadSentAsync();
        }
    }

    /// <summary>
    /// 보낸 메일을 <b>새 메일로 옮겨 담는다</b> — 고쳐서 다시 보내는 자리다.
    /// </summary>
    /// <remarks>
    /// <b>첨부는 따라오지 않는다.</b> 보관함에 이름만 있고 파일이 없기 때문이고
    /// (<c>PortalMail</c> 머리말), 그 사실을 토스트가 말한다 — 말해 주지 않으면
    /// 붙어 있는 줄 알고 그대로 보낸다.
    /// </remarks>
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
