using Microsoft.AspNetCore.Components;
using JSini.Web.Admin.Api;

namespace JSini.Web.Admin.Components.Pages;

public partial class SignupList
{
    [Inject] private SignupClient Api { get; set; } = default!;
    [Inject] private AdminClient AdminApi { get; set; } = default!;

    private IReadOnlyList<SignupPendingDto> _pending = [];
    private IReadOnlyList<AccountDto> _accounts = [];

    private bool _rejecting;
    private SignupPendingDto? _target;
    private string? _reason;
    private bool _merging;
    private string? _targetAccountId;

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _pending = await Api.GetPendingAsync();
        return _pending.Count;
    }, "승인을 기다리는 신청이 없습니다.", "신청 목록을 읽지 못했습니다");

    private async Task ApproveAsync(SignupPendingDto row)
    {
        var ok = await RunAsync(
            () => Api.ApproveAsync(row.Id),
            $"{row.UserName} 님을 승인했습니다. 소속과 역할은 계정 관리에서 붙여 주십시오.",
            "승인하지 못했습니다");

        if (ok)
        {
            await ReloadAsync();

            // 승인 문구를 조회 안내가 덮는다 — 목록이 비면 LoadAsync 가 「없습니다」를 띄우므로 다시 세워 준다.
            Say($"{row.UserName} 님을 승인했습니다. 소속과 역할은 계정 관리에서 붙여 주십시오.");
        }
    }

    private Task AskRejectAsync(SignupPendingDto row)
    {
        _target = row;
        _reason = null;
        _rejecting = true;
        return Task.CompletedTask;
    }

    private async Task RejectAsync()
    {
        if (_target is null)
        {
            return;
        }

        var name = _target.UserName;

        var ok = await RunAsync(
            () => Api.RejectAsync(_target.Id, _reason),
            $"{name} 님의 신청을 거절했습니다.",
            "거절하지 못했습니다");

        _rejecting = false;

        if (ok)
        {
            await ReloadAsync();
            Say($"{name} 님의 신청을 거절했습니다.");
        }
    }

    private async Task AskMergeAsync(SignupPendingDto row)
    {
        _target = row;
        _targetAccountId = null;

        if (_accounts.Count == 0)
        {
            try
            {
                var all = await AdminApi.GetAccountsAsync();
                _accounts = all.Where(a => a.Status != "RESIGNED" && a.Id != row.Id && a.LoginId != row.LoginId).ToList();
            }
            catch
            {
                _accounts = [];
            }
        }

        // 신청자의 이메일 또는 이름과 일치하는 기존 계정이 있으면 자동 선택
        var matched = _accounts.FirstOrDefault(a =>
            (!string.IsNullOrWhiteSpace(row.Email) && (string.Equals(a.Email, row.Email, StringComparison.OrdinalIgnoreCase) || a.Emails.Any(e => string.Equals(e, row.Email, StringComparison.OrdinalIgnoreCase))))
            || (!string.IsNullOrWhiteSpace(row.UserName) && string.Equals(a.UserName, row.UserName, StringComparison.OrdinalIgnoreCase)));

        if (matched is not null)
        {
            _targetAccountId = matched.LoginId;
        }

        _merging = true;
    }

    private async Task MergeAsync()
    {
        if (_target is null || string.IsNullOrWhiteSpace(_targetAccountId))
        {
            return;
        }

        var targetId = _targetAccountId.Trim();
        var name = _target.UserName;

        var ok = await RunAsync(
            () => Api.MergeAsync(_target.Id, targetId),
            $"{name} 님의 소셜 연결을 결합했습니다.",
            "결합하지 못했습니다");

        _merging = false;

        if (ok)
        {
            await ReloadAsync();
            Say($"{name} 님의 소셜 연결을 결합했습니다.");
        }
    }
}
