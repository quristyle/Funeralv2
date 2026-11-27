using Microsoft.EntityFrameworkCore;
using SiteServer.DTOs;
using SiteServer.Data;
using SiteServer.Entities;

namespace SiteServer.Services;

/// <summary>
/// 소개 사이트의 공개 조회와 문의 접수.
/// </summary>
public interface ISiteService
{
    Task<List<SectionDto>> GetSectionsAsync(string locale, string? keyPrefix);
    Task<List<PostListItemDto>> GetPostsAsync(string locale, int take);
    Task<PostDetailDto?> GetPostAsync(string locale, string slug);
    Task<List<DownloadDto>> GetDownloadsAsync(string locale, string? category);

    /// <summary>내려받기 횟수를 하나 올리고 FileServer 주소를 돌려준다. 없으면 null</summary>
    Task<string?> ResolveDownloadAsync(Guid id);

    /// <summary>
    /// 문의를 접수한다. 봇으로 보이면 <c>Saved</c> 도 <c>Rejection</c> 도 비어 있다 — 화면에는 성공처럼
    /// 보여야 한다(봇에게 단서를 주지 않는다). <c>Rejection</c> 은 사람이 고칠 수 있는 잘못(길이·메일 형식)이다.
    /// </summary>
    Task<InquiryIntake> CreateInquiryAsync(InquiryRequestDto request, string? clientIp, string? userAgent);

    /// <summary>조회를 하루 단위로 센다</summary>
    Task RecordVisitAsync(string path, string locale);
}

public class SiteService : ISiteService
{
    private readonly SiteDbContext _db;
    private readonly ILogger<SiteService> _logger;
    private readonly IHttpClientFactory _httpFactory;

    public SiteService(SiteDbContext db, ILogger<SiteService> logger, IHttpClientFactory httpFactory)
    {
        _db = db;
        _logger = logger;
        _httpFactory = httpFactory;
    }

    /// <summary>
    /// 언어 값을 정리한다. 아는 것만 통과시키고 나머지는 <c>ko</c> 로 본다.
    /// 바깥에서 들어온 문자열을 그대로 쿼리에 넣지 않기 위한 것이다.
    /// </summary>
    public static string NormalizeLocale(string? locale) =>
        locale is "en" or "EN" or "en-US" ? "en" : "ko";

    public async Task<List<SectionDto>> GetSectionsAsync(string locale, string? keyPrefix)
    {
        var loc = NormalizeLocale(locale);
        var q = _db.Sections.Where(x => !x.IsDeleted && x.IsPublished && x.Locale == loc);

        if (!string.IsNullOrWhiteSpace(keyPrefix))
        {
            q = q.Where(x => x.SectionKey.StartsWith(keyPrefix));
        }

        return await q
            .OrderBy(x => x.SortOrder).ThenBy(x => x.SectionKey)
            .Select(x => new SectionDto
            {
                SectionKey = x.SectionKey,
                Title = x.Title,
                Subtitle = x.Subtitle,
                Body = x.Body,
                SortOrder = x.SortOrder,
            })
            .ToListAsync();
    }

    public async Task<List<PostListItemDto>> GetPostsAsync(string locale, int take)
    {
        var loc = NormalizeLocale(locale);
        var now = DateTime.UtcNow;

        return await _db.Posts
            .Where(x => !x.IsDeleted && x.IsPublished && x.Locale == loc
                        && x.PublishedAt != null && x.PublishedAt <= now)
            .OrderByDescending(x => x.PublishedAt)
            .Take(Math.Clamp(take, 1, 100))
            .Select(x => new PostListItemDto
            {
                Slug = x.Slug,
                Title = x.Title,
                Summary = x.Summary,
                CoverFileId = x.CoverFileId,
                PublishedAt = x.PublishedAt,
            })
            .ToListAsync();
    }

