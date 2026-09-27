using System.Globalization;
using System.Text.Json;

namespace JSini.Web.HelpDesk.Api;

/// <summary>
/// 포털 회사와 헬프데스크 회사를 이관 시 보존한 원본 ID로 연결한다.
/// </summary>
public static class HelpdeskCompanyOptionMapper
{
    private const string LegacyCompanyRemarkPrefix = "helpdesk:company:";

    public static IReadOnlyList<BizOption> ForPortalCompanies(
        IReadOnlyList<BizOption> helpdeskOptions,
        IEnumerable<JsonElement> portalCompanies)
    {
        var helpdeskCompanyIds = portalCompanies
            .Select(company => BizOptionService.GetText(company, "remark"))
            .Select(ParseHelpdeskCompanyId)
            .Where(id => id.HasValue)
            .Select(id => id.GetValueOrDefault())
            .ToHashSet();

        return helpdeskOptions
            .Where(option =>
                int.TryParse(option.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                && helpdeskCompanyIds.Contains(id))
            .ToArray();
    }

    private static int? ParseHelpdeskCompanyId(string? remark)
    {
        var value = remark?.Trim();
        if (value is null
            || !value.StartsWith(LegacyCompanyRemarkPrefix, StringComparison.OrdinalIgnoreCase)
            || !int.TryParse(
                value[LegacyCompanyRemarkPrefix.Length..],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var id)
            || id <= 0)
        {
            return null;
        }

        return id;
    }
}
