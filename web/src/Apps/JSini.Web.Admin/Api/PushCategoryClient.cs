using JSini.Web.Components.Data;
using JSini.Web.Http;

namespace JSini.Web.Admin.Api;

/// <summary>알림구분 한 건. 화면은 값과 이름만 쓴다.</summary>
public sealed class PushCategory
{
    /// <summary>보관되는 값(<c>DEPLOY</c> · <c>HELPDESK</c> …).</summary>
    public string CodeValue { get; set; } = string.Empty;

    /// <summary>사람이 읽는 이름.</summary>
    public string CodeName { get; set; } = string.Empty;

    public int SortOrder { get; set; }
    public int Status { get; set; } = 1;
}

/// <summary>
/// <b>알림구분</b>을 공통코드에서 읽어 온다 — 묶음 <c>NOTI_CATEGORY</c>.
/// </summary>
/// <remarks>
/// <para>
/// [왜 화면마다 목록을 박아 두지 않나]
/// </para>
///
/// <para>
/// 갈래를 코드에 적어 두면 <b>갈래를 하나 늘리는 데 배포가 필요하다.</b>
/// 「배포 알림만 보기」 같은 것은 쓰다 보면 늘어나고 이름도 바뀐다. 목록의
/// 정본을 공통코드(<c>/admin/system/common-code</c>)에 두면 그 자리에서
/// 늘리고 고친다 — 보내는 서비스는 코드값만 알면 된다.
/// </para>
///
/// <para>
/// 다른 업무 모듈에서 같은 고르개가 필요하면 <b>메타데이터 관리</b>
/// (<c>/admin/system/metadata</c>)에 둔 <c>NOTI_CATEGORY</c> 줄을 범용
/// 셀렉트로 읽으면 된다. 포털관리가 그 길을 안 쓰는 것은 이 모듈에 범용
/// 셀렉트 부품이 없어서고(모듈끼리 부품을 나눠 쓰지 못한다 — 의존 규칙 2),
/// 읽는 주소는 둘이 같다.
/// </para>
///
/// <para>
/// [회로 바깥에서 담는다]
/// </para>
///
/// <para>
/// 갈래는 몇 달에 한 번 바뀌는데 이 목록을 쓰는 화면이 셋이다(알림함 ·
/// 발송 이력 · 메시지 발송). 화면을 열 때마다 받으면 왕복만 는다.
/// <b>모두가 나눠 쓴다</b>(<see cref="ReferenceData.SharedAsync"/>) —
/// <c>GET /auth/system/common-code/{groupCode}</c> 는 지금 요청의 신원을
/// 보지 않는다(장례식장의 <c>CommonCodeClient</c> 와 같은 근거).
/// </para>
/// </remarks>
public sealed class PushCategoryClient(GatewayClient gateway, ReferenceData data)
{
    /// <summary>공통코드 묶음 코드. 서버 쪽 정본은 <c>JSini.Shared.DTOs.PushCategories</c> 다.</summary>
    public const string GroupCode = "NOTI_CATEGORY";

    /// <summary>
    /// 사람이 손으로 보내는 알림의 갈래. <b>「메시지 발송」의 기본값</b>이다.
    /// </summary>
    /// <remarks>
    /// 공통코드에서 이 값을 지우면 고르개에 안 뜨지만, 그때도 보낸 것은
    /// 이 값으로 남는다(고르지 않고 보낼 수 있는 자리라 바닥값이 하나 필요하다).
    /// 서버 쪽 정본은 <c>JSini.Shared.DTOs.PushCategories.Notice</c> 다.
    /// </remarks>
    public const string Notice = "NOTICE";

    /// <summary>
    /// <b>구분이 안 붙은 줄</b>만 보는 조회 조건. 저장되는 값이 아니다 —
    /// 서버의 <c>PushCategories.Unset</c> 과 같은 글자여야 한다.
    /// </summary>
    /// <remarks>
    /// 구분을 붙이기 전에 쌓인 기록이 그렇게 남아 있고, 앞으로 구분을 빠뜨린
    /// 발송도 여기 모인다 — <b>어느 발송 자리가 빠뜨렸는지 찾는 자리</b>다.
    /// </remarks>
    public const string Unset = "__unset__";

