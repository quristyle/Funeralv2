using Microsoft.AspNetCore.Components;
using JSini.Web.Http;
using JSini.Web.LifeEnv.Api;

namespace JSini.Web.LifeEnv.Components.Pages;

public partial class WeatherStandards
{
    [Inject] private LifeEnvClient Client { get; set; } = default!;

    private IReadOnlyList<WeatherStandard> _all = [];
    private string? _keyword;

    private static readonly object[] Categories =
    [
        new { Value = "WIND", Text = "풍속" },
        new { Value = "RAIN", Text = "강우" },
        new { Value = "SNOW", Text = "강설" },
        new { Value = "HEAT", Text = "폭염" },
        new { Value = "COLD", Text = "한파" },
        new { Value = "T1H", Text = "기온" },
        new { Value = "REH", Text = "습도" },
    ];

    private static readonly object[] Operators =
    [
        new { Value = "GE", Text = "이상 (≥)" },
        new { Value = "GT", Text = "초과 ( >)" },
        new { Value = "LE", Text = "이하 (≤)" },
        new { Value = "LT", Text = "미만 (<)" },
        new { Value = "EQ", Text = "같음 (=)" },
        new { Value = "BT", Text = "사이 (값1 ~ 값2)" },
        new { Value = "NB", Text = "사이 아님" },
        new { Value = "DGE", Text = "기준 대비 이상 하강/상승" },
        new { Value = "DLE", Text = "기준 대비 이하 하강/상승" },
    ];

    private static readonly object[] Statuses =
    [
        new { Value = "ALLOW", Text = "작업 가능" },
        new { Value = "CAUTION", Text = "주의" },
        new { Value = "RESTRICTED", Text = "작업 제한" },
        new { Value = "SUSPENDED", Text = "작업 중지" },
    ];

    private IReadOnlyList<WeatherStandard> Shown
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_keyword))
            {
                return _all;
            }

            var k = _keyword.Trim();
            return [.. _all.Where(i =>
                (i.Name?.Contains(k, StringComparison.OrdinalIgnoreCase) ?? false)
                || (i.ConditionText?.Contains(k, StringComparison.OrdinalIgnoreCase) ?? false)
                || (i.Category?.Contains(k, StringComparison.OrdinalIgnoreCase) ?? false))];
        }
    }

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        var list = await Client.GetStandardsAsync();
        _all = [.. list.OrderBy(x => x.SortOrder)];
        return _all.Count;
    }, "등록된 판정 기준이 없습니다.", "판정 기준을 읽지 못했습니다");

    private void FillNew(WeatherStandard std)
    {
        std.Category = "WIND";
        std.Operator = "GE";
        std.WorkStatus = "CAUTION";
        std.SortOrder = _all.Count == 0 ? 1 : _all.Max(x => x.SortOrder) + 1;
    }

    private Task SaveAsync((WeatherStandard Item, bool IsNew) e)
    {
        if (string.IsNullOrWhiteSpace(e.Item.Name))
        {
            throw new ApiException("명칭을 넣으십시오.");
        }

        if (e.Item.ThresholdValue is null)
        {
            throw new ApiException("임계값을 넣으십시오. 값이 없으면 판정이 돌지 않습니다.");
        }

        if (NeedsTwo(e.Item.Operator) && e.Item.ThresholdValue2 is null)
        {
            throw new ApiException("이 연산자는 값이 둘 필요합니다.");
        }

        return e.IsNew
            ? Client.CreateStandardAsync(e.Item)
            : Client.UpdateStandardAsync(e.Item.Id, e.Item);
    }

    private Task DeleteAsync(WeatherStandard std) => Client.DeleteStandardAsync(std.Id);

    /// <summary >값이 둘 필요한 연산자인가.</summary >
    private static bool NeedsTwo(string? op) =>
        op is "BT" or "NB" or "DGE" or "DLE";

    /// <summary >두 번째 값이 「범위의 끝」이 아니라 「차이」인 연산자인가.</summary >
    private static bool IsDiff(string? op) => op is "DGE" or "DLE";

    private static string ThresholdText(WeatherStandard row) => row.Operator switch
    {
        "BT" or "NB" => $"{row.ThresholdValue} ~ {row.ThresholdValue2}",
        "DGE" or "DLE" => $"기준 {row.ThresholdValue}, 차이 {row.ThresholdValue2}",
        _ => row.ThresholdValue?.ToString() ?? "-",
    };

    private static string CategoryLabel(string? category) => category switch
    {
        "WIND" => "풍속",
        "RAIN" => "강우",
        "SNOW" => "강설",
        "HEAT" => "폭염",
        "COLD" => "한파",
        "T1H" => "기온",
        "REH" => "습도",
        _ => category ?? string.Empty,
    };

    private static string OperatorLabel(string? op) => op switch
    {
        "GE" => "≥",
        "GT" => ">",
        "LE" => "≤",
        "LT" => "<",
        "EQ" => "=",
        "BT" => "사이",
        "NB" => "사이 아님",
        "DGE" => "차이 ≥",
        "DLE" => "차이 ≤",
        _ => op ?? string.Empty,
    };

    private static string StatusLabel(string? status) => status switch
    {
        "ALLOW" => "작업 가능",
        "CAUTION" => "주의",
        "RESTRICTED" => "작업 제한",
        "SUSPENDED" => "작업 중지",
        _ => status ?? "-",
    };

    private static string StatusClass(string? status) => status switch
    {
        "ALLOW" => "jsini-badge--on",
        "CAUTION" or "RESTRICTED" => "jsini-badge--warn",
        "SUSPENDED" => "jsini-badge--off",
        _ => "",
    };
}
