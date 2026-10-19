using System.Net;
using System.Net.Http.Json;

namespace JSini.PublicSite.Api;

/// <summary>SiteServer 의 공개 조회(게이트웨이 경유). 인증이 없어 일반 HttpClient 를 쓴다. API 가 실패하면 빈 값을 돌려주고 화면은 그대로 그린다(소개 사이트가 백엔드 상태에 묶여 죽는 것이 최악이다). 그래서 조회는 예외를 밖으로 내보내지 않는다.</summary>
public sealed class SiteApi
{
    private readonly HttpClient _http;
    private readonly string _publicBase;
    private readonly ILogger<SiteApi> _logger;

    public SiteApi(HttpClient http, IConfiguration config, ILogger<SiteApi> logger)
    {
        _http = http;
        _logger = logger;

        // 브라우저가 직접 여는 주소(표지 이미지·내려받기)의 밑동. 따로 정하지 않으면 서버 호출 주소의 오리진을 쓴다.
        var configured = config["Gateway:PublicBaseUrl"];
        _publicBase = string.IsNullOrWhiteSpace(configured)
            ? new Uri(config["Gateway:BaseUrl"] ?? "http://localhost:5265/api/").GetLeftPart(UriPartial.Authority)
            : configured.TrimEnd('/');
    }

    /// <summary>이 저장소의 응답 봉투. data.result 안에 목록이 들어온다</summary>
    private sealed class Envelope<T>
    {
        public bool Success { get; set; }
        public EnvelopeData<T>? Data { get; set; }
    }

    private sealed class EnvelopeData<T>
    {
        public List<T>? Result { get; set; }
    }

    private async Task<List<T>> GetListAsync<T>(string path)
    {
        try
        {
            using var res = await _http.GetAsync(path);
            if (!res.IsSuccessStatusCode)
            {
                return [];
            }

            var json = await res.Content.ReadFromJsonAsync<Envelope<T>>();
            return json?.Data?.Result ?? [];
        }
        catch (Exception ex)
        {
            // 게이트웨이가 내려가 있어도 화면은 빈 상태로 그린다.
            _logger.LogWarning(ex, "SiteServer 조회 실패: {Path}", path);
            return [];
        }
    }

    public Task<List<Section>> SectionsAsync(string locale, string? keyPrefix = null) =>
        GetListAsync<Section>(
            $"site/sections?locale={locale}{(keyPrefix is null ? string.Empty : $"&keyPrefix={Uri.EscapeDataString(keyPrefix)}")}");

    public Task<List<PostListItem>> PostsAsync(string locale, int take = 20) =>
        GetListAsync<PostListItem>($"site/posts?locale={locale}&take={take}");

    public async Task<PostDetail?> PostAsync(string locale, string slug)
    {
        var rows = await GetListAsync<PostDetail>($"site/posts/{Uri.EscapeDataString(slug)}?locale={locale}");
        return rows.FirstOrDefault();
    }

    public Task<List<DownloadItem>> DownloadsAsync(string locale, string? category = null) =>
        GetListAsync<DownloadItem>(
            $"site/downloads?locale={locale}{(category is null ? string.Empty : $"&category={Uri.EscapeDataString(category)}")}");

    /// <summary>게이트웨이 상대 경로(/api/...)를 브라우저가 열 수 있는 절대 주소로 바꾼다. 이 앱(:5556)은 게이트웨이(:5265)와 오리진이 달라 마크업에 절대 주소를 박는다.</summary>
    public string? PublicUrl(string? gatewayPath) =>
        string.IsNullOrEmpty(gatewayPath) ? gatewayPath : _publicBase + gatewayPath;

    /// <summary>문의를 접수한다. 실패 이유는 자세히 주지 않는다. 허니팟에 걸린 것도 서버가 성공으로 답하므로(봇에게 단서를 주지 않으려고) 화면도 성공으로 처리한다. 게이트웨이가 IP 당 분당 3회로 조이므로 429 가 올 수 있다.</summary>
    public async Task<InquiryResult> SubmitInquiryAsync(InquiryRequest body)
    {
        try
        {
            using var res = await _http.PostAsJsonAsync("site/inquiries", body);

            if (res.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return new InquiryResult(Ok: false, RateLimited: true);
            }

            ServerReply? json = null;
            try
            {
                json = await res.Content.ReadFromJsonAsync<ServerReply>();
            }
            catch
            {
                // 본문이 JSON 이 아니어도 상태 코드만으로 판단한다.
            }

            return new InquiryResult(Ok: res.IsSuccessStatusCode && json?.Success != false, Message: json?.Message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "문의 접수 실패");
            return new InquiryResult(Ok: false);
        }
    }

    private sealed class ServerReply
    {
        public bool? Success { get; set; }
        public string? Message { get; set; }
    }

    /// <summary>조회를 센다. 실패해도 아무것도 하지 않는다. 집계는 부수 효과라 페이지 렌더를 기다리게 하지 않는다(fire-and-forget).</summary>
    public void RecordVisit(string path, string locale)
    {
        _ = RecordVisitCoreAsync(path, locale);
    }

    private async Task RecordVisitCoreAsync(string path, string locale)
    {
        try
        {
            using var res = await _http.PostAsync(
                $"site/visits?path={Uri.EscapeDataString(path)}&locale={locale}", content: null);
        }
        catch
        {
            // 집계 실패는 화면과 무관하다.
        }
    }
}
