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

/// <summary>알림구분을 공통코드에서 읽어 온다 — 묶음 <c>NOTI_CATEGORY</c>.</summary>
public sealed class PushCategoryClient(GatewayClient gateway, ReferenceData data)
{
    /// <summary>공통코드 묶음 코드. 서버 쪽 정본은 <c>JSini.Shared.DTOs.PushCategories</c> 다.</summary>
    public const string GroupCode = "NOTI_CATEGORY";

    /// <summary>사람이 손으로 보내는 알림의 갈래. 「메시지 발송」의 기본값이다.</summary>
    public const string Notice = "NOTICE";

    /// <summary>구분이 안 붙은 줄만 보는 조회 조건. 저장되는 값이 아니다 — 서버의 <c>PushCategories.Unset</c> 과 같은 글자여야 한다.</summary>
    public const string Unset = "__unset__";

    /// <summary>참조자료 통 안에서의 묶음 이름.</summary>
    private const string CacheGroup = "admin.push-category";

    /// <summary>쓰는 갈래들. 중지된 것(<c>status = 0</c>)은 뺀다.</summary>
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
                // 갈래를 못 읽었다고 화면을 세우지 않는다. 고르개에 「전체」만 남고 조회는 그대로 된다.
                return null;
            }
        });

        return cached ?? [];
    }

    /// <summary>조회 조건 고르개에 담을 항목들 — 「전체」 + 갈래들 + 「구분 없음」.</summary>
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

    /// <summary>보낼 때 고르는 항목들 — 갈래만 담는다.</summary>
    public async Task<IReadOnlyList<SchOption>> SendOptionsAsync(CancellationToken ct = default)
        => [.. (await GetAsync(ct)).Select(c => new SchOption(c.CodeValue, c.CodeName))];

    /// <summary>코드값 → 이름 옮기개. 이미 받아 둔 목록으로 만든다 — 표가 줄마다 부르는 자리라 그때마다 통을 두드리게 두지 않는다.</summary>
    public static Func<string?, string> Labeler(IReadOnlyList<PushCategory> codes)
    {
        var map = codes.ToDictionary(c => c.CodeValue, c => c.CodeName, StringComparer.OrdinalIgnoreCase);

        return value => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : map.GetValueOrDefault(value, value);
    }
}
