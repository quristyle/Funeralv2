using Microsoft.AspNetCore.Components;
using JSini.Web.Models;
using JSini.Web.Components.Settings;
using JSini.Web.Admin.Api;

namespace JSini.Web.Admin.Components.Pages;

public partial class AccountAppPage
{
    [Inject] private AdminClient Api { get; set; } = default!;

    /// <summary>볼 사람의 로그인 아이디. 계정 관리 목록이 줄마다 실어 보낸다.</summary>
    [Parameter] public string LoginId { get; set; } = string.Empty;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => $"{LoginId} · 최근 {_days} 일";

    private static readonly int[] DayOptions = [7, 30, 90, 365];

    private int _days = 30;

    /// <summary>이미 읽어 둔 사람. 매개변수가 안 바뀌었으면 다시 읽지 않는다.</summary>
    private string? _loadedFor;

    private AccountAppStatusDto? _status;

    // 표 셋이 읽는 자리. **아직 안 읽었을 때 빈 목록**이어야 하므로 화면에서
    // 직접 `?? []` 를 적지 않는다 — 그 꼴은 대상 형이 없어 Razor 가 거절한다.
    private IReadOnlyList<PushDeviceDto> Devices => _status?.Devices ?? [];

    private IReadOnlyList<AppChannelStatDto> Channels => _status?.Channels ?? [];

    private IReadOnlyList<AppDeliveryRowDto> Deliveries => _status?.Deliveries ?? [];

    /// <summary>
    /// 앱으로 깔아 쓰는 기기 수.
    ///
    /// <para>
    /// <b>「모름」은 안 센다.</b> 옛 구독에는 실행 방식 칸이 비어 있는데, 그것을
    /// 설치로도 미설치로도 셀 수 없다 — 세면 어느 쪽이든 틀린 수가 된다.
    /// </para>
    /// </summary>
    private int InstalledDevices =>
        _status?.Devices.Count(d => PushDeviceLabel.Installed(d) is true) ?? 0;

    /// <summary>연달아 실패 중인 기기 수. 맨 위 안내가 이 값으로 갈린다.</summary>
    private int DeadDevices => _status?.Devices.Count(d => d.FailureCount > 0) ?? 0;

    /// <summary>
    /// 길 × 방향 한 칸. <b>없으면 0 짜리를 만들어 준다</b> — 서버가 빈 칸도
    /// 채워 보내지만, 응답을 못 받았을 때도 타일이 그려져야 한다.
    /// </summary>
    private AppChannelStatDto Stat(string channel, string direction) =>
        _status?.Channels.FirstOrDefault(c => c.Channel == channel && c.Direction == direction)
        ?? new AppChannelStatDto { Channel = channel, Direction = direction };

    /// <summary>
    /// 매개변수가 바뀌면 다시 읽는다.
    ///
    /// <para>
    /// 같은 사람으로 다시 들어온 것(기간 고르개를 만진 뒤 다시 그려지는 것)은
    /// 걸러 낸다 — 안 걸면 고른 기간이 매번 기본값으로 되돌아간다.
    /// </para>
    /// </summary>
    protected override async Task OnParametersSetAsync()
    {
        if (_loadedFor == LoginId)
        {
            return;
        }

        _loadedFor = LoginId;
        _days = 30;
        await ReloadAsync();
    }

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _status = await Api.GetAccountAppStatusAsync(LoginId, _days);

        // 건수를 세어 돌려주면 「없습니다」가 뜬다. 이 화면은 **기록이 없는
        // 것 자체가 답**인 경우가 많아(아예 보낸 적이 없다) 그것을 빈 결과로
        // 말하지 않는다 — 표마다 자기 자리에 빈 안내가 이미 있다.
        return _status is null ? 0 : -1;
    }, "그 계정의 앱 현황을 찾지 못했습니다.", "앱 현황을 읽지 못했습니다");

    /// <summary>
    /// 스위치 한 칸. <b>여기서는 「저장한 적 없음」을 흐리게 그리지 않는다</b> —
    /// 그 사실은 아래 「설정 저장」 칸이 한 번만 말한다. 칸마다 되풀이하면
    /// 여섯 칸이 전부 흐려져 무엇이 켜졌는지 안 보인다.
    /// </summary>
    private static RenderFragment Switch(bool on) => builder =>
    {
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "class", $"jsini-badge {(on ? "jsini-badge--on" : "jsini-badge--off")}");
        builder.AddContent(2, on ? "받음" : "안 받음");
        builder.CloseElement();
    };

    private static string ChannelText(string? channel) => channel switch
    {
        "push" => "앱 푸시",
        "email" => "메일",
        _ => channel ?? "-",
    };

    private static string DirectionText(string? direction) => direction switch
    {
        "received" => "수신",
        "sent" => "발신",
        _ => direction ?? "-",
    };

    private static string Stamp(DateTime? at) =>
        at is null ? "-" : at.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
}
