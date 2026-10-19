using Microsoft.AspNetCore.Components;
using JSini.Web.Funeral.Api;

namespace JSini.Web.Funeral.Components.Pages;

public partial class WorkOptionSetting
{
    [Inject] private FuneralApi Api { get; set; } = default!;

    private IReadOnlyList<EnvironmentSetting> _all = [];

    /// <summary>서버가 준 묶음을 그대로 그린다. 묶음 이름을 화면이 열거하면 DB 에 새로 생긴 묶음이 조용히 사라진다.</summary>
    private IEnumerable<IGrouping<string, EnvironmentSetting>> Groups =>
        _all.GroupBy(s => string.IsNullOrWhiteSpace(s.GroupName) ? "기타" : s.GroupName)
            .OrderBy(g => g.Key, StringComparer.Ordinal);

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _all = await Api.GetEnvironmentSettingsAsync();
        return _all.Count;
    }, "설정 항목이 없습니다.", "설정을 읽지 못했습니다");

    private async Task ToggleAsync(EnvironmentSetting setting, bool value)
    {
        // 화면 값을 먼저 바꾼다. 서버 왕복을 기다리면 스위치가 눌린 뒤 잠깐 제자리에 있어 한 번 더 누르게 된다.
        setting.Enabled = value;

        if (await RunAsync(() => Api.UpdateEnvironmentSettingAsync(setting.Code, value),
                $"{setting.Name} 을(를) {(value ? "켰" : "껐")}습니다.", "저장하지 못했습니다"))
        {
            setting.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            // 실패했으면 되돌린다 — 안 되돌리면 화면과 서버가 어긋난 채로 남는다.
            setting.Enabled = !value;
        }
    }
}