    public async Task<PostDetailDto?> GetPostAsync(string locale, string slug)
    {
        var loc = NormalizeLocale(locale);
        var now = DateTime.UtcNow;

        return await _db.Posts
            .Where(x => !x.IsDeleted && x.IsPublished && x.Locale == loc && x.Slug == slug
                        && x.PublishedAt != null && x.PublishedAt <= now)
            .Select(x => new PostDetailDto
            {
                Slug = x.Slug,
                Title = x.Title,
                Summary = x.Summary,
                Body = x.Body,
                CoverFileId = x.CoverFileId,
                PublishedAt = x.PublishedAt,
            })
            .FirstOrDefaultAsync();
    }

    public async Task<List<DownloadDto>> GetDownloadsAsync(string locale, string? category)
    {
        var loc = NormalizeLocale(locale);
        var q = _db.Downloads.Where(x => !x.IsDeleted && x.IsPublished && x.Locale == loc);

        if (!string.IsNullOrWhiteSpace(category))
        {
            q = q.Where(x => x.Category == category);
        }

        return await q
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Title)
            .Select(x => new DownloadDto
            {
                Id = x.Id,
                Title = x.Title,
                Description = x.Description,
                Category = x.Category,
                FileName = x.FileName,
                FileSize = x.FileSize,
                DownloadCount = x.DownloadCount,
            })
            .ToListAsync();
    }

    public async Task<string?> ResolveDownloadAsync(Guid id)
    {
        var row = await _db.Downloads
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted && x.IsPublished);

        if (row is null)
        {
            return null;
        }

        // 세는 데 실패해도 내려받기는 막지 않는다. 집계는 부수 효과일 뿐이다.
        try
        {
            row.DownloadCount += 1;
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "내려받기 횟수 증가 실패: {Id}", id);
        }

        return $"/api/file/download/id/{row.FileId}";
    }

    // 칸별 최대 길이. 표는 text 라 막는 것이 여기뿐이고, 그대로 두면 한 번에 수 MB 가 저장되고 메일로도 나간다.
    public const int NameMax = 100, CompanyMax = 200, EmailMax = 254, PhoneMax = 40,
        CategoryMax = 50, SubjectMax = 200, MessageMax = 5000;

    public async Task<InquiryIntake> CreateInquiryAsync(InquiryRequestDto request, string? clientIp, string? userAgent)
    {
        // 허니팟. 사람은 못 보는 칸이라 채워져 있으면 기계다.
        if (!string.IsNullOrWhiteSpace(request.Website))
        {
            _logger.LogInformation("허니팟에 걸린 문의를 버렸습니다. ip={Ip}", clientIp);
            return InquiryIntake.Dropped;
        }

        // 동의 없이 개인정보를 받지 않는다.
        if (!request.Consent)
        {
            return InquiryIntake.Dropped;
        }

        // 한 줄짜리 칸은 줄바꿈·제어문자를 걷는다 — 이름과 제목은 알림 메일의 제목 줄로 들어간다.
        var name = OneLine(request.Name);
        var company = Optional(OneLine(request.Company));
        var email = OneLine(request.Email);
        var phone = Optional(OneLine(request.Phone));
        var category = Optional(OneLine(request.Category));
        var subject = OneLine(request.Subject);
        var raw = request.Message?.Trim() ?? string.Empty;

        if (name.Length == 0 || email.Length == 0 || raw.Length == 0)
        {
            return InquiryIntake.Dropped;
        }

        if (name.Length > NameMax || (company?.Length ?? 0) > CompanyMax || email.Length > EmailMax
            || (phone?.Length ?? 0) > PhoneMax || (category?.Length ?? 0) > CategoryMax
            || subject.Length > SubjectMax || raw.Length > MessageMax)
        {
            _logger.LogInformation("길이를 넘은 문의를 거절했습니다. ip={Ip} 본문={Len}", clientIp, raw.Length);
            return InquiryIntake.Reject("입력한 내용이 너무 깁니다.");
        }

        if (!System.Net.Mail.MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)
        {
            return InquiryIntake.Reject("이메일 주소가 올바르지 않습니다.");
        }

        if (LooksLikeFormSpam(name, company, subject, raw))
        {
            _logger.LogWarning("폼 스팸으로 보이는 문의를 버렸습니다. ip={Ip} email={Email}", clientIp, email);
            return InquiryIntake.Dropped;
        }

        // 본문은 에디터가 만든 HTML 이다. 허용 목록으로 거른 것만 저장한다 —
        // 관리 화면과 메일 본문에 그대로 들어가기 때문이다.
        var message = InquiryHtmlSanitizer.Sanitize(raw);
        if (!message.Contains('<'))
        {
            // 평문으로 온 경우(API 직접 호출 등) 줄바꿈이 HTML 에서 뭉개지지 않게 한다.
            message = message.Replace("\r\n", "\n").Replace("\n", "<br>");
        }

        var row = new SiteInquiry
        {
            Id = Guid.NewGuid(),
            Name = name,
            Company = company,
            Email = email,
            Phone = phone,
            Category = category,
            Subject = subject.Length == 0 ? "(제목 없음)" : subject,
            Message = message,
            Locale = NormalizeLocale(request.Locale),
            ConsentedAt = DateTime.UtcNow,
            ClientIp = clientIp is null ? null : clientIp[..Math.Min(clientIp.Length, 45)],
            // 길이를 잘라 둔다. User-Agent 는 길이 제한이 없어서 그대로 넣으면 표가 지저분해진다.
            UserAgent = userAgent is null ? null : userAgent[..Math.Min(userAgent.Length, 400)],
            Status = "new",
            CreatedBy = "PublicSite",
        };

        _db.Inquiries.Add(row);
        await _db.SaveChangesAsync();

        _logger.LogInformation("문의가 접수됐습니다. id={Id} 분류={Category}", row.Id, row.Category);
        // 담당자 알림은 엔드포인트가 이 저장된 값(거른 본문)으로 보낸다 — 요청 원문을 메일에 넣지 않는다.
        return new InquiryIntake(row, null);
    }

    /// <summary>
    /// 칸마다 페이지 주소만 채워 보내는 폼 탐색 봇(이름·회사·제목·본문이 전부 <c>:https://jsini.co.kr/ko/contact:</c>).
    /// 허니팟 칸은 비워 두어 그것으로는 안 걸린다. 사람은 이름에 주소를 적지 않고, 주소만 있는 본문은 문의가 아니다.
    /// </summary>
    public static bool LooksLikeFormSpam(string name, string? company, string subject, string message)
    {
        if (name.Contains("://", StringComparison.Ordinal)) return true;

        var plain = InquiryHtmlSanitizer.ToPlainText(message);
        var withoutUrls = System.Text.RegularExpressions.Regex.Replace(plain, @"\S*://\S*", " ");
        return !withoutUrls.Any(char.IsLetterOrDigit);
    }

    private static string OneLine(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var cleaned = new string(value.Select(c => char.IsControl(c) ? ' ' : c).ToArray());
        return cleaned.Trim();
    }

    private static string? Optional(string value) => value.Length == 0 ? null : value;

    public async Task RecordVisitAsync(string path, string locale)
    {
        var loc = NormalizeLocale(locale);
        var today = DateTime.UtcNow.Date;

        // 경로를 그대로 쌓으면 쿼리스트링 때문에 행이 무한정 늘어난다. 앞부분만 쓴다.
        var key = path.Split('?')[0];
        if (key.Length > 200)
        {
            key = key[..200];
        }

        var row = await _db.Visits.FirstOrDefaultAsync(
            x => x.VisitDate == today && x.Path == key && x.Locale == loc);

        if (row is null)
        {
            _db.Visits.Add(new SiteVisit
            {
                Id = Guid.NewGuid(),
                VisitDate = today,
                Path = key,
                Locale = loc,
                ViewCount = 1,
            });
        }
        else
        {
            row.ViewCount += 1;
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // 같은 (날짜·경로·언어) 를 두 요청이 동시에 처음 만들면 유일 제약에 걸린다.
            // 집계라서 한 건 놓쳐도 문제가 없다. 다시 시도하지 않는다.
        }
    }
}

/// <summary>문의 접수 결과. 둘 다 비면 봇으로 보고 조용히 버린 것이다.</summary>
public sealed record InquiryIntake(SiteInquiry? Saved, string? Rejection)
{
    public static readonly InquiryIntake Dropped = new(null, null);
    public static InquiryIntake Reject(string reason) => new(null, reason);
}
