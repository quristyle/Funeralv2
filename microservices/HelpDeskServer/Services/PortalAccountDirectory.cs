using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace HelpDeskServer.Services;

/// <summary>
/// 사람 이름을 푸는 단 하나의 통로. 정본은 <b>포털</b>(<c>scom.accounts</c>)이다.
/// </summary>
/// <remarks>
/// <para>
/// 회사를 푸는 <see cref="IPortalCompanyDirectory"/> 의 사람 짝이다. 그쪽과
/// 같은 판단으로 같은 모양을 쓴다 — 게이트웨이를 거치지 않고 AuthServer 를
/// 직접 부르고, 못 읽으면 빈 표를 돌려주어 <b>화면이 죽는 대신 아이디가
/// 보이게</b> 한다.
/// </para>
/// <para>
/// [열쇠는 GUID 가 아니라 로그인 아이디다]
/// </para>
/// <para>
/// 포털 계정의 기본키는 GUID(<c>scom.accounts.id</c>)지만, 서비스 사이를
/// 오가는 값은 <b>로그인 아이디</b>(<c>scom.accounts.user_id</c>)다 — 토큰의
/// <c>NameIdentifier</c>, 게이트웨이가 실어 주는 <c>X-User-Id</c>,
/// <c>auth_user_links.AuthUserId</c>, 운송관리의 <c>app_user.ExternalUserId</c>
/// 가 모두 그 값이다. GUID 는 토큰에만 있고 헤더로 나가지 않으며, 아래 묶음
/// 조회가 받지도 않는다. 그래서 업무 자료가 사람을 가리킬 때 드는 값도
/// 로그인 아이디여야 한다.
/// </para>
/// <para>
/// [한 번에 묻는다 — 목록 화면이 여기 걸린다]
/// </para>
/// <para>
/// <c>GET /user/faces?ids=a,b,c</c> 는 아이디를 쉼표로 묶어 <b>한 번에
/// 100개</b>까지 받는다(서버 쪽 상한이 그렇다). 줄마다 한 번씩 물으면 스물다섯
/// 줄짜리 목록이 스물다섯 왕복이 된다. 프로젝트관리·생활과환경의
/// <c>UserFaceClient</c> 가 같은 끝점을 같은 묶음 크기로 쓴다.
/// </para>
/// <para>
/// <b>모르는 아이디는 빠져서 온다</b>(없는 값으로 채워 주지 않는다). 그래서
/// 물어본 것을 그대로 믿고 다시 묻지 않도록, 못 찾은 것도 「물어봤다」고
/// 적어 둔다 — 안 그러면 캐시가 영영 비어 매번 다시 묻는다.
/// </para>
/// <para>
/// [부르는 사람이 필요하다]
/// </para>
/// <para>
/// 회사 목록과 달리 이 끝점은 <c>X-User-Id</c> 가 없으면 401 을 준다. 게이트웨이를
/// 건너뛰고 부르므로 그 헤더를 우리가 달아야 한다. AuthServer 는 직접 부르는
/// 쪽의 그 헤더를 이미 신원으로 받아들인다(<c>UserContext</c>) — 새로 뚫는
/// 구멍이 아니라 그 서버가 이미 쓰는 길이다.
/// </para>
/// </remarks>
public interface IPortalAccountDirectory {
  /// <summary>
  /// 로그인 아이디 → 이름. 못 읽은 것은 표에 없다 — 부르는 쪽이 아이디로 물러선다.
  /// </summary>
  Task<IReadOnlyDictionary<string, string>> GetNamesAsync(
      IEnumerable<string> loginIds, CancellationToken ct = default);

