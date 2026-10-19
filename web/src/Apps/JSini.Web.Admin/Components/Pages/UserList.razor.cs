using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using JSini.Web.Http;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.Admin.Api;

namespace JSini.Web.Admin.Components.Pages;

public partial class UserList
{
    [Inject] private AdminClient Api { get; set; } = default!;

    /// <summary>발급한 비밀번호를 클립보드로 옮길 때만 쓴다.</summary>
    [Inject] private IJSRuntime Js { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        SchSummary.Or(_keyword),
        SchSummary.Or(_status));

    private IReadOnlyList<AccountDto> _all = [];
    private IReadOnlyList<RoleDto> _roles = [];
    private IReadOnlyList<CompanyDto> _companies = [];
    private IReadOnlyList<DeptDto> _depts = [];
    private IReadOnlyList<DeptDto> _newAccountDepts = [];
    private readonly Dictionary<string, IReadOnlyList<DeptDto>> _deptsByCompany =
        new(StringComparer.Ordinal);
    private AccountDto? _editingAccount;
    private int _departmentLoadVersion;

    private string? _keyword;
    private string? _status;

    /// <summary>서버가 발급한 비밀번호를 띄우는 창. 등록과 초기화가 같이 쓴다 — 둘 다 지금 한 번만 보이는 평문이다.</summary>
    private bool _issuedVisible;
    private string? _issuedLoginId;
    private string? _issuedUserName;
    private string? _issuedPassword;

    /// <summary>초기화로 연 창인가. 머리말 한 줄만 이 값으로 갈린다.</summary>
    private bool _issuedIsReset;

    /// <summary>복사를 눌렀는가. 단추 글자가 「복사됨」으로 바뀐다.</summary>
    private bool _issuedCopied;

    /// <summary>창에 적을 「누구의 것인지」. 이름을 모르면 아이디만 적는다.</summary>
    private string IssuedWho => string.IsNullOrWhiteSpace(_issuedUserName)
        ? _issuedLoginId ?? string.Empty
        : $"{_issuedLoginId} ({_issuedUserName})";

    /// <summary>초기화를 묻는 창.</summary>
    private ConfirmDialog? _confirm;

    /// <summary>계정 결합 창이 열려 있는가.</summary>
    private bool _mergingVisible;
    private AccountDto? _mergeSource;
    private string? _mergeTargetAccountId;

    /// <summary>결합 대상 계정 후보 (원본 계정 제외).</summary>
    private IReadOnlyList<AccountDto> MergeTargetCandidates =>
        _all.Where(a => a.Id != _mergeSource?.Id && a.LoginId != _mergeSource?.LoginId).ToList();

