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

    /// <summary>폼이 실어 보내는 값. POST 로 돌아올 때 Blazor 가 채운다. 초기값을 주지 않는다(BL0008). 첫 GET 에서 OnInitialized 가 채운다.</summary>
    [SupplyParameterFromForm]
    private ContactInput? Input { get; set; }

    private Section? _consent;
    private bool _done;
    private string? _error;
    private string? _captchaImage;

    /// <summary>「새 그림」 단추의 name. 이것으로 온 POST 는 접수하지 않고 그림만 바꾼다.</summary>
    private const string RefreshField = "captcha-refresh";

    /// <summary>동의 문구. DB(site.sections 의 contact.consent)가 정본이다. 블록이 없으면 코드의 문장을 쓴다. 동의 문구가 아예 안 보이는 것이 더 나쁘다.</summary>
    private string ConsentBody =>
        string.IsNullOrWhiteSpace(_consent?.Body) ? T.Contact.Form.ConsentFallback : _consent.Body!;

    protected override async Task OnInitializedAsync()
    {
        Input ??= new ContactInput();

        var rows = await Api.SectionsAsync(L, "contact.");
        _consent = rows.FirstOrDefault(r => r.SectionKey == "contact.consent");

        // POST 로 다시 들어온 요청에서는 세지 않는다. 한 번 본 화면이다. 캡차는 POST 처리가 끝난 뒤 새로 받는다.
        if (!HttpMethods.IsPost(HttpContext?.Request.Method ?? "GET"))
        {
            Api.RecordVisit($"/{L}/contact", L);
            await NewCaptchaAsync();
        }
    }

    /// <summary>새 문제를 받고 친 답을 비운다. 한 토큰은 서버에서 한 번만 검사되므로 보낸 뒤에는 늘 새로 받는다.</summary>
    private async Task NewCaptchaAsync()
    {
        var challenge = await Api.CaptchaAsync();
        Input!.CaptchaToken = challenge?.Token;
        Input.CaptchaAnswer = null;
        _captchaImage = challenge?.Image;
    }

    [CascadingParameter] private HttpContext? HttpContext { get; set; }

    private async Task SubmitAsync()
    {
        _error = null;

        if (HttpContext?.Request.HasFormContentType == true && HttpContext.Request.Form.ContainsKey(RefreshField))
        {
            await NewCaptchaAsync();
            return;
        }

        await SubmitCoreAsync();

        if (!_done)
        {
            await NewCaptchaAsync();
        }
    }

    private async Task SubmitCoreAsync()
    {

        var name = Input!.Name?.Trim() ?? string.Empty;
        var email = Input!.Email?.Trim() ?? string.Empty;
        var message = Input!.Message?.Trim() ?? string.Empty;

        // 브라우저의 required 만 믿지 않는다. 개발자 도구로 지우거나 스크립트가 직접 POST 할 수 있다.
        if (name.Length == 0 || email.Length == 0 || message.Length == 0 || !Input!.Consent)
        {
            _error = T.Contact.Form.Required;
            return;
        }

        if (string.IsNullOrWhiteSpace(Input!.CaptchaAnswer))
        {
            _error = T.Contact.Form.CaptchaHint;
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
            CaptchaToken = Input!.CaptchaToken,
            CaptchaAnswer = Input!.CaptchaAnswer.Trim(),
        });

        if (result.Ok)
        {
            _done = true;
            return;
        }

        _error = result.RateLimited ? T.Contact.Form.RateLimited
            : result.CaptchaFailed ? T.Contact.Form.CaptchaWrong
            : T.Contact.Form.Failed;
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

        /// <summary>허니팟. 사람에게는 안 보이는 칸이라 비어 있어야 정상이다. 채워져 있으면 서버가 조용히 버리고 성공 응답을 준다.</summary>
        public string? Website { get; set; }

        /// <summary>캡차 토큰(숨은 칸)과 방문자가 친 글자.</summary>
        public string? CaptchaToken { get; set; }
        public string? CaptchaAnswer { get; set; }
    }
}
