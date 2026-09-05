using JSini.Web.Http;

namespace JSini.Web.Funeral.Api;

/// <summary>공통코드 한 건. 화면은 값과 이름만 쓴다.</summary>
public sealed class CommonCode
{
    public string Id { get; set; } = string.Empty;

    /// <summary>저장되는 값. 호실의 <c>roomType</c> 같은 칸에 이 값이 들어간다.</summary>
    public string CodeValue { get; set; } = string.Empty;

    /// <summary>사람이 읽는 이름.</summary>
    public string CodeName { get; set; } = string.Empty;

    public int SortOrder { get; set; }
    public int Status { get; set; } = 1;
}

/// <summary>
/// 공통코드를 읽어 온다 — AuthServer 의 <c>scom.common_codes</c>.
///
/// [왜 이 모듈에 또 두는가]
///
/// 포털관리(<c>AdminClient</c>)에도 같은 조회가 있다. 업무 모듈끼리는 참조할 수
/// 없고(의존 규칙 2), 아직 두 모듈이 쓰므로 이 저장소의 규칙대로 <b>복제</b>한다.
/// 세 번째 모듈이 필요해지면 그때 <c>JSini.Web.Components</c> 로 올린다.
///
/// 프로젝트관리의 공통코드와는 <b>다른 표</b>다 — 그쪽은 ProjMng 서버의 저장
/// 프로시저가 다룬다. 이름이 같아서 헷갈리지만 서로 자료를 주고받지 않는다.
///
/// [회로 수명 동안 캐싱한다]
///
/// 호실 구분·사망 종류 같은 목록은 몇 달에 한 번 바뀐다. 화면을 열 때마다
/// 받으면 왕복만 늘고, 한 화면이 코드 묶음을 서넛 쓰는 경우도 있다.
/// Vue 의 <c>useDictStore</c> 와 같은 폭이다.
/// </summary>
public sealed class CommonCodeClient(GatewayClient gateway)
{
    private readonly Dictionary<string, IReadOnlyList<CommonCode>> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 한 묶음의 코드들. 중지된 것은 뺀다.
    ///
    /// <b><c>hierarchical</c> 을 반드시 실어야 한다.</b> 서버가 그 값을
    /// nullable 이 아닌 필수로 받아, 빠뜨리면 500 이 난다 — 「서버가 죽었나」로
    /// 읽히는 종류의 실패다.
    /// </summary>
    public async Task<IReadOnlyList<CommonCode>> GetAsync(
        string groupCode, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(groupCode, out var cached))
        {
            return cached;
        }

        try
        {
            var rows = await gateway.GetListAsync<CommonCode>(
                $"auth/system/common-code/{Uri.EscapeDataString(groupCode)}?hierarchical=false", ct);

            var usable = rows.Where(c => c.Status == 1).OrderBy(c => c.SortOrder).ToList();

            _cache[groupCode] = usable;
            return usable;
        }
        catch (ApiException)
        {
            // 코드를 못 읽었다고 화면을 세우지 않는다. 고르개가 비어 있을 뿐이고,
            // 이미 저장된 값은 코드값 그대로 보인다.
            //
            // 캐시에는 넣지 않는다 — 다음에 다시 시도할 수 있어야 한다.
            return [];
        }
    }

    /// <summary>
    /// 코드값 → 이름 표. 표에서 저장된 값을 사람이 읽는 글자로 바꿀 때 쓴다.
    ///
    /// 표에 없는 값이면 <b>값을 그대로</b> 보여 준다. 빈칸으로 두면 자료가
    /// 없는 것처럼 보이는데, 실제로는 코드 목록에서 지워진 옛 값일 때가 많다.
    /// </summary>
    public async Task<Func<string?, string>> LabelerAsync(
        string groupCode, CancellationToken ct = default)
    {
        var codes = await GetAsync(groupCode, ct);
        var map = codes.ToDictionary(c => c.CodeValue, c => c.CodeName, StringComparer.OrdinalIgnoreCase);

        return value => string.IsNullOrEmpty(value)
            ? string.Empty
            : map.GetValueOrDefault(value, value);
    }
}
