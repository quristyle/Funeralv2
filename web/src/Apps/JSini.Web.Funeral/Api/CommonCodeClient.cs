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

/// <summary>공통코드를 읽어 온다(AuthServer scom.common_codes). 포털관리와 같은 조회지만 업무 모듈끼리는 참조할 수 없어 복제했다. 프로젝트관리의 공통코드와는 다른 표다.
/// 캐시는 회로가 아니라 ReferenceData 에 둔다 — 회로마다 두면 업무를 넘나들 때 표가 사라진다.</summary>
public sealed class CommonCodeClient(GatewayClient gateway, ReferenceData data)
{
    /// <summary>참조자료 통 안에서의 묶음 이름. 코드를 고치는 화면이 이 이름으로 버린다.</summary>
    public const string Group = "funeral.common-code";

    /// <summary>한 묶음의 코드들(중지된 것은 뺀다). hierarchical 은 반드시 실어야 한다 — 서버가 필수로 받아 빠뜨리면 500 이 난다.</summary>
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
                // 코드를 못 읽어도 화면은 세우지 않는다(고르개만 빈다). 저장된 값은 코드값 그대로 보인다.
                // null 을 돌려줘야 통에 담기지 않아 다음에 다시 시도한다. 빈 목록을 담으면 TTL 동안 빈 고르개가 남는다.
                return null;
            }
        });

        return cached ?? [];
    }

    /// <summary>코드값 → 이름 표. 표에 없는 값은 그대로 보여 준다 — 빈칸이면 자료가 없는 것처럼 보인다.</summary>
    public async Task<Func<string?, string>> LabelerAsync(
        string groupCode, CancellationToken ct = default)
        => Labeler(await GetAsync(groupCode, ct));

    /// <summary>이미 받아 둔 목록으로 옮기개를 만든다. 목록과 옮기개를 나란히 부르면 둘 다 통을 지나쳐 두 번 받으므로, 조회는 목록만 받고 이것으로 옮기개를 만든다.</summary>
    public static Func<string?, string> Labeler(IReadOnlyList<CommonCode> codes)
    {
        var map = codes.ToDictionary(c => c.CodeValue, c => c.CodeName, StringComparer.OrdinalIgnoreCase);

        return value => string.IsNullOrEmpty(value)
            ? string.Empty
            : map.GetValueOrDefault(value, value);
    }
}
