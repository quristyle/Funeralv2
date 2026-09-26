using Microsoft.AspNetCore.Components;
using JSini.PublicSite.Api;
using JSini.PublicSite.Site;

namespace JSini.PublicSite.Components.Pages;

public partial class Contact
{
    [Inject] private SiteApi Api { get; set; } = default!;

    [Parameter] public string? Locale { get; set; }

    private Messages T => SiteMessages.For(Locale);
    private string L => SiteMessages.Normalize(Locale);

    /// <summary>
    /// 폼이 실어 보내는 값. POST 로 돌아올 때 Blazor 가 여기에 채워 준다.
    /// </summary>
    /// 초기값을 주지 않는다(BL0008) — POST 로 돌아올 때 Blazor 가 통째로 갈아
    /// 끼우므로, 초기값이 있으면 그것이 null 로 덮이는 경우가 생긴다.
    /// 대신 첫 GET 에서 OnInitialized 가 채운다.
    [SupplyParameterFromForm]
    private ContactInput? Input { get; set; }

    private Section? _consent;
    private bool _done;
    private string? _error;

    /// <summary>
    /// 동의 문구. DB(<c>site.sections</c> 의 <c>contact.consent</c>)가 정본이다 —
    /// 법률 문구라 코드 배포 없이 고칠 수 있어야 한다.
    /// 블록이 없으면 코드에 둔 문장을 쓴다. <b>동의 문구가 아예 안 보이는 것이 더 나쁘다.</b>
    /// </summary>
    private string ConsentBody =>
        string.IsNullOrWhiteSpace(_consent?.Body) ? T.Contact.Form.ConsentFallback : _consent.Body!;

    protected override async Task OnInitializedAsync()
    {
        Input ??= new ContactInput();

        var rows = await Api.SectionsAsync(L, "contact.");
        _consent = rows.FirstOrDefault(r => r.SectionKey == "contact.consent");

        // POST 로 다시 들어온 요청에서는 세지 않는다. 한 번 본 화면이다.
        if (!HttpMethods.IsPost(HttpContext?.Request.Method ?? "GET"))
        {
            Api.RecordVisit($"/{L}/contact", L);
        }
    }

    [CascadingParameter] private HttpContext? HttpContext { get; set; }

    private async Task SubmitAsync()
    {
        _error = null;

        var name = Input!.Name?.Trim() ?? string.Empty;
        var email = Input!.Email?.Trim() ?? string.Empty;
        var message = Input!.Message?.Trim() ?? string.Empty;

        // 브라우저의 `required` 만 믿지 않는다. 개발자 도구로 지우면 그만이고,
        // 스크립트가 폼을 직접 POST 할 수도 있다.
        if (name.Length == 0 || email.Length == 0 || message.Length == 0 || !Input!.Consent)
        {
            _error = T.Contact.Form.Required;
            return;
        }

        var result = await Api.SubmitInquiryAsync(new InquiryRequest
        {
            Name = name,
            Company = Blank(Input!.Company),
            Email = email,
            Phone = Blank(Input!.Phone),
            // 제목을 비워 두면 본문 앞머리를 쓴다. 담당자 목록에서 빈 줄이 늘어서는 것을 막는다.
            Subject = string.IsNullOrWhiteSpace(Input!.Subject)
                ? message[..Math.Min(40, message.Length)]
                : Input!.Subject.Trim(),
            Message = message,
            Locale = L,
            Consent = Input!.Consent,
            Website = Input!.Website,
        });

        if (result.Ok)
        {
            _done = true;
            return;
        }

        _error = result.RateLimited ? T.Contact.Form.RateLimited : T.Contact.Form.Failed;
    }

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>폼이 실어 보내는 칸들.</summary>
    public sealed class ContactInput
    {
        public string? Name { get; set; }
        public string? Company { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Subject { get; set; }
        public string? Message { get; set; }
        public bool Consent { get; set; }

        /// <summary>
        /// 허니팟. 사람에게는 보이지 않는 칸이라 <b>비어 있어야 정상</b>이다.
        /// 채워져 있으면 서버가 조용히 버리고 성공 응답을 준다 — 봇에게 단서를 주지 않는다.
        /// </summary>
        public string? Website { get; set; }
    }
}