  /// <summary>사람 하나의 이름. 모르면 null.</summary>
  Task<string?> GetNameAsync(string? loginId, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class PortalAccountDirectory : IPortalAccountDirectory {
  /// <summary>설정에서 AuthServer 주소를 읽는 자리. 회사 쪽과 같은 열쇠를 쓴다.</summary>
  public const string BaseUrlKey = PortalCompanyDirectory.BaseUrlKey;

  /// <summary>
  /// 한 번에 묻는 아이디 수. <b>AuthServer 의 상한과 같은 값이어야 한다</b>
  /// (<c>UserEndpoints.MaxFaces</c>). 더 크게 잡으면 넘친 만큼이 조용히 빠진
  /// 채로 돌아와, 목록에서 몇 줄만 아이디로 보인다.
  /// </summary>
  private const int Chunk = 100;

  /// <summary>
  /// 이름을 얼마나 들고 있을지. 사람 이름은 회사보다도 덜 바뀌지만, 바꾸고
  /// 나서 한참 옛 이름이 보이면 고친 사람이 안 고쳐진 줄 안다.
  /// </summary>
  private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(5);

  private const string CachePrefix = "portal-account-name:";

  private readonly IHttpClientFactory _httpClientFactory;
  private readonly IMemoryCache _cache;
  private readonly IConfiguration _configuration;
  private readonly ILogger<PortalAccountDirectory> _logger;

  /// <summary>서비스를 생성한다.</summary>
  public PortalAccountDirectory(
      IHttpClientFactory httpClientFactory,
      IMemoryCache cache,
      IConfiguration configuration,
      ILogger<PortalAccountDirectory> logger) {
    _httpClientFactory = httpClientFactory;
    _cache = cache;
    _configuration = configuration;
    _logger = logger;
  }

  /// <inheritdoc />
  public async Task<IReadOnlyDictionary<string, string>> GetNamesAsync(
      IEnumerable<string> loginIds, CancellationToken ct = default) {

    var wanted = loginIds
        .Where(id => !string.IsNullOrWhiteSpace(id))
        .Select(id => id.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    var found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    var missing = new List<string>();

    foreach (var id in wanted) {
      // 못 찾았던 것도 담아 두었다(빈 글자). 그때는 다시 묻지 않고 넘어간다 —
      // 포털에 없는 아이디 하나가 목록을 그릴 때마다 왕복을 만들면 안 된다.
      if (_cache.TryGetValue(CachePrefix + id, out string? cached)) {
        if (!string.IsNullOrEmpty(cached)) found[id] = cached;
        continue;
      }

      missing.Add(id);
    }

    if (missing.Count == 0) {
      return found;
    }

    var baseUrl = _configuration[BaseUrlKey]?.TrimEnd('/');
    if (string.IsNullOrWhiteSpace(baseUrl)) {
      _logger.LogWarning("{Key} 가 비어 있어 포털 계정 이름을 읽지 못했습니다.", BaseUrlKey);
      return found;
    }

    for (var at = 0; at < missing.Count; at += Chunk) {
      var slice = missing.Skip(at).Take(Chunk).ToList();
      var names = await FetchAsync(baseUrl, slice, ct);

      // **못 읽은 것과 없는 것을 가른다.** 통째로 실패했으면(null) 아무것도
      // 담지 않는다 — 담으면 포털이 잠깐 안 떠 있던 사이의 빈 답을 5분 동안
      // 정답처럼 돌려준다. 회사 쪽이 같은 까닭으로 같은 일을 한다.
      if (names is null) {
        continue;
      }

      foreach (var id in slice) {
        var name = names.TryGetValue(id, out var hit) ? hit : string.Empty;

        _cache.Set(CachePrefix + id, name, CacheFor);

        if (name.Length > 0) found[id] = name;
      }
    }

    return found;
  }

  /// <inheritdoc />
  public async Task<string?> GetNameAsync(string? loginId, CancellationToken ct = default) {
    if (string.IsNullOrWhiteSpace(loginId)) return null;

    var names = await GetNamesAsync([loginId], ct);
    return names.TryGetValue(loginId.Trim(), out var name) ? name : null;
  }

  /// <summary>
  /// 한 묶음을 묻는다. <b>통째로 실패하면 <c>null</c></b> — 「없더라」와
  /// 가르려고 빈 표가 아니라 null 을 준다.
  /// </summary>
  private async Task<IReadOnlyDictionary<string, string>?> FetchAsync(
      string baseUrl, IReadOnlyList<string> ids, CancellationToken ct) {

    try {
      var client = _httpClientFactory.CreateClient();
      client.Timeout = TimeSpan.FromSeconds(10);

      var query = string.Join(',', ids.Select(Uri.EscapeDataString));

      using var request = new HttpRequestMessage(
          HttpMethod.Get, $"{baseUrl}/user/faces?ids={query}");

      // 부르는 사람이 없으면 401 이다(머리말). 배경 작업에서도 불리는 길이라
      // 사람의 토큰을 실어 보낼 수가 없어, 묻는 쪽을 우리로 적는다.
      request.Headers.TryAddWithoutValidation("X-User-Id", ServiceCallerId);

      using var response = await client.SendAsync(request, ct);
      response.EnsureSuccessStatusCode();

      await using var stream = await response.Content.ReadAsStreamAsync(ct);
      using var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

      // 봉투는 회사 쪽과 같다 — { success, data: { result: [...] } }.
      if (!json.RootElement.TryGetProperty("data", out var data)
          || !data.TryGetProperty("result", out var rows)
          || rows.ValueKind != JsonValueKind.Array) {
        _logger.LogWarning("포털 계정 목록의 모양이 예상과 다릅니다.");
        return null;
      }

      var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      foreach (var row in rows.EnumerateArray()) {
        var id = Text(row, "userId");
        if (string.IsNullOrWhiteSpace(id)) continue;

        names[id] = Text(row, "name") ?? id;
      }

      return names;
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException) {
      _logger.LogWarning(ex, "포털 계정 이름을 읽지 못했습니다 ({BaseUrl}).", baseUrl);
      return null;
    }
  }

  /// <summary>
  /// AuthServer 에 우리를 밝히는 이름. <b>사람 계정이 아니다</b> — 그 서버는
  /// 이 헤더가 있는지만 보고 받아 주며(<c>UserContext</c>), 이름 조회는 누가
  /// 묻든 같은 답이라 부르는 사람에 따라 갈릴 것이 없다.
  /// </summary>
  private const string ServiceCallerId = "helpdesk-service";

  /// <summary>대소문자를 가리지 않고 글자 속성을 꺼낸다. 회사 쪽과 같은 함수다.</summary>
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