    /// <summary>참조자료 통 안에서의 묶음 이름.</summary>
    private const string CacheGroup = "admin.push-category";

    /// <summary>
    /// 쓰는 갈래들. 중지된 것(<c>status = 0</c>)은 뺀다.
    /// </summary>
    /// <remarks>
    /// <b><c>hierarchical</c> 을 반드시 싣는다.</b> 서버가 nullable 이 아닌
    /// 필수로 받아, 빠뜨리면 500 이 난다.
    /// </remarks>
    public async Task<IReadOnlyList<PushCategory>> GetAsync(CancellationToken ct = default)
    {
        var cached = await data.SharedAsync(CacheGroup, GroupCode, async () =>
        {
            try
            {
                var rows = await gateway.GetListAsync<PushCategory>(
                    $"auth/system/common-code/{GroupCode}?hierarchical=false", ct);

                return (IReadOnlyList<PushCategory>)
                    [.. rows.Where(c => c.Status == 1).OrderBy(c => c.SortOrder)];
            }
            catch (ApiException)
            {
                // 갈래를 못 읽었다고 화면을 세우지 않는다. 고르개에 「전체」만
                // 남고 조회는 그대로 된다.
                //
                // **null 을 돌려주면 통에 담기지 않는다** — 빈 목록을 담으면
                // 서버가 돌아와도 한동안 빈 고르개가 남는다.
                return null;
            }
        });

        return cached ?? [];
    }

    /// <summary>
    /// 조회 조건 고르개에 담을 항목들 — 「전체」 + 갈래들 + 「구분 없음」.
    /// </summary>
    /// <remarks>
    /// 「구분 없음」을 끝에 두는 것은 <b>그것이 자료의 갈래가 아니라 자료의
    /// 상태</b>이기 때문이다. 갈래 사이에 섞어 두면 공통코드에 그런 코드가
    /// 있는 것으로 읽힌다.
    /// </remarks>
    public async Task<IReadOnlyList<SchOption>> OptionsAsync(CancellationToken ct = default)
    {
        var codes = await GetAsync(ct);

        return
        [
            new SchOption(null, "전체"),
            .. codes.Select(c => new SchOption(c.CodeValue, c.CodeName)),
            new SchOption(Unset, "구분 없음"),
        ];
    }

    /// <summary>
    /// <b>보낼 때</b> 고르는 항목들 — 갈래만 담는다.
    /// </summary>
    /// <remarks>
    /// 「전체」도 「구분 없음」도 넣지 않는다. 그 둘은 <b>조회 조건</b>이고,
    /// 보내는 자리에서 고르면 구분 없는 줄을 일부러 만드는 셈이 된다.
    /// </remarks>
    public async Task<IReadOnlyList<SchOption>> SendOptionsAsync(CancellationToken ct = default)
        => [.. (await GetAsync(ct)).Select(c => new SchOption(c.CodeValue, c.CodeName))];

    /// <summary>
    /// 코드값 → 이름 옮기개. <b>이미 받아 둔 목록</b>으로 만든다 — 표가 줄마다
    /// 부르는 자리라 그때마다 통을 두드리게 두지 않는다.
    /// </summary>
    /// <remarks>
    /// 목록에 없는 값이면 <b>값을 그대로</b> 보여 준다. 빈칸으로 두면 구분이
    /// 없는 줄처럼 보이는데, 실제로는 공통코드에서 지워진 옛 갈래일 때가 많다.
    /// </remarks>
    public static Func<string?, string> Labeler(IReadOnlyList<PushCategory> codes)
    {
        var map = codes.ToDictionary(c => c.CodeValue, c => c.CodeName, StringComparer.OrdinalIgnoreCase);

        return value => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : map.GetValueOrDefault(value, value);
    }
}
