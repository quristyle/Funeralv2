using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.Http;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>진행 상태 고르개의 한 칸. DevExpress 는 칸 이름을 속성에서 찾으므로 튜플로 두면 안 된다.</summary>
public sealed record ScheduleState(string Code, string Name);

public partial class ScheduleEditPopup
{
    [Inject] private WbsClient Api { get; set; } = default!;

    [Inject] private Toasts Toasts { get; set; } = default!;

    /// <summary>구분이 빈 채로 저장할 때 붙일 구분. 비우면 어느 일정표 조건에도 안 걸리는 줄이 된다.</summary>
    [Parameter] public string DefaultKind { get; set; } = "Public";

    /// <summary>저장·복제·삭제가 서버에 들어간 뒤. 화면은 다시 읽기만 하면 된다.</summary>
    [Parameter] public EventCallback Changed { get; set; }

    internal static readonly ScheduleState[] StateOptions =
    [
        new("READY", "대기"),
        new("RUNNING", "진행중"),
        new("COMP", "완료"),
        new("DISCARD", "폐기"),
    ];

    private bool _visible;
    private bool _isNew;
    private bool _busy;
    private string _formState = "READY";
    private WbsItemDto _form = new();
    private ConfirmDialog? _confirm;

    /// <summary>창이 떠 있는가. 떠 있는 동안 화면의 Delete 글쇠는 창의 일이라 받지 않는다.</summary>
    public bool IsOpen => _visible;

    /// <summary>새 일정. 기본값(프로젝트·구분·기간·담당자)은 부르는 화면이 채워 넘긴다.</summary>
    public void OpenNew(WbsItemDto seed)
    {
        _form = seed;
        _formState = "READY";
        _isNew = true;
        _visible = true;
        StateHasChanged();
    }

    /// <summary>있는 일정. 복사본을 띄운다 — 목록 줄을 그대로 묶으면 취소해도 화면에 바뀐 값이 남는다.</summary>
    public void OpenEdit(WbsItemDto item)
    {
        _form = Copy(item);
        _formState = StateCode(item);
        _isNew = false;
        _visible = true;
        StateHasChanged();
    }

    private void Close() => _visible = false;

    private void OnFormStateChanged(string? state)
    {
        if (state is "READY" or "RUNNING" or "COMP" or "DISCARD")
        {
            _formState = state;
        }
    }

    /// <summary>줄의 진행 상태. 폐기가 먼저고, 개발 종료일이 있으면 완료, 시작일만 있으면 진행중이다.</summary>
    internal static string StateCode(WbsItemDto w)
    {
        if (w.Discarded == true || w.WbsState == "DISCARD") return "DISCARD";
        if (w.DevEdt is not null || w.WbsState == "COMP") return "COMP";
        if (w.DevSdt is not null || w.WbsState == "RUNNING") return "RUNNING";
        return "READY";
    }

    /// <summary>고른 상태를 서버가 판정하는 폐기 여부와 개발 시작·종료일로 옮긴다.</summary>
    private void ApplyFormState() => ApplyState(_form, _formState, AppTime.TodayDate);

    /// <summary>
    /// 상태 하나를 줄에 옮긴다. 폐기는 개발 날짜를 그대로 둔다 — 폐기를 되돌리면 그 전 상태로 돌아가야 한다.
    /// </summary>
    internal static void ApplyState(WbsItemDto form, string state, DateTime today)
    {
        form.Discarded = state == "DISCARD";
        form.WbsState = state;

        switch (state)
        {
            case "DISCARD":
                break;
            case "RUNNING":
                form.DevSdt ??= today;
                form.DevEdt = null;
                break;
            case "COMP":
                form.DevSdt ??= today;
                form.DevEdt ??= today;
                if (form.PlanSdt > form.DevEdt) form.PlanSdt = form.DevEdt;
                if (form.PlanEdt > form.DevEdt) form.PlanEdt = form.DevEdt;
                break;
            default:
                form.DevSdt = null;
                form.DevEdt = null;
                break;
        }
    }

    /// <summary>저장·복제가 함께 거치는 검사. 통과하면 구분과 상태를 채워 둔다.</summary>
    private bool Validate()
    {
        if (string.IsNullOrWhiteSpace(_form.ProcNm))
        {
            Toasts.Show("일정 이름을 넣으십시오.", NoticeTone.Warning);
            return false;
        }

        if (_form.PrjRid is null)
        {
            Toasts.Show("프로젝트를 고르십시오.", NoticeTone.Warning);
            return false;
        }

        if (_form.PlanEdt < _form.PlanSdt)
        {
            Toasts.Show("종료일이 시작일보다 앞섭니다.", NoticeTone.Warning);
            return false;
        }

        if (string.IsNullOrWhiteSpace(_form.ScheduleType))
        {
            _form.ScheduleType = DefaultKind;
        }

        ApplyFormState();
        return true;
    }

