using JSini.Web.Components.Data;
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
/// [회로 바깥에서 캐싱한다]
///
/// 호실 구분·사망 종류 같은 목록은 몇 달에 한 번 바뀐다. 화면을 열 때마다
/// 받으면 왕복만 늘고, 한 화면이 코드 묶음을 서넛 쓰는 경우도 있다.
/// Vue 의 <c>useDictStore</c> 와 같은 폭이다.
///
/// <para>
/// 한동안 이 클래스가 <c>Dictionary</c> 를 직접 들고 있었다. 그런데 이 서비스가
/// scoped 라 <b>업무를 넘나들면 그 표가 통째로 사라졌다</b> — Piral 모듈
/// 컨테이너가 갈리기 때문이다. 지금은 <see cref="ReferenceData"/> 에 맡긴다.
/// </para>
///
/// <para>
/// <b>모두가 나눠 쓴다(<c>SharedAsync</c>).</b> 근거는 백엔드다 —
/// <c>GET /auth/system/common-code/{groupCode}</c> 는
/// <c>GetCodesByGroupAsync(groupCode, hierarchical)</c> 를 부르고, 그 서비스는
/// <c>UserContext</c> 도 <c>IHttpContextAccessor</c> 도 보지 않는다. 지금 요청의
/// 신원과 무관한 응답이라 사람을 갈라 담을 이유가 없다.
/// </para>
/// </summary>
public sealed class CommonCodeClient(GatewayClient gateway, ReferenceData data)
{
    /// <summary>참조자료 통 안에서의 묶음 이름. 코드를 고치는 화면이 이 이름으로 버린다.</summary>
    public const string Group = "funeral.common-code";

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
        // 묶음 이름을 소문자로 맞춘다. 옛 캐시는 대소문자를 안 가렸고
        // (`StringComparer.OrdinalIgnoreCase`) 부르는 자리가 섞여 쓴다.
        var key = groupCode.ToLowerInvariant();

        var cached = await data.SharedAsync(Group, key, async () =>
        {
            try
            {
                var rows = await gateway.GetListAsync<CommonCode>(
                    $"auth/system/common-code/{Uri.EscapeDataString(groupCode)}?hierarchical=false", ct);

                return (IReadOnlyList<CommonCode>)
                    rows.Where(c => c.Status == 1).OrderBy(c => c.SortOrder).ToList();
            }
            catch (ApiException)
            {
                // 코드를 못 읽었다고 화면을 세우지 않는다. 고르개가 비어 있을
                // 뿐이고, 이미 저장된 값은 코드값 그대로 보인다.
                //
                // **null 을 돌려주면 통에 담기지 않는다** — 다음에 다시 시도할
                // 수 있어야 한다. 빈 목록을 돌려주면 서버가 돌아와도 TTL 동안
                // 빈 고르개가 남는다.
                return null;
            }
        });

        return cached ?? [];
    }

    /// <summary>
    /// 코드값 → 이름 표. 표에서 저장된 값을 사람이 읽는 글자로 바꿀 때 쓴다.
    ///
    /// 표에 없는 값이면 <b>값을 그대로</b> 보여 준다. 빈칸으로 두면 자료가
    /// 없는 것처럼 보이는데, 실제로는 코드 목록에서 지워진 옛 값일 때가 많다.
    /// </summary>
    public async Task<Func<string?, string>> LabelerAsync(
        string groupCode, CancellationToken ct = default)
        => Labeler(await GetAsync(groupCode, ct));

    /// <summary>
    /// <b>이미 받아 둔 목록</b>으로 옮기개를 만든다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 코드 목록과 옮기개를 둘 다 쓰는 화면이 <see cref="LabelerAsync"/> 를
    /// 따로 부르면, 차례로 부를 때는 두 번째가 통에 맞아 공짜지만
    /// <b>나란히 부르면 둘 다 통을 지나쳐 두 번 받는다.</b>
    /// </para>
    ///
    /// <para>
    /// 그래서 화면이 조회를 묶을 때는(<c>Task.WhenAll</c>) 목록만 받고 옮기개는
    /// 이것으로 만든다 — 왕복이 없다.
    /// </para>
    /// </remarks>
    public static Func<string?, string> Labeler(IReadOnlyList<CommonCode> codes)
    {
        var map = codes.ToDictionary(c => c.CodeValue, c => c.CodeName, StringComparer.OrdinalIgnoreCase);

        return value => string.IsNullOrEmpty(value)
            ? string.Empty
            : map.GetValueOrDefault(value, value);
    }
}
