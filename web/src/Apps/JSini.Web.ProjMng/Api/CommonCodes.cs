using JSini.Web.Components.Data;
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
/// [회로 바깥에서 캐싱하되 **사람마다 따로 담는다**]
///
/// 한동안 이 클래스가 <c>Dictionary</c> 를 직접 들고 있었다. 이 서비스가
/// scoped 라 <b>업무를 넘나들면 그 표가 통째로 사라졌다</b> — Piral 모듈
/// 컨테이너가 갈리기 때문이다. 지금은 <see cref="ReferenceData"/> 에 맡긴다.
///
/// 다른 참조자료(회사 목록 · 범용 셀렉트 설정 · 장례식장 공통코드)는 모두가
/// 나눠 쓰는데 <b>여기만 <c>PerUserAsync</c> 다.</b> 이유는 하나다.
///
/// <para>
/// ProjMngServer 의 <c>UserIdentityActionFilter</c> 가 <b>모든 요청의 본문에</b>
/// 부르는 사람을 실어 넣는다(<c>dto.SSUserId = userId</c>). 그래서
/// <c>sp_projCommon</c> 은 <b>누가 물었는지를 안다</b> — 그것으로 거르는지는
/// 프로시저 안을 봐야 알 수 있고, 그 DB(<c>jsini.co.kr:15432</c>)는 개발망에서
/// 풀리지 않아 확인하지 못했다.
/// </para>
///
/// <para>
/// 확인하지 못한 것을 나눠 쓰면 틀렸을 때 나는 일이 <b>남의 코드 목록이
/// 보이는 것</b>이다. 사람마다 담아도 <b>고치려던 문제는 그대로 사라진다</b> —
/// 업무를 넘나들 때 다시 읽는 일이 없어진다. 잃는 것은 사람 사이의 중복뿐이다.
/// </para>
///
/// <para>
/// 프로시저가 <c>SSUserId</c> 를 안 본다는 것이 확인되면 <c>SharedAsync</c> 로
/// 바꾼다. <b>그때 근거를 이 자리에 적는다.</b>
/// </para>
/// </summary>
public sealed class CommonCodes(ProjMngClient client, ReferenceData data)
{
    private const string Proc = "sp_projCommon";

    /// <summary>참조자료 통 안에서의 묶음 이름. 코드를 고치는 화면이 이 이름으로 버린다.</summary>
    public const string Group = "projmng.common-code";

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
        var cached = await data.PerUserAsync(Group, $"{codeId} {key}",
            () => LoadAsync(codeId, key, cancellationToken));

        return cached ?? [];
    }

    /// <summary>
    /// 실제로 읽는다. <b><c>null</c> 을 돌려주면 통에 담기지 않는다</b> —
    /// 실패를 그렇게 알린다.
    /// </summary>
    private async Task<IReadOnlyList<CommonCodeItem>?> LoadAsync(
        string codeId,
        string key,
        CancellationToken cancellationToken)
    {
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
            // 고르개가 비어 보인다. 통에는 넣지 않으므로 서버가 돌아오면 다음
            // 조회에서 저절로 채워진다. `BizOptions` 가 같은 선택을 한다.
            return null;
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

        return items;
    }

    /// <summary>
    /// 캐시를 비운다. <b>코드를 편집하는 화면이 저장 뒤에 부른다.</b>
    ///
    /// 안 부르면 방금 고친 코드가 다른 화면의 드롭다운에 반영되지 않는다.
    /// 사용자는 저장이 안 된 줄 알고 같은 일을 반복한다.
    /// </summary>
    /// <remarks>
    /// 통이 하나라 <b>부르면 모두에게 반영된다.</b> 전에는 부른 사람의 회로에서만
    /// 비어서, 다른 사용자는 옛 값을 계속 봤다.
    /// </remarks>
    public void Clear() => data.Invalidate(Group);
}
