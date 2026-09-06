using JSini.Web.Http;

namespace JSini.Web.ProjMng.Api;

/// <summary>공통코드 한 건.</summary>
/// <param name="Code">코드값. 그리드 셀에 실제로 저장되는 값이다.</param>
/// <param name="Name">사람이 읽는 이름. 드롭다운과 셀에 보이는 값이다.</param>
/// <param name="Others">
/// 프로시저가 함께 돌려준 나머지 컬럼 전체 (<c>db_type</c> · <c>db_nick</c> ·
/// <c>db_schema</c> 등). 개발 도구 화면들이 DB 선택 드롭다운에서 고른 항목의
/// 부가 정보를 프로시저 파라미터로 되돌려 보낼 때 쓴다.
/// </param>
public sealed record CommonCodeItem(
    string Code,
    string Name,
    IReadOnlyDictionary<string, string> Others);

/// <summary>
/// 공통코드를 읽고 캐시한다.
///
/// [왜 biz-select 를 거치지 않나]
///
/// Vue 는 포털의 범용 셀렉트 장치(<c>scom.biz_select_configs</c> 의
/// <c>projmng_common</c> 행)를 통해 이걸 읽었다. 메타데이터가 "MSA=projmng ·
/// POST /Proj · 고정 파라미터 <c>{ProcName:'sp_projCommon'}</c>" 를 정해 준다.
///
/// 여기서는 그 프로시저를 <b>직접</b> 부른다. 프로젝트관리의 드롭다운은 전부
/// 이 프로시저 하나를 <c>code_id</c> 만 바꿔 부르므로 메타데이터를 한 번 더
/// 거칠 이유가 없고, 거치면 포털의 그 장치가 살아 있어야만 이 앱이 뜬다 —
/// 앱을 나눈 뜻에 어긋난다.
///
/// (포털의 범용 셀렉트가 필요한 화면이 나중에 생기면 그때 Blazor Common 으로
/// 따로 옮긴다. 그건 이 앱만의 문제가 아니다.)
///
/// [수명은 scoped 다]
///
/// 회로(사용자) 하나에 하나. 싱글턴으로 두면 코드를 고친 사람의 화면에서만
/// 캐시가 비고 다른 사용자는 옛 값을 계속 본다 — 그 반대도 마찬가지로 나쁘다.
/// 코드 종류가 많지 않아 회로마다 다시 읽어도 부담이 없다.
/// </summary>
public sealed class CommonCodes(ProjMngClient client)
{
    private const string Proc = "sp_projCommon";

    private readonly Dictionary<string, IReadOnlyList<CommonCodeItem>> _cache = [];

    /// <summary>
    /// 코드 목록을 읽는다. 같은 <paramref name="codeId"/> 는 한 번만 읽는다.
    /// </summary>
    /// <param name="codeId">코드 종류 (<c>CODE_TYPE</c> · <c>projdb</c> …)</param>
    /// <param name="key">종류 안에서 다시 거를 값. 안 쓰는 코드가 대부분이다.</param>
    /// <param name="cancellationToken">취소 토큰</param>
    public async Task<IReadOnlyList<CommonCodeItem>> GetAsync(
        string codeId,
        string key = "",
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{codeId} {key}";
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        ProjMngResult result;

        try
        {
            result = await client.DbContAsync(
                Proc,
                new Dictionary<string, object?> { ["code_id"] = codeId, ["etc0"] = key },
                cancellationToken: cancellationToken);
        }
        catch (ApiException)
        {
            // 드롭다운 하나가 못 읽었다고 화면을 죽이지 않는다.
            //
            // 이 메서드는 `CodeSelect` 가 그리는 중에 부른다. 여기서 예외가
            // 밖으로 나가면 **회로가 통째로 끊겨** 탭 전체가 멎는다 — 화면이
            // DataPage 를 상속했는지와 무관하다. 그 감싸개는 화면이 부르는
            // 조회를 감쌀 뿐, 부품이 스스로 부르는 것까지 덮지 못한다.
            //
            // 빈 목록을 돌려주면 고르개가 비어 보인다. 캐시에는 넣지 않으므로
            // 서버가 돌아오면 다음 조회에서 저절로 채워진다.
            // `BizOptions` 가 같은 자리에서 같은 선택을 한다.
            return [];
        }

        var items = new List<CommonCodeItem>(result.Rows?.Count ?? 0);

        foreach (var row in result.Rows ?? [])
        {
            // 컬럼 이름은 프로시저가 정한다. Vue 도 메타데이터가 없을 때
            // code·name 으로 떨어지도록 두었으니 같은 이름을 쓴다.
            var others = row.ToDictionary(
                pair => pair.Key,
                pair => pair.Value?.ToString() ?? string.Empty,
                StringComparer.Ordinal);

            items.Add(new CommonCodeItem(
                others.GetValueOrDefault("code", string.Empty),
                others.GetValueOrDefault("name", string.Empty),
                others));
        }

        _cache[cacheKey] = items;
        return items;
    }

    /// <summary>
    /// 캐시를 비운다. <b>코드를 편집하는 화면이 저장 뒤에 부른다.</b>
    ///
    /// 안 부르면 방금 고친 코드가 다른 화면의 드롭다운에 반영되지 않는다.
    /// 사용자는 저장이 안 된 줄 알고 같은 일을 반복한다.
    /// </summary>
    public void Clear() => _cache.Clear();
}
