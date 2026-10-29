using JSini.Web.Admin.Api;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.Http;
using Microsoft.AspNetCore.Components;

namespace JSini.Web.Admin.Components.Pages;

public partial class ServerPingManage
{
    [Inject] private AdminClient Api { get; set; } = default!;

    private IReadOnlyList<ServerPingTargetDto> _all = [];
    private string? _keyword;
    private bool _activeOnly;
    private readonly HashSet<string> _testingIps = new(StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<ServerPingTargetDto> Shown
    {
        get
        {
            var list = _all.AsEnumerable();

            if (_activeOnly)
            {
                list = list.Where(x => x.IsActive);
            }

            if (!string.IsNullOrWhiteSpace(_keyword))
            {
                var k = _keyword.Trim();
                list = list.Where(x =>
                    x.Name.Contains(k, StringComparison.OrdinalIgnoreCase)
                    || x.Ip.Contains(k, StringComparison.OrdinalIgnoreCase)
                    || (x.Description?.Contains(k, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            return list.OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToList();
        }
    }

    protected override Task OnInitializedAsync() => ReloadAsync();

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        var items = await Api.GetServerPingTargetsAsync();
        _all = items ?? [];
        return _all.Count;
    }, "등록된 감시 대상 장비가 없습니다.", "장비 목록을 불러오지 못했습니다.");

    private Task ResetAsync()
    {
        _keyword = null;
        _activeOnly = false;
        return ReloadAsync();
    }

    private static void FillNew(ServerPingTargetDto item)
    {
        item.Name = string.Empty;
        item.Ip = string.Empty;
        item.TimeoutMs = 2000;
        item.IsActive = true;
        item.SortOrder = 0;
        item.Description = string.Empty;
    }

    private async Task SaveAsync((ServerPingTargetDto Item, bool IsNew) e)
    {
        if (string.IsNullOrWhiteSpace(e.Item.Name))
        {
            throw new ApiException("장비명을 입력해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(e.Item.Ip))
        {
            throw new ApiException("IP 주소를 입력해 주세요.");
        }

        if (e.IsNew)
        {
            var req = new CreateServerPingTargetDto
            {
                Name = e.Item.Name.Trim(),
                Ip = e.Item.Ip.Trim(),
                TimeoutMs = e.Item.TimeoutMs > 0 ? e.Item.TimeoutMs : 2000,
                IsActive = e.Item.IsActive,
                SortOrder = e.Item.SortOrder,
                Description = e.Item.Description?.Trim(),
            };
            var res = await Api.CreateServerPingTargetAsync(req);
            if (res == null) throw new ApiException("장비 등록에 실패했습니다.");
            Say($"'{e.Item.Name}' 장비가 등록되었습니다.");
        }
        else
        {
            var req = new UpdateServerPingTargetDto
            {
                Name = e.Item.Name.Trim(),
                Ip = e.Item.Ip.Trim(),
                TimeoutMs = e.Item.TimeoutMs > 0 ? e.Item.TimeoutMs : 2000,
                IsActive = e.Item.IsActive,
                SortOrder = e.Item.SortOrder,
                Description = e.Item.Description?.Trim(),
            };
            var res = await Api.UpdateServerPingTargetAsync(e.Item.Id, req);
            if (res == null) throw new ApiException("장비 수정에 실패했습니다.");
            Say($"'{e.Item.Name}' 장비 정보가 수정되었습니다.");
        }
    }

    private async Task DeleteAsync(ServerPingTargetDto item)
    {
        await Api.DeleteServerPingTargetAsync(item.Id);
        Say($"'{item.Name}' 장비가 삭제되었습니다.");
    }

    private async Task TestPingAsync(ServerPingTargetDto target)
    {
        if (_testingIps.Contains(target.Ip)) return;
        _testingIps.Add(target.Ip);
        StateHasChanged();

        try
        {
            var res = await Api.GetServerPingStatusAsync(target.Ip, target.TimeoutMs);
            if (res?.IsAlive == true)
            {
                Say($"[{target.Name} ({target.Ip})] 응답 성공 — 왕복 시간 {res.RoundtripTimeMs}ms");
            }
            else
            {
                var msg = res?.Message ?? "응답 없음 (시간 초과)";
                Say($"[{target.Name} ({target.Ip})] 응답 실패 — {msg}", NoticeTone.Warning);
            }
        }
        catch (Exception ex)
        {
            Say($"[{target.Name} ({target.Ip})] Ping 시험 오류: {ex.Message}", NoticeTone.Error);
        }
        finally
        {
            _testingIps.Remove(target.Ip);
            StateHasChanged();
        }
    }

    private void GoToMonitor()
    {
        Navigation.NavigateTo("/admin/status/server-ping");
    }
}
