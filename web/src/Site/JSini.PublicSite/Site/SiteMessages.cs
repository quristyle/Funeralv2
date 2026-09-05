namespace JSini.PublicSite.Site;

/// <summary>
/// 화면에 붙박인 문구.
///
/// 원본: <c>fronts/apps/jsini-site/src/i18n/messages.ts</c>.
///
/// [번역 라이브러리를 얹지 않는 이유]
///
/// 이 사이트의 문구는 대부분 DB(<c>site.sections</c>)에서 온다. 여기 남는 것은
/// 메뉴 이름처럼 코드에 붙은 몇 개뿐이라, 그 정도를 위해 리소스 파일과
/// 지역화 미들웨어를 얹으면 얻는 것보다 읽을 것이 늘어난다.
///
/// 언어를 늘릴 때는 <see cref="Locales"/> 에 코드를 더하고 <see cref="All"/> 에
/// 한 줄을 더한다. DB 쪽 언어 열도 같은 코드를 쓴다(SiteServer 의 NormalizeLocale).
/// </summary>
public static class SiteMessages
{
    /// <summary>지원하는 언어. 주소의 첫 조각이 이 값이다.</summary>
    public static readonly string[] Locales = ["ko", "en"];

    /// <summary>언어를 못 알아볼 때 쓰는 값.</summary>
    public const string DefaultLocale = "ko";

    /// <summary>
    /// 바깥에서 들어온 문자열을 아는 값으로만 좁힌다.
    ///
    /// 주소의 언어 조각은 사용자가 아무렇게나 칠 수 있다. 좁혀 두지 않으면
    /// 사전 조회가 <c>KeyNotFoundException</c> 으로 죽는다.
    /// </summary>
    public static string Normalize(string? value) =>
        value is not null && Locales.Contains(value, StringComparer.OrdinalIgnoreCase)
            ? value.ToLowerInvariant()
            : DefaultLocale;

    /// <summary>이 언어의 문구.</summary>
    public static Messages For(string? locale) => All[Normalize(locale)];

    /// <summary>다른 언어 코드. 언어 전환 링크가 쓴다.</summary>
    public static string Other(string? locale) => Normalize(locale) == "ko" ? "en" : "ko";

    private static readonly Dictionary<string, Messages> All = new(StringComparer.Ordinal)
    {
        ["ko"] = new Messages
        {
            Nav = new NavMessages
            {
                Home = "홈",
                About = "회사소개",
                Work = "구축 사례",
                News = "뉴스",
                Downloads = "자료실",
                Contact = "문의",
            },
            Hero = new HeroMessages
            {
                Eyebrow = "납품에서 끝나지 않습니다",
                Headline = "만들고,\n계속 함께 갑니다",
                Lead = "업무 시스템을 만들어 납품하고, 그 뒤로도 유지보수 · 업그레이드 · 보수 관리를 이어 갑니다. 헬프데스크를 직접 운영해 언제든 닿을 수 있게 두었습니다.",
                Cta = "구축 사례 보기",
            },
            Work = new WorkMessages
            {
                Title = "구축 사례",
                Lead = "만들어 납품하고 지금도 함께 운영하는 시스템들입니다. 고객사와의 약속에 따라 이름 대신 분야로 적습니다.",
                CtaLead = "쓰고 계신 시스템도\n같은 방식으로 이어받습니다",
                MockupNote = "실제 화면을 본뜬 재현 이미지입니다. 표시된 자료는 모두 가상입니다.",
            },
            Common = new CommonMessages
            {
                ReadMore = "자세히",
                Download = "내려받기",
                Empty = "등록된 내용이 없습니다.",
                BackToList = "목록으로",
                LangLabel = "언어",
            },
            Contact = new ContactMessages
            {
                Title = "문의",
                Lead = "제안 · 도입 문의를 남겨 주시면 담당자가 연락드립니다.",
                Email = "quristyle@gmail.com",
                Form = new ContactFormMessages
                {
                    Name = "이름",
                    Company = "회사명",
                    EmailField = "이메일",
                    Phone = "연락처",
                    Subject = "제목",
                    Message = "내용",
                    Optional = "선택",
                    Consent = "개인정보 수집·이용에 동의합니다.",
                    ConsentTitle = "개인정보 수집·이용 동의",
                    ConsentFallback = "문의 답변을 위해 이름 · 이메일 · 문의 내용을 수집하며, 접수일로부터 3년간 보관합니다.",
                    Submit = "보내기",
                    Done = "문의가 접수되었습니다. 담당자가 확인한 뒤 연락드립니다.",
                    Failed = "접수하지 못했습니다. 잠시 후 다시 시도해 주십시오.",
                    RateLimited = "요청이 너무 잦습니다. 잠시 후 다시 시도해 주십시오.",
                    Required = "필수 항목을 채워 주십시오.",
                },
            },
            Footer = new FooterMessages
            {
                Rights = "JSINI. All rights reserved.",
                Portal = "관리 포털",
            },
        },

        ["en"] = new Messages
        {
            Nav = new NavMessages
            {
                Home = "Home",
                About = "Company",
                Work = "Work",
                News = "News",
                Downloads = "Resources",
                Contact = "Contact",
            },
            Hero = new HeroMessages
            {
                Eyebrow = "Delivery is not the end",
                Headline = "We build it,\nthen we stay",
                Lead = "We build and deliver business systems — then keep them running, maintained and up to date. We run our own help desk so there is always somewhere to reach us.",
                Cta = "See our work",
            },
            Work = new WorkMessages
            {
                Title = "Work",
                Lead = "Systems we built, delivered, and still run alongside our clients. Described by field rather than by name, as agreed with them.",
                CtaLead = "The system you already run\ncan be taken over the same way",
                MockupNote = "An illustration modelled on the real screen. Every value shown is fictitious.",
            },
            Common = new CommonMessages
            {
                ReadMore = "Read more",
                Download = "Download",
                Empty = "Nothing here yet.",
                BackToList = "Back to list",
                LangLabel = "Language",
            },
            Contact = new ContactMessages
            {
                Title = "Contact",
                Lead = "Leave a note and we will get back to you.",
                Email = "quristyle@gmail.com",
                Form = new ContactFormMessages
                {
                    Name = "Name",
                    Company = "Company",
                    EmailField = "Email",
                    Phone = "Phone",
                    Subject = "Subject",
                    Message = "Message",
                    Optional = "optional",
                    Consent = "I consent to the collection and use of my personal data.",
                    ConsentTitle = "Consent to collection and use of personal data",
                    ConsentFallback = "We collect your name, email, and message to answer your enquiry, and keep them for three years.",
                    Submit = "Send",
                    Done = "Your enquiry has been received. We will be in touch.",
                    Failed = "We could not accept it. Please try again in a moment.",
                    RateLimited = "Too many requests. Please try again in a moment.",
                    Required = "Please fill in the required fields.",
                },
            },
            Footer = new FooterMessages
            {
                Rights = "JSINI. All rights reserved.",
                Portal = "Admin portal",
            },
        },
    };
}

