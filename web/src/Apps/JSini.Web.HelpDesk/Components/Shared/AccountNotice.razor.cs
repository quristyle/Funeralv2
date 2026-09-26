using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using JSini.Web.HelpDesk.Api;

namespace JSini.Web.HelpDesk.Components.Shared;

public partial class AccountNotice
{
    [Inject] private HelpDeskContext HelpDesk { get; set; } = default!;

    [CascadingParameter] private Task<AuthenticationState>? AuthState { get; set; }

    private bool _show;
    private bool _admin;
    private string _message = string.Empty;
    private string _description = string.Empty;

    private string NoticeStyle => _admin
        ? "margin-bottom:12px; padding:10px 14px; border-radius:6px; border:1px solid #91caff; background:#e6f4ff; color:#003a8c;"
        : "margin-bottom:12px; padding:10px 14px; border-radius:6px; border:1px solid #ffe58f; background:#fffbe6; color:#613400;";

    protected override async Task OnInitializedAsync()
    {
        await HelpDesk.LoadIdentityAsync();

        _show = HelpDesk.IdentityChecked && !HelpDesk.IsLinked;
        if (!_show)
        {
            return;
        }

        var who = "현재";
        if (AuthState is not null)
        {
            var user = (await AuthState).User;
            who = user.Identity?.Name ?? "현재";
        }

        _admin = HelpDesk.IsUnlinkedAdmin;
        _message = _admin
            ? $"{who} 계정은 포털 관리자 역할로 헬프데스크를 조회·관리합니다."
            : $"{who} 계정에 연결된 헬프데스크 사용자가 없습니다.";
        _description = _admin
            ? "조회와 관리는 그대로 하실 수 있습니다. 다만 이 계정은 헬프데스크 담당자 레코드에 이어져 있지 않아, 나에게 배정된 요청·내가 쓴 댓글·알림 구독처럼 \"내 것\"을 가리키는 기능은 비어 있습니다. 필요하시면 헬프데스크 설정 › 계정 연결에서 담당자 레코드와 이어 주세요."
            : "헬프데스크 설정 › 계정 연결 화면에서 이 포털 계정을 헬프데스크 담당자 또는 고객 계정과 연결해야 요청 데이터를 볼 수 있습니다.";
    }
}
