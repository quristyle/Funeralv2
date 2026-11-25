using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace HelpDeskServer.Services;

/// <summary>회사 이름을 푸는 단 하나의 통로. 정본은 <b>포털</b>(<c>scom.companies</c>)이다. 헬프데스크에는 회사 표가 없다.</summary>
public interface IPortalCompanyDirectory {
  /// <summary>회사 아이디 → 이름. 못 읽으면 빈 표를 준다(집계가 죽지 않게).</summary>
  Task<IReadOnlyDictionary<string, string>> GetNamesAsync(CancellationToken ct = default);

  /// <summary>회사 하나의 이름. 모르면 null.</summary>
  Task<string?> GetNameAsync(string? companyId, CancellationToken ct = default);

  /// <summary>이 로그인 아이디가 포털관리 「사용처 담당자」에서 헬프데스크(HELP_DESK) 담당으로 걸린 회사들. 못 읽으면 빈 목록 — 판정은 막는 쪽으로 떨어진다.</summary>
  Task<IReadOnlyList<string>> GetManagedCompanyIdsAsync(string loginId, CancellationToken ct = default);

  /// <summary>그 회사를 「사용처 담당자」(HELP_DESK)로 맡은 사람들 중 <paramref name="roleId"/> 역할을 가진 이의 로그인 아이디. 못 읽으면 빈 목록.</summary>
  Task<IReadOnlyList<string>> GetManagerLoginIdsAsync(string companyId, string roleId, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class PortalCompanyDirectory : IPortalCompanyDirectory {
  /// <summary>설정에서 AuthServer 주소를 읽는 자리.</summary>
  public const string BaseUrlKey = "Auth:BaseUrl";

  private const string CacheKey = "portal-companies";

  /// <summary>얼마나 들고 있을지. 회사는 거의 안 바뀌고, 바뀌어도 이름 표시가 몇 분 늦는 것뿐이라 짧게 잡을 이유가 없다.</summary>
  private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(5);

  private readonly IHttpClientFactory _httpClientFactory;
  private readonly IMemoryCache _cache;
  private readonly IConfiguration _configuration;
  private readonly ILogger<PortalCompanyDirectory> _logger;

  /// <summary>서비스를 생성한다.</summary>
  public PortalCompanyDirectory(
      IHttpClientFactory httpClientFactory,
      IMemoryCache cache,
      IConfiguration configuration,
      ILogger<PortalCompanyDirectory> logger) {
    _httpClientFactory = httpClientFactory;
    _cache = cache;
    _configuration = configuration;
    _logger = logger;
  }

  /// <inheritdoc />
  public async Task<IReadOnlyDictionary<string, string>> GetNamesAsync(CancellationToken ct = default) {
    if (_cache.TryGetValue(CacheKey, out IReadOnlyDictionary<string, string>? cached) && cached is not null) {
      return cached;
    }

    var names = await FetchAsync(ct);

    // 못 읽었을 때는 담아 두지 않는다. 담으면 포털이 잠깐 안 떠 있던 사이에
    // 받은 빈 표를 5분 동안 정답처럼 돌려주게 된다.
    if (names.Count > 0) {
      _cache.Set(CacheKey, names, CacheFor);
    }

    return names;
  }

  /// <inheritdoc />
  public async Task<string?> GetNameAsync(string? companyId, CancellationToken ct = default) {
    if (string.IsNullOrWhiteSpace(companyId)) return null;

    var names = await GetNamesAsync(ct);
    return names.TryGetValue(companyId, out var name) ? name : null;
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<string>> GetManagedCompanyIdsAsync(string loginId, CancellationToken ct = default) {
    var baseUrl = _configuration[BaseUrlKey]?.TrimEnd('/');
    if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(loginId)) return [];

    try {
      var client = _httpClientFactory.CreateClient();
      client.Timeout = TimeSpan.FromSeconds(10);

      // 게이트웨이를 거치지 않으므로 AuthServer 가 읽는 신원 헤더를 직접 싣는다. /mine 은 그 사람의 것만 준다.
      using var request = new HttpRequestMessage(
          HttpMethod.Get, $"{baseUrl}/system/company-usage-managers/mine?codeValue=HELP_DESK");
      request.Headers.Add("X-User-Id", loginId);

      using var response = await client.SendAsync(request, ct);
      response.EnsureSuccessStatusCode();

      await using var stream = await response.Content.ReadAsStreamAsync(ct);
      using var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

      if (!json.RootElement.TryGetProperty("data", out var data)
          || !data.TryGetProperty("result", out var rows)
          || rows.ValueKind != JsonValueKind.Array) {
        return [];
      }

      return [.. rows.EnumerateArray()
          .Where(r => r.ValueKind == JsonValueKind.String)
          .Select(r => r.GetString()!)
          .Where(id => !string.IsNullOrWhiteSpace(id))];
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException) {
      _logger.LogWarning(ex, "{LoginId} 의 헬프데스크 담당 회사를 읽지 못했습니다 ({BaseUrl}).", loginId, baseUrl);
      return [];
    }
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<string>> GetManagerLoginIdsAsync(string companyId, string roleId, CancellationToken ct = default) {
    var baseUrl = _configuration[BaseUrlKey]?.TrimEnd('/');
    if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(companyId)) return [];

    try {
      var client = _httpClientFactory.CreateClient();
      client.Timeout = TimeSpan.FromSeconds(10);

      // 서비스 간 호출 전용 길이다 — X-User-Role 을 싣지 않아야 받는다.
      using var request = new HttpRequestMessage(
          HttpMethod.Get,
          $"{baseUrl}/system/company-usage-managers/of?companyId={Uri.EscapeDataString(companyId)}"
          + $"&codeValue=HELP_DESK&role={Uri.EscapeDataString(roleId)}");
      request.Headers.Add("X-User-Id", "system:helpdesk");

      using var response = await client.SendAsync(request, ct);
      response.EnsureSuccessStatusCode();

      await using var stream = await response.Content.ReadAsStreamAsync(ct);
      using var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

      if (!json.RootElement.TryGetProperty("data", out var data)
          || !data.TryGetProperty("result", out var rows)
          || rows.ValueKind != JsonValueKind.Array) {
        return [];
      }

      return [.. rows.EnumerateArray()
          .Where(r => r.ValueKind == JsonValueKind.String)
          .Select(r => r.GetString()!)
          .Where(id => !string.IsNullOrWhiteSpace(id))];
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException) {
      _logger.LogWarning(ex, "회사 {CompanyId} 의 헬프데스크 담당자를 읽지 못했습니다 ({BaseUrl}).", companyId, baseUrl);
      return [];
    }
  }

  private async Task<IReadOnlyDictionary<string, string>> FetchAsync(CancellationToken ct) {
    var baseUrl = _configuration[BaseUrlKey]?.TrimEnd('/');
    if (string.IsNullOrWhiteSpace(baseUrl)) {
      _logger.LogWarning("{Key} 가 비어 있어 포털 회사 목록을 읽지 못했습니다.", BaseUrlKey);
      return new Dictionary<string, string>();
    }

    try {
      var client = _httpClientFactory.CreateClient();
      client.Timeout = TimeSpan.FromSeconds(10);

      using var response = await client.GetAsync($"{baseUrl}/system/companies", ct);
      response.EnsureSuccessStatusCode();

      await using var stream = await response.Content.ReadAsStreamAsync(ct);
      using var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

      // 봉투는 { success, data: { result: [...] } } 다.
      if (!json.RootElement.TryGetProperty("data", out var data)
          || !data.TryGetProperty("result", out var rows)
          || rows.ValueKind != JsonValueKind.Array) {
        _logger.LogWarning("포털 회사 목록의 모양이 예상과 다릅니다.");
        return new Dictionary<string, string>();
      }

      var names = new Dictionary<string, string>(StringComparer.Ordinal);
      foreach (var row in rows.EnumerateArray()) {
        var id = Text(row, "id");
        if (string.IsNullOrWhiteSpace(id)) continue;
        names[id] = Text(row, "name") ?? id;
      }

      return names;
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException) {
      // 이름을 못 읽는 것이 집계를 못 주는 것보다 낫다. 화면에는 아이디가 나온다.
      _logger.LogWarning(ex, "포털 회사 목록을 읽지 못했습니다 ({BaseUrl}).", baseUrl);
      return new Dictionary<string, string>();
    }
  }

  /// <summary>대소문자를 가리지 않고 글자 속성을 꺼낸다.</summary>
  private static string? Text(JsonElement element, string property) {
    if (element.ValueKind != JsonValueKind.Object) return null;

    foreach (var member in element.EnumerateObject()) {
      if (!string.Equals(member.Name, property, StringComparison.OrdinalIgnoreCase)) continue;

      return member.Value.ValueKind switch {
        JsonValueKind.String => member.Value.GetString(),
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        _ => member.Value.ToString(),
      };
    }

    return null;
  }
}
