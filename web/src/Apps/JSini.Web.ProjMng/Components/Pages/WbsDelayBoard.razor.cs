using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.ProjMng.Api;
using JSini.Web.ProjMng.Components.Shared;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class WbsDelayBoard
{
    [Inject] private WbsBoardClient Api { get; set; } = default!;

    [Inject] private IJSRuntime Js { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        _projectName,
        WbsBoardOptions.TextOf(WbsBoardOptions.Scope, _scope));

    /// <summary>고른 프로젝트의 이름. 코드만 들고, 목록은 CodeSelect 가 쥐고 있다.</summary>
    private string? _projectName;

    private string? _projectCode;
    private string _scope = "dev";

    /// <summary>상세 목록의 지연 유형. 위의 요약·사람 표는 이것과 무관하게 늘 둘 다를 센다.</summary>
    private string _kind = "all";

    /// <summary>사람 표에서 누른 사람. 담당자와 개발자는 한쪽만 걸린다 — 둘을 겹치면 거의 늘 빈 목록이다.</summary>
    private string? _selUser;
    private string? _selReal;

    private WbsBoardDelayDto? _summary;
    private IReadOnlyList<WbsBoardDelayUserDto> _byPlan = [];
    private IReadOnlyList<WbsBoardDelayUserDto> _byReal = [];
    private IReadOnlyList<WbsBoardDelayRowDto> _rows = [];
    private string? _loadedAt;

    private bool _pvOpen;
    private string _pvScript = "";
    private int _pvSkipped;

    private int? ProjectRid => int.TryParse(_projectCode, out var rid) ? rid : null;

    private static readonly SchOption[] KindOptions =
    [
        new("all", "착수 + 종료 지연"),
        new("start", "착수지연만"),
        new("finish", "종료지연만"),
    ];

    /// <summary>지연이 있는 사람만. 서버는 사번 순으로 주는데 이름은 여기서 채워지므로 이름 순은 여기서 다시 세운다.</summary>
    private IReadOnlyList<WbsBoardDelayUserDto> PlanLate => LateOnly(_byPlan);

    /// <inheritdoc cref="PlanLate"/>
    private IReadOnlyList<WbsBoardDelayUserDto> RealLate => LateOnly(_byReal);

    private static List<WbsBoardDelayUserDto> LateOnly(IEnumerable<WbsBoardDelayUserDto> users) =>
        [.. users.Where(u => u.AnyLate > 0)
                 .OrderByDescending(u => u.AnyLate)
                 .ThenBy(u => u.UserNm ?? u.UserBpId, StringComparer.CurrentCulture)];

    private bool HasSel => _selUser is not null || _selReal is not null || _kind != "all";

    private string? FilterText =>
        _selUser is not null ? $"담당자 {NameOf(_byPlan, _selUser)} ({_selUser})"
        : _selReal is not null ? $"개발자 {NameOf(_byReal, _selReal)} ({_selReal})"
        : null;

    private static string? NameOf(IEnumerable<WbsBoardDelayUserDto> users, string id) =>
        users.FirstOrDefault(u => u.UserBpId == id)?.UserNm;

    /// <summary>실적시작일을 채워 보낼 줄 — 계획시작일이 지났는데 실적시작일이 빈(착수지연) 것. 종료지연만 걸린 줄은 채울 값이 없다.</summary>
    private IReadOnlyList<WbsBoardDelayRowDto> PvRows =>
        [.. _rows.Where(r => HasActivity(r) && r.PlanSdt is not null && r.PlanSdtC is null)];

    private static bool HasActivity(WbsBoardDelayRowDto r) =>
        !string.IsNullOrWhiteSpace(r.ActivityId)
        && !r.ActivityId.StartsWith(WbsBoardLinks.NoIdPrefix, StringComparison.OrdinalIgnoreCase);

    private string PvMeta =>
        $"{PvRows.Count - _pvSkipped}건 · 계획시작일 → 실적시작일"
        + (_pvSkipped > 0 ? $" · ProjectView 에 이미 있어 제외 {_pvSkipped}건" : "");

    /// <summary>ProjectView 작업 상세. 코드 앞자리가 projectKey 다(AAC-A1047 → AAC).</summary>
    private static string? PvWorkUrl(WbsBoardDelayRowDto r) =>
        HasActivity(r)
            ? $"{WbsBoardLinks.PvBase}/#/PGE_PSW_WorkDetailPage?projectKey={Uri.EscapeDataString(r.ActivityId!.Split('-')[0])}&activityId={Uri.EscapeDataString(r.ActivityId)}"
            : null;

    private Task SearchAsync() => LoadAsync(async () =>
    {
        if (ProjectRid is not int rid)
        {
            _summary = null;
            _byPlan = [];
            _byReal = [];
            _rows = [];
            return 0;
        }

        var summary = Api.DelayAsync(rid, _scope);
        var plan = Api.DelayByUserAsync(rid, _scope, "plan");
        var real = Api.DelayByUserAsync(rid, _scope, "real");
        var rows = ReadRowsAsync(rid);

        await Task.WhenAll(summary, plan, real, rows);

        _summary = summary.Result;
        _byPlan = plan.Result;
        _byReal = real.Result;
        _rows = rows.Result;
        _loadedAt = AppTime.UtcNow.Kst("HH:mm:ss");

        return _rows.Count;
    }, "지연 항목이 없습니다.", "지연 현황을 읽지 못했습니다");

    /// <summary>상세 목록만 다시 읽는다. 사람·유형을 바꿀 때 위의 숫자까지 다시 읽을 까닭이 없다.</summary>
    private Task LoadRowsAsync() => LoadAsync(async () =>
    {
        if (ProjectRid is not int rid)
        {
            _rows = [];
            return 0;
        }

        _rows = await ReadRowsAsync(rid);
        return _rows.Count;
    }, "지연 항목이 없습니다.", "지연 상세 목록을 읽지 못했습니다");

    private Task<IReadOnlyList<WbsBoardDelayRowDto>> ReadRowsAsync(int rid) =>
        Api.DelayRowsAsync(rid, _scope, _kind, _selUser, _selReal);

    private Task SetKindAsync(string kind)
    {
        _kind = kind;
        return LoadRowsAsync();
    }

    private Task PickUserAsync(WbsBoardDelayUserDto u)
    {
        _selUser = _selUser == u.UserBpId ? null : u.UserBpId;
        _selReal = null;
        return LoadRowsAsync();
    }

    private Task PickRealAsync(WbsBoardDelayUserDto u)
    {
        _selReal = _selReal == u.UserBpId ? null : u.UserBpId;
        _selUser = null;
        return LoadRowsAsync();
    }

    private Task ClearSelAsync()
    {
        _selUser = null;
        _selReal = null;
        _kind = "all";
        return LoadRowsAsync();
    }

    /// <summary>그 사람의 지연 건으로 좁힌 [상세 목록]을 연다.</summary>
    private void Drill(string? user, string? realUser)
    {
        var query = new List<string>
        {
            // 갈래 이름이 목록과 다르다 — 여기서 all 은 「하나라도」인데 목록의 지연 필터는 any 다.
            $"late={(_kind == "all" ? "any" : _kind)}",
            $"scope={Uri.EscapeDataString(_scope)}",
        };

        if (!string.IsNullOrWhiteSpace(user)) query.Add($"user={Uri.EscapeDataString(user)}");
        if (!string.IsNullOrWhiteSpace(realUser)) query.Add($"realUser={Uri.EscapeDataString(realUser)}");

        Navigation.NavigateTo($"/projmng/wbs/rows?{string.Join('&', query)}");
    }

    private void DrillCurrent() => Drill(_selUser, _selReal);

    private void OpenPvScript()
    {
        var all = PvRows;

        // ProjectView 에 이미 실적시작일이 있는 건은 스크립트가 어차피 건너뛴다 — 캐시로 알 수 있으면 조회조차 하지 않게 미리 뺀다.
        var items = all.Where(r => r.PvActualSdt is null)
                       .Select(r => new WbsPvDateScript.Item(r.ActivityId!, r.PlanSdt!))
                       .ToList();
        _pvSkipped = all.Count - items.Count;

        var ids = all.Where(r => !string.IsNullOrWhiteSpace(r.PvWorkId))
                     .GroupBy(r => r.ActivityId!)
                     .ToDictionary(g => g.Key, g => g.First().PvWorkId!);
        var projectId = all.Select(r => r.PvProjectId).FirstOrDefault(p => !string.IsNullOrWhiteSpace(p));

        _pvScript = WbsPvDateScript.Build(items, "계획시작일 → 실적시작일(actualStartDate)", ids, projectId);
        _pvOpen = true;
    }

    private async Task CopyPvScriptAsync()
    {
        var copied = await Js.InvokeAsync<bool>("jsiniClipboard.copy", _pvScript);

        Say(copied
            ? "복사했습니다. ProjectView 탭의 F12 → Console 에 붙여넣으세요."
            : "자동 복사가 막혔습니다. 스크립트 칸을 눌러 Ctrl+A · Ctrl+C 로 복사하세요.",
            copied ? NoticeTone.Info : NoticeTone.Warning);
    }
}
