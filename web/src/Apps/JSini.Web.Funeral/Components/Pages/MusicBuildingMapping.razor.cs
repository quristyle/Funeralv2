using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.Funeral.Api;

namespace JSini.Web.Funeral.Components.Pages;

public partial class MusicBuildingMapping
{
    [Inject] private FuneralApi Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary =>
        SchSummary.NameOf(_musics, m => m.Id, m => m.Name, _musicId, "음원을 고르세요");

    private IReadOnlyList<MediaSource> _musics = [];
    private string? _musicId;
    private IReadOnlyList<BuildingMusicMapping> _mappings = [];

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync(async () =>
        {
            _musics = await Api.GetMediaSourcesAsync("AUDIO");

            // 첫 음원을 자동으로 골라 준다 — 빈 화면부터 보이면 무엇을 해야
            // 하는지 알 수 없다. 원본도 그랬다.
            _musicId ??= _musics.FirstOrDefault()?.Id;

            return _musics.Count;
        }, "등록된 음원이 없습니다.", "음원 목록을 읽지 못했습니다");

        if (_musicId is not null)
        {
            await LoadMappingAsync();
        }
    }

    /// <summary>배정을 한꺼번에 켜고 끈다. 저장은 따로 눌러야 한다.</summary>
    private void ToggleAll(bool on)
    {
        foreach (var mapping in _mappings)
        {
            mapping.Mapped = on;
        }
    }

    private Task LoadMappingAsync()
    {
        if (_musicId is null)
        {
            Say("음원을 먼저 고르십시오.", NoticeTone.Warning);
            return Task.CompletedTask;
        }

        return LoadAsync(async () =>
        {
            _mappings = await Api.GetBuildingsForMusicAsync(_musicId);
            return _mappings.Count;
        }, "건물이 없습니다.", "배정 현황을 읽지 못했습니다");
    }

    private async Task SaveAsync()
    {
        if (_musicId is null) return;

        var chosen = _mappings.Where(m => m.Mapped).Select(m => m.BuildingId).ToList();

        if (await RunAsync(() => Api.SaveBuildingsForMusicAsync(_musicId, chosen),
                $"건물 {chosen.Count}곳에 배정했습니다.", "배정을 저장하지 못했습니다"))
        {
            await LoadMappingAsync();
        }
    }
}