    /// <param name="moveToToday">원본의 「오늘로 옮겨 저장」. 기간 길이와 시작 시각은 두고 오늘로 옮긴다.</param>
    private async Task SaveAsync(bool moveToToday)
    {
        if (moveToToday)
        {
            var today = AppTime.TodayDate;
            var diff = _form.PlanEdt.HasValue && _form.PlanSdt.HasValue
                ? _form.PlanEdt.Value - _form.PlanSdt.Value
                : TimeSpan.Zero;
            var startTime = _form.PlanSdt?.TimeOfDay ?? TimeSpan.Zero;
            _form.PlanSdt = today.Add(startTime);
            _form.PlanEdt = _form.PlanSdt.Value.Add(diff);
        }

        if (!Validate())
        {
            return;
        }

        var isNew = _isNew;

        if (await RunAsync(
                () => isNew ? Api.CreateAsync(_form) : Api.UpdateAsync(_form),
                isNew ? "등록했습니다." : "저장했습니다.",
                isNew ? "등록하지 못했습니다" : "저장하지 못했습니다"))
        {
            _visible = false;
            await Changed.InvokeAsync();
        }
    }

    /// <summary>현재 보여지는 정보를 저장하고 동일한 내용(메모·원문 포함)으로 복제하여 팝업을 복제된 일정으로 이어 간다.</summary>
    private async Task DuplicateAsync()
    {
        if (!Validate())
        {
            return;
        }

        WbsItemDto? created = null;
        WbsItemDto? clone = null;

        if (await RunAsync(async () =>
            {
                // 1. 현재 보여지는 정보 저장
                if (_isNew)
                {
                    var saved = await Api.CreateAsync(_form);
                    if (saved is not null)
                    {
                        _form = Copy(saved);
                        _isNew = false;
                    }
                }
                else
                {
                    var saved = await Api.UpdateAsync(_form);
                    if (saved is not null)
                    {
                        _form = Copy(saved);
                    }
                }

                // 2. 현재 정보와 동일한 내용으로 복제 (메모, 원문 포함)
                clone = Copy(_form);
                clone.WbsId = 0;

                created = await Api.CreateAsync(clone);
            }, "복제했습니다.", "복제하지 못했습니다"))
        {
            // 3. 복제된 내용이 팝업에 보여지도록 갱신
            _form = created is not null ? Copy(created) : (clone ?? _form);
            _formState = StateCode(_form);
            _isNew = false;
            await Changed.InvokeAsync();
            StateHasChanged();
        }
    }

    private async Task DeleteAsync()
    {
        if (_isNew || _confirm is null)
        {
            return;
        }

        if (!await _confirm.AskAsync($"「{_form.ProcNm}」 을(를) 지웁니다.\n되돌릴 수 없습니다."))
        {
            return;
        }

        if (await RunAsync(() => Api.DeleteAsync(_form.WbsId), "지웠습니다.", "지우지 못했습니다"))
        {
            _visible = false;
            await Changed.InvokeAsync();
        }
    }

    private async Task<bool> RunAsync(Func<Task> action, string ok, string fail)
    {
        _busy = true;

        try
        {
            await action();
            Toasts.Show(ok);
            return true;
        }
        catch (ApiException ex)
        {
            Toasts.Show($"{fail} — {ex.Message}", NoticeTone.Error);
            return false;
        }
        finally
        {
            _busy = false;
        }
    }

    internal static WbsItemDto Copy(WbsItemDto w) => new()
    {
        WbsId = w.WbsId,
        PrjRid = w.PrjRid,
        ProcId = w.ProcId,
        Gb1 = w.Gb1,
        Gb2 = w.Gb2,
        ProcNm = w.ProcNm,
        ProcTp = w.ProcTp,
        ProcLvl = w.ProcLvl,
        BuildUser = w.BuildUser,
        BuildStatus = w.BuildStatus,
        DevUser = w.DevUser,
        ReqUser = w.ReqUser,
        PlanSdt = w.PlanSdt,
        PlanEdt = w.PlanEdt,
        DevSdt = w.DevSdt,
        DevEdt = w.DevEdt,
        DevChk = w.DevChk,
        BuildChk = w.BuildChk,
        BuildChkDt = w.BuildChkDt,
        QcUser = w.QcUser,
        QcChk = w.QcChk,
        QcChkDt = w.QcChkDt,
        Comm = w.Comm,
        Summary = w.Summary,
        BodyText = w.BodyText,
        ScheduleType = w.ScheduleType,
        AddTarget = w.AddTarget,
        Discarded = w.Discarded,
        WbsState = w.WbsState,
    };
}