/// <summary>한 언어의 문구 묶음.</summary>
public sealed class Messages
{
    public required NavMessages Nav { get; init; }
    public required HeroMessages Hero { get; init; }
    public required WorkMessages Work { get; init; }
    public required CommonMessages Common { get; init; }
    public required ContactMessages Contact { get; init; }
    public required FooterMessages Footer { get; init; }
}

/// <summary>차림표 이름.</summary>
public sealed class NavMessages
{
    public required string Home { get; init; }
    public required string About { get; init; }
    public required string Work { get; init; }
    public required string News { get; init; }
    public required string Downloads { get; init; }
    public required string Contact { get; init; }
}

/// <summary>첫 화면 머리말.</summary>
public sealed class HeroMessages
{
    public required string Eyebrow { get; init; }

    /// <summary>줄바꿈이 들어 있다. 화면에서 <c>white-space: pre-line</c> 으로 살린다.</summary>
    public required string Headline { get; init; }

    public required string Lead { get; init; }
    public required string Cta { get; init; }
}

/// <summary>구축 사례 화면. 사례 목록 자체는 DB(<c>work.*</c>)에서 온다.</summary>
public sealed class WorkMessages
{
    public required string Title { get; init; }
    public required string Lead { get; init; }
    public required string CtaLead { get; init; }

    /// <summary>
    /// 그림 아래에 늘 붙는 한 줄.
    ///
    /// 그림은 <b>실제 화면의 캡처가 아니라 그것을 본뜬 재현 이미지</b>다.
    /// 고객사 시스템 화면에는 고인·상주·담당자·설비 운전값 같은 것이 들어 있어
    /// 공개 사이트에 올릴 수 없다. 보는 사람이 진짜 캡처로 오해하지 않도록
    /// 그림마다 이 문장을 붙인다 — <b>빼지 않는다.</b>
    /// </summary>
    public required string MockupNote { get; init; }
}

/// <summary>여러 화면이 함께 쓰는 낱말.</summary>
public sealed class CommonMessages
{
    public required string ReadMore { get; init; }
    public required string Download { get; init; }
    public required string Empty { get; init; }
    public required string BackToList { get; init; }
    public required string LangLabel { get; init; }
}

/// <summary>문의 화면.</summary>
public sealed class ContactMessages
{
    public required string Title { get; init; }
    public required string Lead { get; init; }
    public required string Email { get; init; }
    public required ContactFormMessages Form { get; init; }
}

/// <summary>문의 폼의 칸 이름과 결과 문구.</summary>
public sealed class ContactFormMessages
{
    public required string Name { get; init; }
    public required string Company { get; init; }
    public required string EmailField { get; init; }
    public required string Phone { get; init; }
    public required string Subject { get; init; }
    public required string Message { get; init; }
    public required string Optional { get; init; }
    public required string Consent { get; init; }
    public required string ConsentTitle { get; init; }

    /// <summary>
    /// DB(<c>contact.consent</c>)에 동의 문구 블록이 없을 때 대신 쓸 문장.
    ///
    /// 법률 문구라 코드 배포 없이 고칠 수 있어야 해서 DB 가 정본이지만,
    /// 블록이 없다고 <b>동의 문구가 아예 안 보이는 것</b>이 더 나쁘다.
    /// </summary>
    public required string ConsentFallback { get; init; }

    public required string Submit { get; init; }
    public required string Done { get; init; }
    public required string Failed { get; init; }
    public required string RateLimited { get; init; }
    public required string Required { get; init; }
}

/// <summary>바닥글.</summary>
public sealed class FooterMessages
{
    public required string Rights { get; init; }
    public required string Portal { get; init; }
}
