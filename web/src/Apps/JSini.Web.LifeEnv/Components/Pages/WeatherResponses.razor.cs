using Microsoft.AspNetCore.Components;
using JSini.Web.Http;
using JSini.Web.LifeEnv.Api;

namespace JSini.Web.LifeEnv.Components.Pages;

public partial class WeatherResponses
{
    [Inject] private LifeEnvClient Client { get; set; } = default!;

    private IReadOnlyList<WeatherStandard> _standards = [];
    private IReadOnlyList<WeatherResponseItem> _responses = [];

    private int _standardId;

    private WeatherStandard? Selected => _standards.FirstOrDefault(s => s.Id == _standardId);

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync(async () =>
        {
            _standards = await Client.GetStandardsAsync();

            // 첫 기준을 골라 둔다. 안 고르면 빈 표만 보이고 무엇을 해야 하는지
            // 알 수 없다.
            _standardId = _standards.FirstOrDefault()?.Id ?? 0;

            return _standards.Count;
        }, "등록된 판정 기준이 없습니다. 「날씨 기준 관리」에서 먼저 만드십시오.",
           "판정 기준을 읽지 못했습니다");

        if (_standardId != 0)
        {
            await ReloadAsync();
        }
    }

    private async Task OnStandardChangedAsync(int id)
    {
        _standardId = id;
        _responses = [];

        if (id != 0)
        {
            await ReloadAsync();
        }
    }

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        if (_standardId == 0)
        {
            _responses = [];
            return 0;
        }

        var rows = await Client.GetResponsesByStandardAsync(_standardId);
        _responses = [.. rows.OrderBy(r => r.SortOrder)];

        return _responses.Count;
    }, "이 기준에 등록된 요령이 없습니다.", "대응 요령을 읽지 못했습니다");

    private void FillNew(WeatherResponseItem item)
    {
        // 고른 기준을 물려준다. 안 넣으면 어느 기준에도 속하지 않아
        // 저장돼도 목록에서 사라진다.
        item.WeatherStandardId = _standardId;
        item.SortOrder = _responses.Count == 0 ? 1 : _responses.Max(r => r.SortOrder) + 1;
    }

    private Task SaveAsync((WeatherResponseItem Item, bool IsNew) e)
    {
        if (e.Item.WeatherStandardId == 0)
        {
            throw new ApiException("판정 기준을 고르십시오.");
        }

        if (string.IsNullOrWhiteSpace(e.Item.ActionContent))
        {
            throw new ApiException("조치 내용을 넣으십시오.");
        }

        return e.IsNew
            ? Client.CreateResponseAsync(e.Item)
            : Client.UpdateResponseAsync(e.Item.Id, e.Item);
    }

    private Task DeleteAsync(WeatherResponseItem item) => Client.DeleteResponseAsync(item.Id);
}
