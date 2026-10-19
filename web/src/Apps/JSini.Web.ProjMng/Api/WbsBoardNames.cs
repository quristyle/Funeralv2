using JSini.Web.Components.Data;

namespace JSini.Web.ProjMng.Api;

/// <summary>원장에 적힌 담당자 아이디를 사람이 읽는 이름으로 바꾼다. 서버는 원장(projmng DB)과 계정(jsiniportal DB)을 조인할 수 없어 아이디로만 집계하고 이름은 여기서 붙인다. 못 찾으면 적힌 값을 그대로 둔다 — 미할당 같은 말로 덮으면 오타와 퇴사자를 가릴 수 없다.</summary>
public sealed class WbsBoardNames(BizOptions options, ReferenceData data)
{
    /// <summary>범용 셀렉트에 등록된 포털 계정 목록의 타입 이름.</summary>
    private const string BizType = "portal_account";

    /// <summary>참조자료 통 안에서의 묶음 이름. 이 모듈만 읽는다.</summary>
    public const string Group = "projmng.wbs-board-names";

    /// <summary>아이디 → 이름. 통은 ReferenceData 가 들고 있다 — 이 서비스는 scoped 라 업무를 넘나들면 사라지므로 접속마다 같은 목록을 읽게 된다. 대소문자를 가리지 않는다(사번과 아이디가 대소문자만 다를 수 있다).</summary>
    private async Task<IReadOnlyDictionary<string, string>> MapAsync(CancellationToken ct)
    {
        // 백엔드가 신원을 보고 거르는 목록이라 사람마다 따로 담는다. 섞이면 남의 목록이 보인다.
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

    /// <summary>줄마다 담당자·개발자 이름을 채운다. fill 은 한 줄에 대해 (아이디 → 이름) 함수로 칸을 채운다.</summary>
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
