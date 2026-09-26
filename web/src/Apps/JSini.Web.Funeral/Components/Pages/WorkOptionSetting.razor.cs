using Microsoft.AspNetCore.Components;
using JSini.Web.Funeral.Api;

namespace JSini.Web.Funeral.Components.Pages;

public partial class WorkOptionSetting
{
    [Inject] private FuneralApi Api { get; set; } = default!;

    private IReadOnlyList<EnvironmentSetting> _all = [];

    /// <summary>
    /// 서버가 준 묶음을 그대로 그린다.
    ///
    /// <b>화면이 묶음 이름을 열거하지 않는다.</b> 그 값은 DB 에 있고 언제든
    /// 늘고 바뀐다 — 열거하면 새 묶음이 조용히 사라진다. 실제로 그래서
    /// 설정 넷이 전부 안 보이고 있었다.
    /// </summary>
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
        // **화면 값을 먼저 바꾼다.** 서버 왕복을 기다리면 스위치가 눌린 뒤
        // 잠깐 제자리에 있어서 사용자가 한 번 더 누른다.
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
