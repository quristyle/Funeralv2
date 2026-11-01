using System.Globalization;
using Microsoft.AspNetCore.Components;
using JSini.Web.Abstractions;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class InterfaceList
{
    [Inject] private InterfaceClient Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(
        _projectName,
        SchSummary.NameOf(StatusFilterOptions, o => o.Value, o => o.Text, _status),
        _domain,
        SchSummary.NameOf(UseOptions, o => o.Value, o => o.Text, _useYn),
        _q);

    /// <summary>고른 프로젝트의 이름. 코드만 들고, 목록은 CodeSelect 가 쥐고 있다.</summary>
    private string? _projectName;

    private string _mode = "list";

    private string? _projectCode;
    private string? _status;
    private string? _domain;
    private string? _useYn = "Y";
    private string? _q;

    private CommGrd<IfMasterDto>? _grid;
    private IReadOnlyList<IfMasterDto> _rows = [];

    private int _ifId;
    private IfMasterDetailDto? _master;
    private string? _flow;
    private IReadOnlyList<IfStepDto> _steps = [];
    private IReadOnlyList<IfNoteDto> _notes = [];
    private List<AttrEdit> _attrs = [];
    private IReadOnlyList<IfFileDto> _files = [];
    private IReadOnlyList<IfSystemDto> _systems = [];

    private FilePicker? _picker;
    private IReadOnlyList<PickedFile> _picked = [];
    private bool _uploading;

    private ConfirmDialog? _confirm;

    private bool _newOpen;
    private IfMasterDetailDto _new = new();
    private int _createdId;

    private bool _stepOpen;
    private IfStepDto _step = new();

    private bool _noteOpen;
    private IfNoteDto _note = new();

    private SchOption[] _statusOptions = [];
    private SchOption[] _directionOptions = [];
    private SchOption[] _cycleOptions = [];
    private SchOption[] _triggerOptions = [];
    private SchOption[] _stepTypeOptions = [];
    private SchOption[] _objectTypeOptions = [];
    private SchOption[] _noteTypeOptions = [];
    private object[] _systemOptions = [];
    private SchOption[] _domainOptions = [new(null, "전체")];

    /// <summary>코드 갈래별 목록. <c>CODE</c> 형 관리항목이 이것으로 고르개를 만든다.</summary>
    private readonly Dictionary<string, SchOption[]> _codesByGroup = [];

    private int? ProjectRid => int.TryParse(_projectCode, out var rid) ? rid : null;

    private string ListHint => $"{_rows.Count}건";

    private bool CanToggleNote => Can(MenuAction.Update);

    private SchOption[] StatusFilterOptions => [new(null, "전체"), .. _statusOptions];

    private static readonly SchOption[] UseOptions =
    [
        new("Y", "사용"),
        new("N", "미사용"),
        new(null, "전체"),
    ];

    private static readonly SchOption[] UseYnOptions =
    [
        new("Y", "사용 (Y)"),
        new("N", "미사용 (N)"),
    ];

    private object[] StepOptions =>
    [
        new { Text = "전체", Value = (int?)null },
        .. _steps.Select(s => new { Text = $"{s.StepNo}. {s.StepNm}", Value = (int?)s.StepId })
    ];

    private SchOption[] CodeOptions(string? group) =>
        group is not null && _codesByGroup.TryGetValue(group, out var list) ? list : [];

    /// <summary>상태 배지의 색. 원본의 여섯 색을 포털 배지 갈래에 맞췄다 — 보류만 맞는 갈래가 없어 이 화면 CSS 에 둔다.</summary>
    private static string StatusBadge(string? status) => status switch
    {
        "DEV" => "jsini-badge--info",
        "TEST" => "jsini-badge--warn",
        "PROD" => "jsini-badge--success",
        "HOLD" => "if-st-hold",
        "DROP" => "jsini-badge--err",
        _ => "jsini-badge--muted",
    };

    /// <summary>원본과 같은 크기 표기 — 1KB 아래도 1 KB 로 올려 적는다(0 KB 가 「빈 파일」로 읽힌다).</summary>
    private static string Kb(long bytes) =>
        bytes >= 1024 * 1024
            ? $"{bytes / 1024.0 / 1024.0:0.0} MB"
            : $"{Math.Max(1, (long)Math.Round(bytes / 1024.0))} KB";

    private string FileUrl(IfFileDto file) =>
        ProjectRid is int rid && file.FileId is not null
            ? FileDownload.InterfaceUrlFor(rid, _ifId, file.FileId, file.Name)
            : "#";

    /// <summary>DB 의 날짜 칸은 <c>yyyy-MM-dd</c> 글자로 오간다(서버가 <c>::date</c> 로 받는다).</summary>
    private static DateTime? DateOf(string? ymd) =>
        DateTime.TryParseExact(ymd, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : null;

    private static string? Ymd(DateTime? date) =>
        date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private int IndexOfStep(IfStepDto step)
    {
        for (var i = 0; i < _steps.Count; i++)
        {
            if (_steps[i].StepId == step.StepId) return i;
        }

        return -1;
    }

    protected override Task OnInitializedAsync() => LoadCodesAsync();

    /// <summary>선택목록을 한 번만 읽는다. 바뀌지 않는 자료라, 화면을 옮길 때마다 읽으면 왕복만 늘어난다.</summary>
    private async Task LoadCodesAsync()
    {
        var codes = await Api.CodesAsync();

        _codesByGroup.Clear();

        foreach (var group in codes.GroupBy(c => c.CodeGrp))
        {
            if (group.Key is null) continue;

            // 이름이 빈 코드가 있으면 코드라도 보여 준다 — 빈 줄이 늘어선 고르개는 고를 수 없다.
            _codesByGroup[group.Key] =
                [.. group.Select(c => new SchOption(c.Code, c.CodeNm ?? c.Code ?? string.Empty))];
        }

        _statusOptions = CodeOptions("IF_STATUS");
        _directionOptions = CodeOptions("IF_DIRECTION");
        _cycleOptions = CodeOptions("IF_CYCLE");
        _triggerOptions = CodeOptions("TRIGGER_TYPE");
        _stepTypeOptions = CodeOptions("STEP_TYPE");
        _objectTypeOptions = CodeOptions("OBJECT_TYPE");
        _noteTypeOptions = CodeOptions("NOTE_TYPE");
    }

    /// <summary>고르개를 바꾸면 바로 다시 읽는다 — 원본이 그렇게 움직였다. 검색어만 조회 단추(또는 Enter)를 기다린다.</summary>
    private Task FilterAsync(Action set)
    {
        set();
        return SearchAsync();
    }

    private Task SearchAsync() => LoadAsync(async () =>
    {
        if (ProjectRid is not int rid)
        {
            _rows = [];
            return 0;
        }

        var rows = Api.ListAsync(rid, _status, _domain, _q, _useYn);
        var counts = Api.FileCountsAsync(rid);
        var systems = Api.SystemsAsync(rid);

        await Task.WhenAll(rows, counts, systems);

        _rows = rows.Result;
        _systems = systems.Result;

        var byId = counts.Result.ToDictionary(c => c.IfId, c => c.Cnt);

        foreach (var row in _rows)
        {
            if (byId.TryGetValue(row.IfId, out var cnt)) row.FileCnt = cnt;
        }

        _systemOptions =
        [
            .. _systems.Select(s => new { Text = s.SystemNm, Value = (int?)s.SystemId })
        ];

        // 도메인은 코드표가 아니라 **쓰인 값**에서 모은다 — 원본도 그랬다.
        _domainOptions =
        [
            new(null, "전체"),
            .. _rows.Select(r => r.DomainCd)
                    .Where(d => !string.IsNullOrWhiteSpace(d))
                    .Distinct()
                    .Order()
                    .Select(d => new SchOption(d, d!))
        ];

        return _rows.Count;
    }, "등록된 인터페이스가 없습니다.", "인터페이스를 읽지 못했습니다");

    // ──────────────────────────────────────────── 목록 ↔ 상세

    private async Task OpenDetailAsync(int ifId)
    {
        _ifId = ifId;
        _mode = "detail";

        await LoadDetailAsync();
        await LoadFilesAsync();
    }

    private async Task ReloadDetailAsync()
    {
        await LoadDetailAsync();
        await LoadFilesAsync();
    }

    private async Task BackToListAsync()
    {
        _mode = "list";
        _ifId = 0;
        _master = null;

        await SearchAsync();
    }

    private Task LoadDetailAsync() => LoadAsync(async () =>
    {
        if (ProjectRid is not int rid || _ifId == 0) return 0;

        var detail = await Api.DetailAsync(rid, _ifId);

        _master = detail?.Master;
        _flow = detail?.Flow;
        _steps = detail?.Steps ?? [];
        _notes = detail?.Notes ?? [];

        _attrs =
        [
            .. (detail?.Attrs ?? []).Select(a => new AttrEdit
            {
                AttrDefId = a.AttrDefId,
                AttrCd = a.AttrCd,
                AttrNm = a.AttrNm,
                AttrType = a.AttrType,
                CodeGrp = a.CodeGrp,
                Required = a.RequiredYn == "Y",
                Value = a.AttrVal,
            })
        ];

        return 1;
    }, "상세를 찾지 못했습니다.", "상세를 읽지 못했습니다");

    // ──────────────────────────────────────────── 등록 · 저장 · 삭제

    private void StartNew()
    {
        if (ProjectRid is null)
        {
            Say("프로젝트를 먼저 고르세요.", NoticeTone.Warning);
            return;
        }

        _new = new IfMasterDetailDto { StatusCd = "DESIGN", UseYn = "Y" };
        _newOpen = true;
    }

    /// <summary>등록 창의 「등록」. 아래 띠의 ＋ 와 같은 저장 길(<c>CommGrd.SaveAsync</c>)로 보내고, 만든 것을 바로 연다 — 원본과 같다.</summary>
    private async Task CreateAsync()
    {
        if (string.IsNullOrWhiteSpace(_new.IfCd) || string.IsNullOrWhiteSpace(_new.IfNm)
            || string.IsNullOrWhiteSpace(_new.DirectionCd) || string.IsNullOrWhiteSpace(_new.StatusCd))
        {
            Say("코드 · 이름 · 방향 · 상태는 필수입니다.", NoticeTone.Warning);
            return;
        }

        if (_grid is null) return;

        _createdId = 0;

        // 표가 넘겨 주는 줄은 쓰지 않는다 — 등록 창의 값(_new)을 SaveNewAsync 가 읽는다.
        if (!await _grid.SaveAsync(new IfMasterDto(), true)) return;

        _newOpen = false;

        if (_createdId > 0) await OpenDetailAsync(_createdId);
    }

    /// <summary>CommGrd 의 OnSave. 등록만 이 길로 온다 — 기본정보 수정은 상세 구역의 단추가 따로 보낸다.</summary>
    private async Task SaveNewAsync((IfMasterDto Item, bool IsNew) _)
    {
        if (ProjectRid is not int rid) return;

        var body = new Dictionary<string, object?>
        {
            ["if_cd"] = _new.IfCd?.Trim(),
            ["if_nm"] = _new.IfNm?.Trim(),
            ["direction_cd"] = _new.DirectionCd,
            ["status_cd"] = _new.StatusCd,
            ["domain_cd"] = _new.DomainCd,
            ["use_yn"] = "Y",
        };

        _createdId = await Api.CreateAsync(rid, body);
    }

    private async Task SaveMasterAsync()
    {
        if (ProjectRid is not int rid || _master is null) return;

        if (string.IsNullOrWhiteSpace(_master.IfCd) || string.IsNullOrWhiteSpace(_master.IfNm))
        {
            Say("인터페이스 코드와 인터페이스명은 필수입니다.", NoticeTone.Warning);
            return;
        }

        var body = new Dictionary<string, object?>
        {
            ["if_cd"] = _master.IfCd,
            ["if_nm"] = _master.IfNm,
            ["if_desc"] = _master.IfDesc,
            ["direction_cd"] = _master.DirectionCd,
            ["src_system_id"] = _master.SrcSystemId,
            ["tgt_system_id"] = _master.TgtSystemId,
            ["domain_cd"] = _master.DomainCd,
            ["trigger_cd"] = _master.TriggerCd,
            ["cycle_cd"] = _master.CycleCd,
            ["status_cd"] = _master.StatusCd,
            ["owner_nm"] = _master.OwnerNm,
            ["owner_bp_id"] = _master.OwnerBpId,
            ["plan_sdt"] = _master.PlanSdt,
            ["plan_edt"] = _master.PlanEdt,
            ["open_dt"] = _master.OpenDt,
            ["remark"] = _master.Remark,
            ["sort_order"] = _master.SortOrder,
            ["use_yn"] = _master.UseYn ?? "Y",
        };

        var saved = await RunAsync(
            () => Api.UpdateAsync(rid, _ifId, body), "저장했습니다.", "저장하지 못했습니다");

        if (saved) await LoadDetailAsync();
    }

    /// <summary>지운다. 함께 사라지는 것과 감추는 길을 먼저 말한다 — 실제로 필요한 것은 대개 감추기다.</summary>
    private async Task DeleteMasterAsync()
    {
        if (ProjectRid is not int rid || _master is null || _confirm is null) return;

        var ok = await _confirm.AskAsync(
            $"「{_master.IfCd} {_master.IfNm}」 를 삭제하면 단계 · 메모 · 추가항목이 함께 사라집니다.\n"
            + "보관이 목적이라면 사용여부를 N 으로 바꾸는 편이 안전합니다.\n\n그래도 삭제할까요?",
            "인터페이스 삭제");

        if (!ok) return;

        var gone = await RunAsync(
            () => Api.DeleteAsync(rid, _ifId), "삭제했습니다.", "삭제하지 못했습니다");

        if (gone) await BackToListAsync();
    }

    // ──────────────────────────────────────────── 단계

    private void StartNewStep()
    {
        _step = new IfStepDto();
        _stepOpen = true;
    }

    private void EditStep(IfStepDto step)
    {
        _step = new IfStepDto
        {
            StepId = step.StepId,
            StepNo = step.StepNo,
            StepNm = step.StepNm,
            StepTypeCd = step.StepTypeCd,
            SystemId = step.SystemId,
            ObjectOwner = step.ObjectOwner,
            ObjectNm = step.ObjectNm,
            ObjectType = step.ObjectType,
            StepDesc = step.StepDesc,
            Params = step.Params,
        };

        _stepOpen = true;
    }

    private async Task SaveStepAsync()
    {
        if (ProjectRid is not int rid) return;

        if (string.IsNullOrWhiteSpace(_step.StepNm))
        {
            Say("단계명은 필수입니다.", NoticeTone.Warning);
            return;
        }

        // params 를 여기서 검사한다 — 안 막으면 서버가 22P02 로 끊고, 그 문구는 무엇이 틀렸는지 말해 주지 않는다.
        if (!string.IsNullOrWhiteSpace(_step.Params))
        {
            try
            {
                System.Text.Json.JsonDocument.Parse(_step.Params).Dispose();
            }
            catch (System.Text.Json.JsonException e)
            {
                Say($"params 가 올바른 JSON 이 아닙니다: {e.Message}", NoticeTone.Warning);
                return;
            }
        }

        var body = new Dictionary<string, object?>
        {
            ["step_nm"] = _step.StepNm,
            ["step_type_cd"] = _step.StepTypeCd,
            ["system_id"] = _step.SystemId,
            ["object_owner"] = _step.ObjectOwner,
            ["object_nm"] = _step.ObjectNm,
            ["object_type"] = _step.ObjectType,
            ["step_desc"] = _step.StepDesc,
            ["params"] = string.IsNullOrWhiteSpace(_step.Params) ? null : _step.Params,
        };

        var saved = _step.StepId == 0
            ? await RunAsync(() => Api.CreateStepAsync(rid, _ifId, body), "단계를 추가했습니다.", "단계를 추가하지 못했습니다")
            : await RunAsync(() => Api.UpdateStepAsync(rid, _ifId, _step.StepId, body), "저장했습니다.", "저장하지 못했습니다");

        if (!saved) return;

        _stepOpen = false;
        await LoadDetailAsync();
    }

    private async Task DeleteStepAsync(IfStepDto step)
    {
        if (ProjectRid is not int rid || _confirm is null) return;

        var ok = await _confirm.AskAsync(
            $"단계 {step.StepNo}. {step.StepNm} 을 삭제할까요?", "단계 삭제");

        if (!ok) return;

        var gone = await RunAsync(
            () => Api.DeleteStepAsync(rid, _ifId, step.StepId), "삭제했습니다.", "삭제하지 못했습니다");

        if (gone) await LoadDetailAsync();
    }

    /// <summary>이웃과 자리를 바꿔 통째로 다시 매긴다.</summary>
    private async Task MoveStepAsync(int index, int direction)
    {
        if (ProjectRid is not int rid) return;

        var ids = _steps.Select(s => s.StepId).ToList();
        var other = index + direction;

        if (index < 0 || other < 0 || other >= ids.Count) return;

        (ids[index], ids[other]) = (ids[other], ids[index]);

        var moved = await RunAsync(
            () => Api.ReorderStepsAsync(rid, _ifId, ids), "차례를 바꿨습니다.", "차례를 바꾸지 못했습니다");

        if (moved) await LoadDetailAsync();
    }

    // ──────────────────────────────────────────── 메모 · 이슈

    private void StartNewNote()
    {
        _note = new IfNoteDto { DoneYn = "N" };
        _noteOpen = true;
    }

    /// <summary>메모는 더하고 지우기만 한다 — 원본에 고치는 길이 없다. 이슈의 해결 표시만 목록에서 켜고 끈다.</summary>
    private async Task SaveNoteAsync()
    {
        if (ProjectRid is not int rid) return;

        if (string.IsNullOrWhiteSpace(_note.Title))
        {
            Say("메모 제목은 필수입니다.", NoticeTone.Warning);
            return;
        }

        // 유형·내용은 빈 값을 안 보낸다. 서버가 빈 글자를 null 로 바꿔 넣는데 두 칸이 NOT NULL 이라
        // 그대로 보내면 등록이 끊긴다 — 빼고 보내면 DB 기본값(MEMO · 빈 글)이 들어간다.
        var body = new Dictionary<string, object?>
        {
            ["title"] = _note.Title,
            ["done_yn"] = "N",
            ["step_id"] = _note.StepId,

            // DB 기본값(current_date)은 UTC 날짜라 아침 아홉 시 전에는 하루 전으로 찍힌다.
            ["note_dt"] = AppTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        };

        if (!string.IsNullOrWhiteSpace(_note.NoteTypeCd)) body["note_type_cd"] = _note.NoteTypeCd;
        if (!string.IsNullOrWhiteSpace(_note.Content)) body["content"] = _note.Content;
        if (!string.IsNullOrWhiteSpace(_note.WriterNm)) body["writer_nm"] = _note.WriterNm;

        var saved = await RunAsync(
            () => Api.CreateNoteAsync(rid, _ifId, body), "메모를 추가했습니다.", "메모를 추가하지 못했습니다");

        if (!saved) return;

        _noteOpen = false;
        await LoadDetailAsync();
    }

    /// <summary>해결 표시를 켜고 끈다. <b>창을 열지 않는다.</b></summary>
    private async Task ToggleNoteAsync(IfNoteDto note)
    {
        if (ProjectRid is not int rid) return;

        var body = new Dictionary<string, object?> { ["done_yn"] = note.Done ? "N" : "Y" };

        var saved = await RunAsync(
            () => Api.UpdateNoteAsync(rid, _ifId, note.NoteId, body), "바꿨습니다.", "바꾸지 못했습니다");

        if (saved) await LoadDetailAsync();
    }

    /// <summary>CommGrd 의 OnDelete. 묻는 것과 다시 읽는 것은 표가 한다(DeleteConfirm · Reload).</summary>
    private async Task DeleteNoteAsync(IfNoteDto note)
    {
        if (ProjectRid is not int rid) return;

        await Api.DeleteNoteAsync(rid, _ifId, note.NoteId);
    }

    // ──────────────────────────────────────────── 관리항목

    /// <summary>필수와 JSON 을 여기서 검사한다. required_yn 은 정의상의 뜻이고 서버는 강제하지 않으니, 화면이 안 막으면 아무도 안 막는다.</summary>
    private async Task SaveAttrsAsync()
    {
        if (ProjectRid is not int rid) return;

        foreach (var attr in _attrs)
        {
            if (attr.Required && string.IsNullOrWhiteSpace(attr.Value))
            {
                Say($"필수 항목입니다: {attr.AttrNm}", NoticeTone.Warning);
                return;
            }

            if (attr.AttrType != "JSON" || string.IsNullOrWhiteSpace(attr.Value)) continue;

            try
            {
                System.Text.Json.JsonDocument.Parse(attr.Value).Dispose();
            }
            catch (System.Text.Json.JsonException e)
            {
                Say($"{attr.AttrNm} 이 올바른 JSON 이 아닙니다: {e.Message}", NoticeTone.Warning);
                return;
            }
        }

        var values = _attrs.ToDictionary(a => a.AttrDefId.ToString(CultureInfo.InvariantCulture), a => a.Value);

        var saved = await RunAsync(
            () => Api.SaveAttrsAsync(rid, _ifId, values), "관리항목을 저장했습니다.", "저장하지 못했습니다");

        if (saved) await LoadDetailAsync();
    }

    // ──────────────────────────────────────────── 첨부

    private Task LoadFilesAsync() => LoadAsync(async () =>
    {
        if (ProjectRid is not int rid || _ifId == 0)
        {
            _files = [];
            return 1;
        }

        _files = await Api.FilesAsync(rid, _ifId);
        return 1;
    }, "첨부파일이 없습니다.", "첨부파일을 읽지 못했습니다");

    private async Task UploadAsync()
    {
        if (ProjectRid is not int rid || _picked.Count == 0 || _uploading) return;

        var count = _picked.Count;
        _uploading = true;

        try
        {
            var sent = await RunAsync(
                async () => _files = await Api.UploadFilesAsync(rid, _ifId, _picked),
                $"{count}개를 올렸습니다.", "올리지 못했습니다");

            if (!sent) return;

            _picked = [];

            if (_picker is not null) await _picker.ClearAsync();
        }
        finally
        {
            _uploading = false;
        }
    }

    private async Task DeleteFileAsync(IfFileDto file)
    {
        if (ProjectRid is not int rid || file.FileId is null) return;

        await Api.DeleteFileAsync(rid, _ifId, file.FileId);
    }

    /// <summary>편집 중인 관리항목 하나. 사전에 묶지 않고 줄 목록으로 푼다 — @bind 는 사전의 칸을 못 잡고, 타입마다 다른 편집기를 붙이려면 줄마다 값이 있어야 한다.</summary>
    private sealed class AttrEdit
    {
        public int AttrDefId { get; set; }
        public string? AttrCd { get; set; }
        public string? AttrNm { get; set; }
        public string? AttrType { get; set; }
        public string? CodeGrp { get; set; }
        public bool Required { get; set; }
        public string? Value { get; set; }

        /// <summary>제목 줄. 필수면 별을 붙인다.</summary>
        public string Caption => Required ? $"{AttrNm} *" : AttrNm ?? "";

        /// <summary><c>YN</c> 형의 확인칸. DB 는 <c>Y</c>/<c>N</c> 글자다.</summary>
        public bool Flag
        {
            get => Value == "Y";
            set => Value = value ? "Y" : "N";
        }

        /// <summary><c>NUMBER</c> 형의 숫자칸. 값은 글자 칸(attr_val) 하나에 담기므로 불변 문화로 오간다.</summary>
        public decimal? Number
        {
            get => decimal.TryParse(Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : null;
            set => Value = value?.ToString(CultureInfo.InvariantCulture);
        }
    }
}
