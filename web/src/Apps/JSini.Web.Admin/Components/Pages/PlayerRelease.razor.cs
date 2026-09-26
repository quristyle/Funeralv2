using Microsoft.AspNetCore.Components;
using JSini.Web.Admin.Api;

namespace JSini.Web.Admin.Components.Pages;

public partial class PlayerRelease
{
    [Inject] private AdminClient Api { get; set; } = default!;

    private PlayerReleaseDto? _status;

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadOneAsync(
        () => Api.GetPlayerReleaseStatusAsync(), v => _status = v,
        "배포 상태를 받지 못했습니다.", "배포 상태를 읽지 못했습니다");
}
