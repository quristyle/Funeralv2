using Microsoft.AspNetCore.Components;
using JSini.Web.Funeral.Api;

namespace JSini.Web.Funeral.Components.Pages;

public partial class MyInfoPage
{
    [Inject] private FuneralApi Api { get; set; } = default!;

    private MyInfo? _info;

    protected override Task OnInitializedAsync()
        => LoadOneAsync(() => Api.GetMyInfoAsync(), v => _info = v,
            "내 정보를 찾지 못했습니다.", "내 정보를 읽지 못했습니다");
}