    // PENDING 은 거르기에만 있다 — 승인은 가입 신청 화면이 하고, 여기서 손으로 지정하면 이유 없이 로그인을 못 한다.
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
                    || (a.Email?.Contains(k, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (a.Emails?.Any(e => e.Contains(k, StringComparison.OrdinalIgnoreCase)) ?? false)
                    || (a.Phone?.Contains(k, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (a.Phones?.Any(p => p.Contains(k, StringComparison.OrdinalIgnoreCase)) ?? false)
                    || (a.SocialProviderText?.Contains(k, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            return [.. rows];
        }
    }

    /// <summary>화면을 열면 바로 읽는다. 역할·부서도 함께 받아 편집 폼의 고르개가 비지 않게 한다.</summary>
    protected override Task OnInitializedAsync() => LoadAsync(async () =>
    {
        var rolesTask = Api.GetRolesAsync();
        var companiesTask = Api.GetCompaniesAsync();
        var deptsTask = Api.GetDeptsAsync();
        var accountsTask = Api.GetAccountsAsync();

        await Task.WhenAll(rolesTask, companiesTask, deptsTask, accountsTask);

        _roles = rolesTask.Result;
        _companies = companiesTask.Result;
        _newAccountDepts = Flatten(deptsTask.Result);
        _depts = _newAccountDepts;
        _all = accountsTask.Result;

        var companyIds = _all
            .Select(a => a.CompanyId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var companyDepartments = await Task.WhenAll(companyIds.Select(async id =>
            (CompanyId: id!, Departments: Flatten(await Api.GetDeptsAsync(id)))));

        _deptsByCompany.Clear();
        foreach (var (companyId, departments) in companyDepartments)
        {
            _deptsByCompany[companyId] = departments;
        }

        return _all.Count;
    }, "계정이 없습니다.", "계정 목록을 읽지 못했습니다");

    /// <summary>「조회」와 저장·삭제 뒤가 부른다.</summary>
    /// <summary>로그인 아이디로 찾는 알림 상태. 없을 수 있다 — 알림 서버를 못 읽었거나 설정·기기가 없을 때다.</summary>
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

        // 행이 없으면 기본값이다 — 없는 것을 「꺼짐」으로 그리면 반대로 말하게 된다.
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

    /// <summary>알림 상태를 따로 읽는다. 여기서 실패해도 계정 목록은 뜬다 — 알림 서버가 죽었다고 계정 관리가 안 열리면 안 된다. 실패는 토스트가 아니라 칸의 「–」가 말한다.</summary>
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

    /// <summary>스위치 한 칸. 저장한 적이 없으면 흐리게 그리고 기본값이라고 적는다 — 안 건드린 것과 켜 둔 것은 다르다.</summary>
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

    /// <summary>편집 창을 열 때 그 계정의 회사 부서를 표시한다.</summary>
    private void PrepareEdit(AccountDto account, bool isNew)
    {
        _departmentLoadVersion++;
        _editingAccount = isNew ? null : account;
        _depts = isNew
            ? _newAccountDepts
            : account.CompanyId is { } companyId
                && _deptsByCompany.TryGetValue(companyId, out var departments)
                    ? departments
                    : [];
    }

    /// <summary>회사를 바꾸면 이전 회사의 부서 선택을 버리고 새 부서 목록을 읽는다.</summary>
    private async Task CompanyChangedAsync(AccountDto account)
    {
        account.DeptId = null;
        var version = ++_departmentLoadVersion;
        var companyId = account.CompanyId;

        if (string.IsNullOrWhiteSpace(companyId))
        {
            _depts = [];
            return;
        }

        if (_deptsByCompany.TryGetValue(companyId, out var cached))
        {
            _depts = cached;
            return;
        }

        _depts = [];
        await LoadAsync(async () =>
        {
            var departments = Flatten(await Api.GetDeptsAsync(companyId));
            _deptsByCompany[companyId] = departments;

            if (version == _departmentLoadVersion
                && ReferenceEquals(_editingAccount, account)
                && string.Equals(account.CompanyId, companyId, StringComparison.Ordinal))
            {
                _depts = departments;
            }

            return -1;
        }, string.Empty, "부서 목록을 읽지 못했습니다");
    }

    /// <summary>저장한다. 등록이면 서버가 발급한 첫 비밀번호를 받아 창에 띄운다 — 그 응답이 그 값을 볼 수 있는 유일한 자리다.</summary>
    private async Task SaveAsync((AccountDto Item, bool IsNew) e)
    {
        var body = new SaveAccountDto
        {
            LoginId = e.Item.LoginId,
            UserName = e.Item.UserName,
            Email = e.Item.Email,
            Emails = e.Item.Emails,
            Phone = e.Item.Phone,
            Phones = e.Item.Phones,
            Status = e.Item.Status,
            CompanyId = e.Item.CompanyId,
            DeptId = e.Item.DeptId,
            RoleIds = e.Item.RoleIds,
            BirthDate = e.Item.BirthDate,
            BirthDateIsLunar = e.Item.BirthDateIsLunar,
            BirthdayCelebrated = e.Item.BirthdayCelebrated,
            Watermark = e.Item.Watermark,

            // 편집 창이 사전을 제자리에서 고친다 — 여기서 모으는 단계가 없다.
            DevAttributes = e.Item.DevAttributes,
        };

        if (!e.IsNew)
        {
            await Api.UpdateAccountAsync(e.Item.Id, body);
            return;
        }

        var created = await Api.CreateAccountAsync(body);

        // 값이 없으면 창을 띄우지 않는다 — 빈 칸은 「발급이 안 됐나」로 읽힌다.
        if (!string.IsNullOrEmpty(created?.InitialPassword))
        {
            ShowIssued(created.LoginId, created.UserName, created.InitialPassword, isReset: false);
        }
    }

    /// <summary>
    /// 계정을 삭제하기 전에 확인하고 지운다.
    /// </summary>
    private async Task ConfirmDeleteAsync(AccountDto a)
    {
        var who = string.IsNullOrWhiteSpace(a.UserName)
            ? a.LoginId
            : $"{a.UserName}({a.LoginId})";

        if (_confirm is null
            || !await _confirm.AskAsync(
                $"「{who}」 계정을 정말 삭제하시겠습니까?",
                title: "계정 삭제",
                confirmText: "삭제"))
        {
            return;
        }

        if (await RunAsync(() => Api.DeleteAccountAsync(a.Id), "계정을 삭제했습니다.", "계정을 삭제하지 못했습니다"))
        {
            await ReloadAsync();
        }
    }

    private Task DeleteAsync(AccountDto a) => ConfirmDeleteAsync(a);

    /// <summary>그 사람의 비밀번호를 초기화하고 서버가 발급한 값을 창에 띄운다. 되돌릴 수 없어 먼저 묻고, 묻는 말에 누구인지 적는다. 목록은 다시 읽지 않는다(바뀌는 것은 목록에 없다).</summary>
    private async Task ResetPasswordAsync(AccountDto a)
    {
        var who = string.IsNullOrWhiteSpace(a.UserName)
            ? a.LoginId
            : $"{a.UserName}({a.LoginId})";

        if (_confirm is null
            || !await _confirm.AskAsync(
                $"「{who}」 의 비밀번호를 새로 발급합니다. 지금 쓰던 비밀번호는 바로 못 쓰게 됩니다.",
                title: "비밀번호 초기화",
                confirmText: "초기화"))
        {
            return;
        }

        IssuedPasswordDto? issued = null;

        var ok = await RunAsync(
            async () => issued = await Api.ResetAccountPasswordAsync(a.Id),
            "비밀번호를 초기화했습니다.",
            "비밀번호를 초기화하지 못했습니다");

        // 성공했는데 값이 비어 있으면 창을 띄우지 않는다 — 등록과 같은 규칙이다.
        if (ok && !string.IsNullOrEmpty(issued?.Password))
        {
            ShowIssued(issued.LoginId, issued.UserName, issued.Password, isReset: true);
        }
    }

    /// <summary>발급받은 값을 창에 올린다. 등록과 초기화가 같이 쓴다.</summary>
    private void ShowIssued(string? loginId, string? userName, string password, bool isReset)
    {
        _issuedLoginId = loginId;
        _issuedUserName = userName;
        _issuedPassword = password;
        _issuedIsReset = isReset;

        // 앞서 띄운 창의 「복사됨」 표시를 지운다 — 안 지우면 복사한 적 없는 값을 복사한 것으로 안다.
        _issuedCopied = false;

        _issuedVisible = true;
    }

    /// <summary>발급한 비밀번호를 클립보드로 옮긴다. 실패해도 창은 그대로 둔다 — 칸의 값을 손으로 옮겨 적을 수 있다.</summary>
    private async Task CopyIssuedAsync()
    {
        if (string.IsNullOrEmpty(_issuedPassword))
        {
            return;
        }

        bool copied;

        try
        {
            copied = await Js.InvokeAsync<bool>("jsiniClipboard.copy", _issuedPassword);
        }
        catch (JSException)
        {
            copied = false;
        }

        _issuedCopied = copied;

        if (!copied)
        {
            Say("복사하지 못했습니다. 칸의 값을 직접 옮겨 적으십시오.", NoticeTone.Error);
        }
    }

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

    /// <summary>「얼마나 지났나」 한 마디. 시각 밑에 흐리게 덧붙인다. 「오늘·어제」는 한국 달력으로 센다 — UTC 를 그대로 빼면 오전 아홉 시 전까지 오늘 아침 것이 「어제」가 된다(docs/utc-time.md).</summary>
    private static string Ago(DateTime? utc)
    {
        if (utc is null) return string.Empty;

        var days = AppTime.Today.DayNumber - DateOnly.FromDateTime(AppTime.ToKorea(utc.Value)).DayNumber;

        return days switch
        {
            // 시계가 어긋나 미래로 적힌 줄은 아무 말도 하지 않는다.
            < 0 => string.Empty,
            0 => "오늘",
            1 => "어제",
            < 7 => $"{days}일 전",
            < 30 => $"{days / 7}주 전",
            < 365 => $"{days / 30}개월 전",
            _ => $"{days / 365}년 전",
        };
    }

    /// <summary>마지막 로그인 칸의 설명 풍선. 접속 IP 가 있으면 함께 적는다 — IP 는 칸이 늘면 가로로 더 구르므로 마우스를 올린 자리에 둔다.</summary>
    private static string LoginTitle(AccountDto a)
    {
        var at = $"마지막 로그인 {a.LastLoginAt.Kst("yyyy-MM-dd HH:mm:ss")} (한국 시각)";

        return string.IsNullOrWhiteSpace(a.LastLoginIp) ? at : $"{at}\n접속 IP {a.LastLoginIp}";
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

    private static string SocialBadgeClass(string provider) => provider?.ToLowerInvariant() switch
    {
        "kakao" => "jsini-badge--warn",
        "naver" => "jsini-badge--on",
        "google" => "jsini-badge--info",
        _ => "jsini-badge--muted",
    };

    private static string SocialProviderName(string provider) => provider?.ToLowerInvariant() switch
    {
        "kakao" => "카카오",
        "naver" => "네이버",
        "google" => "구글",
        _ => provider ?? "-",
    };

    /// <summary>기존 계정을 다른 계정으로 결합하기 위한 팝업을 연다.</summary>
    private void AskMergeAsync(AccountDto a)
    {
        _mergeSource = a;
        _mergeTargetAccountId = null;

        // 원본 계정과 이메일이나 이름이 일치하는 대상 계정이 있으면 자동 추천
        var matched = _all.FirstOrDefault(candidate =>
            candidate.Id != a.Id
            && candidate.LoginId != a.LoginId
            && ((!string.IsNullOrWhiteSpace(a.Email) && (string.Equals(candidate.Email, a.Email, StringComparison.OrdinalIgnoreCase) || candidate.Emails.Any(e => string.Equals(e, a.Email, StringComparison.OrdinalIgnoreCase))))
                || (!string.IsNullOrWhiteSpace(a.UserName) && string.Equals(candidate.UserName, a.UserName, StringComparison.OrdinalIgnoreCase))));

        if (matched is not null)
        {
            _mergeTargetAccountId = matched.LoginId;
        }

        _mergingVisible = true;
    }

    /// <summary>원본 계정을 대상 계정으로 결합한다.</summary>
    private async Task MergeAsync()
    {
        if (_mergeSource is null || string.IsNullOrWhiteSpace(_mergeTargetAccountId))
        {
            return;
        }

        var source = _mergeSource;
        var targetId = _mergeTargetAccountId.Trim();
        var name = string.IsNullOrWhiteSpace(source.UserName) ? source.LoginId : source.UserName;

        var ok = await RunAsync(
            () => Api.MergeAccountAsync(source.Id, targetId),
            $"{name} 님의 계정을 결합했습니다.",
            "계정을 결합하지 못했습니다");

        _mergingVisible = false;

        if (ok)
        {
            await ReloadAsync();
            Say($"{name} 님의 계정을 결합했습니다.");
        }
    }
}
