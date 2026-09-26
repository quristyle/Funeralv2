using Microsoft.AspNetCore.Components;
using JSini.Web.Http;
using JSini.Web.Components.Data;
using JSini.Web.Admin.Api;

namespace JSini.Web.Admin.Components.Pages;

public partial class UserList
{
    [Inject] private AdminClient Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        SchSummary.Or(_keyword),
        SchSummary.Or(_status));

    private IReadOnlyList<AccountDto> _all = [];
    private IReadOnlyList<RoleDto> _roles = [];
    private IReadOnlyList<DeptDto> _depts = [];

    private string? _keyword;
    private string? _status;

    /// <summary>방금 만든 계정의 첫 비밀번호를 띄우는 창.</summary>
    private bool _issuedVisible;
    private string? _issuedLoginId;
    private string? _issuedPassword;

    // PENDING 은 거르기에만 있고 편집 목록(StatusOptions)에는 없다. 승인은
    // [가입 신청] 화면이 하는 일이라, 여기서 손으로 PENDING 을 지정하면
    // 그 사람이 이유 없이 로그인하지 못한다.
    private static readonly string[] StatusFilters = ["PENDING", "ACTIVE", "LOCKED", "RESIGNED"];

    private static readonly object[] StatusOptions =
    [
        new { Value = "ACTIVE", Text = "정상" },
        new { Value = "LOCKED", Text = "잠금" },
        new { Value = "RESIGNED", Text = "퇴사" },
    ];

    /// <summary>거르기는 브라우저 쪽에서 한다. 서버에 검색 조건이 없다.</summary>
    private IReadOnlyList<AccountDto> Shown
    {
        get
        {
            IEnumerable<AccountDto> rows = _all;

            if (!string.IsNullOrWhiteSpace(_status))
            {
                rows = rows.Where(a => string.Equals(a.Status, _status, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(_keyword))
            {
                var k = _keyword.Trim();
                rows = rows.Where(a =>
                    a.LoginId.Contains(k, StringComparison.OrdinalIgnoreCase)
                    || a.UserName.Contains(k, StringComparison.OrdinalIgnoreCase)
                    || (a.Email?.Contains(k, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            return [.. rows];
        }
    }

    /// <summary>
    /// 화면을 열면 바로 읽는다. 역할·부서도 함께 받는다 — 조회 전에 바로
    /// 「등록」을 눌러도 편집 폼의 고르개가 비어 있으면 안 되기 때문이다.
    /// </summary>
    protected override Task OnInitializedAsync() => LoadAsync(async () =>
    {
        _roles = await Api.GetRolesAsync();
        _depts = Flatten(await Api.GetDeptsAsync());
        _all = await Api.GetAccountsAsync();

        return _all.Count;
    }, "계정이 없습니다.", "계정 목록을 읽지 못했습니다");

    /// <summary>「조회」와 저장·삭제 뒤가 부른다.</summary>
    /// <summary>
    /// 로그인 아이디로 찾는 알림 상태. <b>없을 수 있다</b> — 알림 서버를 못
    /// 읽었거나(그때는 통이 통째로 비어 있다) 그 사람이 설정을 저장한 적도
    /// 기기를 붙인 적도 없을 때다.
    /// </summary>
    private readonly Dictionary<string, OwnerNotificationStateDto> _notify =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>알림 쪽을 못 읽었나. 칸마다 「–」를 그리는 근거다.</summary>
    private bool _notifyFailed;

    private OwnerNotificationStateDto? State(AccountDto a)
    {
        if (_notifyFailed)
        {
            return null;
        }

        // **행이 없으면 기본값이다.** 없는 것을 「꺼짐」으로 그리면 반대로
        // 말하게 된다(머리말).
        return _notify.TryGetValue(a.LoginId, out var state)
            ? state
            : new OwnerNotificationStateDto { OwnerKey = a.LoginId };
    }

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        _all = await Api.GetAccountsAsync();

        await LoadNotifyAsync();

        return _all.Count;
    }, "조건에 맞는 계정이 없습니다.", "계정 목록을 읽지 못했습니다");

    /// <summary>
    /// 알림 상태를 따로 읽는다.
    ///
    /// <para>
    /// <b>여기서 실패해도 계정 목록은 뜬다.</b> 다른 서비스의 표라 같이 묶으면
    /// 알림 서버가 죽었을 때 계정 관리가 통째로 안 열린다 — 그 화면은 알림과
    /// 무관한 일(계정 등록·역할 배정)에도 쓰인다.
    /// </para>
    ///
    /// <para>
    /// 실패를 토스트로 말하지도 않는다. 계정을 고치러 온 사람에게는 상관없는
    /// 말이고, 못 읽었다는 것은 <b>그 칸의 「–」</b>가 이미 말한다.
    /// </para>
    /// </summary>
    private async Task LoadNotifyAsync()
    {
        _notify.Clear();
        _notifyFailed = false;

        try
        {
            foreach (var state in await Api.GetNotificationStatesAsync())
            {
                _notify[state.OwnerKey] = state;
            }
        }
        catch (ApiException)
        {
            _notifyFailed = true;
        }
    }

    /// <summary>
    /// 스위치 한 칸. <b>저장한 적이 없으면 흐리게</b> 그리고 기본값이라고
    /// 적는다 — 안 건드린 것과 켜 둔 것은 다른 일이다(머리말).
    /// </summary>
    private static RenderFragment Switch(
        OwnerNotificationStateDto? state, Func<OwnerNotificationStateDto, bool> pick) => builder =>
    {
        if (state is null)
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "class", "jsini-text-muted");
            builder.AddAttribute(2, "title", "알림 설정을 읽지 못했습니다.");
            builder.AddContent(3, "–");
            builder.CloseElement();
            return;
        }

        var on = pick(state);

        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "class",
            $"jsini-badge {(on ? "jsini-badge--on" : "jsini-badge--off")}{(state.Saved ? null : " ad-notify--default")}");
        builder.AddAttribute(2, "title", state.Saved
            ? $"본인이 정한 값입니다.{Stamp(state.UpdatedAt)}"
            : "아직 알림 설정을 저장한 적이 없어 기본값입니다.");
        builder.AddContent(3, on ? "받음" : "안 받음");
        builder.CloseElement();
    };

    private static string Stamp(DateTime? at) =>
        at is null ? string.Empty : $" (마지막 저장 {at.Value.ToLocalTime():yyyy-MM-dd HH:mm})";

    private static string LastSent(OwnerNotificationStateDto state) =>
        state.LastSentAt is null
            ? " 아직 보낸 적은 없습니다."
            : $" 마지막 발송 {state.LastSentAt.Value.ToLocalTime():yyyy-MM-dd HH:mm}.";

    /// <summary>조건을 비우고 전체를 다시 읽는다.</summary>
    private Task Reset()
    {
        _keyword = null;
        _status = null;
        return ReloadAsync();
    }

    private void FillNew(AccountDto a)
    {
        a.Status = "ACTIVE";
        a.BirthdayCelebrated = true;

        // 새 계정도 켜서 만든다. 기본값과 같은 자리다.
        a.Watermark = true;
        a.RoleIds = [];
    }

    /// <summary>
    /// 저장한다. 등록이면 서버가 발급한 첫 비밀번호를 받아 창에 띄운다 —
    /// <b>그 응답이 그 값을 볼 수 있는 유일한 자리다.</b>
    /// </summary>
    private async Task SaveAsync((AccountDto Item, bool IsNew) e)
    {
        var body = new SaveAccountDto
        {
            LoginId = e.Item.LoginId,
            UserName = e.Item.UserName,
            Email = e.Item.Email,
            Phone = e.Item.Phone,
            Status = e.Item.Status,
            DeptId = e.Item.DeptId,
            RoleIds = e.Item.RoleIds,
            BirthDate = e.Item.BirthDate,
            BirthDateIsLunar = e.Item.BirthDateIsLunar,
            BirthdayCelebrated = e.Item.BirthdayCelebrated,
            Watermark = e.Item.Watermark,

            // 편집 창이 사전을 제자리에서 고친다(`AccountDto.Dev`) — 여기서
            // 모으는 단계가 없다.
            DevAttributes = e.Item.DevAttributes,
        };

        if (!e.IsNew)
        {
            await Api.UpdateAccountAsync(e.Item.Id, body);
            return;
        }

        var created = await Api.CreateAccountAsync(body);

        // 값이 없으면 창을 띄우지 않는다 — 빈 칸을 보여 주면 「발급이 안 됐나」로
        // 읽힌다. 설정으로 고정 비밀번호를 쓰는 경우에도 그 값이 그대로 온다.
        if (!string.IsNullOrEmpty(created?.InitialPassword))
        {
            _issuedLoginId = created.LoginId;
            _issuedPassword = created.InitialPassword;
            _issuedVisible = true;
        }
    }

    private Task DeleteAsync(AccountDto a) => Api.DeleteAccountAsync(a.Id);

    /// <summary>부서는 나무로 온다. 고르개에는 펴서 준다.</summary>
    private static List<DeptDto> Flatten(IEnumerable<DeptDto> nodes)
    {
        var flat = new List<DeptDto>();

        void Walk(IEnumerable<DeptDto> items)
        {
            foreach (var item in items)
            {
                flat.Add(item);

                if (item.Children is { Count: > 0 } children)
                {
                    Walk(children);
                }
            }
        }

        Walk(nodes);
        return flat;
    }

    private static string StatusText(string status) => status?.ToUpperInvariant() switch
    {
        "ACTIVE" => "정상",
        "LOCKED" => "잠금",
        "RESIGNED" => "퇴사",
        "PENDING" => "승인 대기",
        _ => status ?? "-",
    };

    private static string StatusClass(string status) => status?.ToUpperInvariant() switch
    {
        "ACTIVE" => "jsini-badge--on",
        "LOCKED" or "PENDING" => "jsini-badge--warn",
        _ => "jsini-badge--off",
    };
}
