using JSini.Web.Components.Data;

namespace JSini.Web.ProjMng.Api;

/// <summary>
/// 원장에 적힌 담당자 아이디를 <b>사람이 읽는 이름</b>으로 바꾼다.
/// </summary>
/// <remarks>
/// <para>
/// [서버가 이름을 못 붙인다]
/// </para>
///
/// <para>
/// 한동안 서버가 <c>projmng.wbs_user</c>(개발자 명부)를 조인해 이름을 실어
/// 보냈다. 그 명부를 걷어내고 사람을 포털 계정으로 다루기로 하면서
/// (<c>docs/projmng-account-merge.md</c>) 그 길이 끊겼다 — 원장은
/// <c>projmng</c> DB 에 있고 계정은 <c>jsiniportal</c> DB 에 있어서
/// <b>SQL 조인이 아예 불가능하다</b>(같은 PostgreSQL 인스턴스지만
/// 데이터베이스가 다르다).
/// </para>
///
/// <para>
/// 그래서 역할을 나눴다 — <b>서버는 아이디로만 집계하고 이름은 화면이 붙인다.</b>
/// </para>
///
/// <para>
/// [화면 일곱이 아니라 여기 한 곳이다]
/// </para>
///
/// <para>
/// 이름을 쓰는 자리가 일곱 화면에 흩어져 있다(요약 · 사람별 · 진척률 ·
/// 지연 · 상세 목록 …). 화면마다 붙이면 「어떤 화면은 이름, 어떤 화면은
/// 아이디」로 갈리므로 <see cref="WbsBoardClient"/> 가 응답을 돌려주기 전에
/// 여기서 한 번 채운다.
/// </para>
///
/// <para>
/// [못 찾으면 저장된 값 그대로 둔다]
/// </para>
///
/// <para>
/// 계정과 안 이어진 사람(퇴사자 · 외부 인력 · 오타)은 이름이 없다. 그때
/// <c>미할당</c> 같은 말로 덮으면 <b>오타인지 퇴사자인지 가려낼 수 없다</b> —
/// 적힌 값을 그대로 보여 준다.
/// </para>
/// </remarks>
public sealed class WbsBoardNames(BizOptions options, ReferenceData data)
{
    /// <summary>범용 셀렉트에 등록된 포털 계정 목록의 타입 이름.</summary>
    private const string BizType = "portal_account";

    /// <summary>참조자료 통 안에서의 묶음 이름. 이 모듈만 읽는다.</summary>
    public const string Group = "projmng.wbs-board-names";

    /// <summary>
    /// 아이디 → 이름.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>통은 <see cref="ReferenceData"/> 가 들고 있다.</b> 이 서비스는 scoped 라
    /// 업무를 넘나들면 통째로 사라지고(Piral 모듈 컨테이너가 갈린다), 그러면
    /// 접속자 수만큼 같은 목록을 읽게 된다 — <see cref="BizOptions"/> 가 자기
    /// 설정 캐시를 옮긴 것과 같은 이유다.
    /// </para>
    ///
    /// <para>
    /// 대소문자를 가리지 않는다. 옛 명부 조인이 <c>upper()</c> 로 맞췄고,
    /// 사내 자료의 사번과 포털 아이디가 대소문자만 다른 경우가 있다.
    /// </para>
    /// </remarks>
    private async Task<IReadOnlyDictionary<string, string>> MapAsync(CancellationToken ct)
    {
        // 백엔드가 신원을 보고 거를 수 있는 목록이라 **사람마다 따로** 담는다
        // (`ReferenceData.PerUserAsync` 머리말 — 틀렸을 때 나는 일이
        // 「남의 목록이 보이는 것」이다).
        var map = await data.PerUserAsync<IReadOnlyDictionary<string, string>>(
            Group, BizType,
            async () =>
            {
                var rows = await options.GetAsync(BizType, cancellationToken: ct);
                var built = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var row in rows)
                {
                    if (string.IsNullOrWhiteSpace(row.Value)) continue;
                    if (string.IsNullOrWhiteSpace(row.Label)) continue;

                    built[row.Value] = row.Label;
                }

                return built;
            });

        return map ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 줄마다 담당자·개발자 이름을 채운다.
    /// </summary>
    /// <param name="rows">채울 줄들.</param>
    /// <param name="fill">한 줄에 대해 (아이디 → 이름) 함수를 받아 칸을 채운다.</param>
    /// <param name="ct">취소 토큰</param>
    public async Task<IReadOnlyList<T>> FillAsync<T>(
        IReadOnlyList<T> rows,
        Action<T, Func<string?, string?>> fill,
        CancellationToken ct = default)
    {
        if (rows.Count == 0) return rows;

        var map = await MapAsync(ct);

        // 못 찾으면 적힌 값을 그대로 돌려준다(머리말).
        string? NameOf(string? id) =>
            string.IsNullOrWhiteSpace(id) ? id
            : map.TryGetValue(id, out var name) ? name
            : id;

        foreach (var row in rows) fill(row, NameOf);

        return rows;
    }
}
