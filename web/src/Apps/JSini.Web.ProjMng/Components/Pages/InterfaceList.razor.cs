using Microsoft.AspNetCore.Components;
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
        SchSummary.NameOf(_statusOptions, o => o.Value, o => o.Text, _status),
        _domain,
        SchSummary.NameOf(UseOptions, o => o.Value, o => o.Text, _useYn),
        _q);

    /// <summary>
    /// 고른 프로젝트의 <b>이름</b>. 화면이 든 것은 코드뿐이고 목록은
    /// <c>CodeSelect</c> 안에 있어, 그 부품이 <c>@bind-Text</c> 로 올려 준다.
    /// </summary>
    private string? _projectName;

    private string _mode = "list";

    private string? _projectCode;
    private string? _status;
    private string? _domain;
    private string? _useYn = "Y";
    private string? _q;

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

    private ConfirmDialog? _confirm;

    private bool _newOpen;
    private IfMasterDetailDto _new = new();

    private bool _stepOpen;
    private IfStepDto _step = new();
    private int? _stepSystemId;

    private bool _noteOpen;
    private IfNoteDto _note = new();
    private int? _noteStepId;

    private SchOption[] _statusOptions = [];
    private SchOption[] _directionOptions = [];
    private SchOption[] _cycleOptions = [];
    private SchOption[] _triggerOptions = [];
    private SchOption[] _stepTypeOptions = [];
    private SchOption[] _objectTypeOptions = [];
    private SchOption[] _noteTypeOptions = [];
    private object[] _systemOptions = [];
    private object[] _domainOptions = [];

    /// <summary>코드 갈래별 목록. <c>CODE</c> 형 관리항목이 이것으로 고르개를 만든다.</summary>
    private readonly Dictionary<string, SchOption[]> _codesByGroup = [];

    private int? ProjectRid => int.TryParse(_projectCode, out var rid) ? rid : null;

    private string Hint => $"{_rows.Count}건 · 이슈 {_rows.Sum(r => r.OpenIssueCnt)}건";

    private static readonly SchOption[] UseOptions =
    [
        new("Y", "사용"),
        new("N", "미사용"),
        new(null, "전체"),
    ];

    private static readonly object[] UseYnOptions =
    [
        new { Text = "사용", Value = "Y" },
        new { Text = "미사용", Value = "N" },
    ];

    private object[] StepOptions =>
    [
        new { Text = "(인터페이스 전체)", Value = (int?)null },
        .. _steps.Select(s => new { Text = $"{s.StepNo}. {s.StepNm}", Value = (int?)s.StepId })
    ];

    private SchOption[] CodeOptions(string? group) =>
        group is not null && _codesByGroup.TryGetValue(group, out var list) ? list : [];

    // `switch` 의 관계 패턴(`< 1024`)을 `@code` 에 쓰지 않는다 — Razor 가 그
    // `<` 를 여는 태그로 읽고 뒤를 통째로 잘못 자른다(실제로 밟음).
    private static string Size(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:0.#} KB";

        return $"{bytes / 1024.0 / 1024.0:0.#} MB";
    }

    private int IndexOfStep(IfStepDto step)
    {
        for (var i = 0; i < _steps.Count; i++)
        {
            if (_steps[i].StepId == step.StepId) return i;
        }

        return -1;
    }

    protected override Task OnInitializedAsync() => LoadCodesAsync();

    /// <summary>
    /// 선택목록을 한 번만 읽는다. 갈래가 일곱이라 화면을 옮길 때마다 읽으면
    /// 왕복이 그만큼 늘고, <b>바뀌지 않는 자료</b>다.
    /// </summary>
    private async Task LoadCodesAsync()
    {
        var codes = await Api.CodesAsync();

        _codesByGroup.Clear();

        foreach (var group in codes.GroupBy(c => c.CodeGrp))
        {
            if (group.Key is null) continue;

            // 이름이 비어 있는 코드가 있다. 그때는 코드라도 보여 준다 —
            // 빈 줄이 늘어선 고르개는 고를 수가 없다.
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
            new { Text = "(없음)", Value = (int?)null },
            .. _systems.Select(s => new { Text = s.SystemNm, Value = (int?)s.SystemId })
        ];

        // 업무 영역은 코드표가 아니라 **쓰인 값**에서 모은다. 원본도 그렇게 했다.
        _domainOptions =
        [
            new { Text = "전체", Value = (string?)null },
            .. _rows.Select(r => r.DomainCd)
                    .Where(d => !string.IsNullOrWhiteSpace(d))
                    .Distinct()
                    .Order()
                    .Select(d => new { Text = d, Value = d })
        ];

        return _rows.Count;
    }, "인터페이스가 없습니다.", "인터페이스를 읽지 못했습니다");

    // ──────────────────────────────────────────── 목록 ↔ 상세

    private async Task OpenDetailAsync(int ifId)
    {
        _ifId = ifId;
        _mode = "detail";

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
        _new = new IfMasterDetailDto { StatusCd = "DESIGN", UseYn = "Y" };
        _newOpen = true;
    }

    /// <summary>
    /// 등록은 넷만 받는다 — 코드 · 이름 · 방향 · 상태. 나머지는 상세에서 채운다.
    /// </summary>
    private async Task CreateAsync()
    {
        if (ProjectRid is not int rid) return;

        if (string.IsNullOrWhiteSpace(_new.IfCd) || string.IsNullOrWhiteSpace(_new.IfNm)
            || string.IsNullOrWhiteSpace(_new.DirectionCd) || string.IsNullOrWhiteSpace(_new.StatusCd))
        {
            Say("코드 · 이름 · 방향 · 상태는 필수입니다.", NoticeTone.Warning);
            return;
        }

        var code = _new.IfCd;

        var body = new Dictionary<string, object?>
        {
            ["if_cd"] = _new.IfCd,
            ["if_nm"] = _new.IfNm,
            ["direction_cd"] = _new.DirectionCd,
            ["status_cd"] = _new.StatusCd,
            ["use_yn"] = "Y",
        };

        var made = await RunAsync(
            () => Api.CreateAsync(rid, body), "등록했습니다.", "등록하지 못했습니다");

        if (!made) return;

        _newOpen = false;
        await SearchAsync();

        // 만든 것을 바로 연다 — 원본과 같다. 번호는 서버가 정하므로 코드로 되찾는다.
        var made_row = _rows.FirstOrDefault(r => r.IfCd == code);
        if (made_row is not null) await OpenDetailAsync(made_row.IfId);
    }

    private async Task SaveMasterAsync()
    {
        if (ProjectRid is not int rid || _master is null) return;

        if (string.IsNullOrWhiteSpace(_master.IfCd) || string.IsNullOrWhiteSpace(_master.IfNm))
        {
            Say("코드와 이름은 필수입니다.", NoticeTone.Warning);
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

    /// <summary>
    /// 지운다. <b>무엇이 함께 사라지는지</b>와 감추는 길을 먼저 말한다 —
    /// 실제로 필요한 것은 대개 감추기다(원본의 문구를 그대로 옮겼다).
    /// </summary>
    private async Task DeleteMasterAsync()
    {
        if (ProjectRid is not int rid || _master is null || _confirm is null) return;

        var ok = await _confirm.AskAsync(
            $"「{_master.IfCd} {_master.IfNm}」 를 지우면 처리 단계 · 메모 · 추가항목이 함께 사라집니다.\n"
            + "보관이 목적이라면 사용여부를 N 으로 바꾸는 편이 안전합니다.",
            "인터페이스 삭제", "지운다");

        if (!ok) return;

        var gone = await RunAsync(
            () => Api.DeleteAsync(rid, _ifId), "지웠습니다.", "지우지 못했습니다");

        if (gone) await BackToListAsync();
    }

    // ──────────────────────────────────────────── 단계

    private void StartNewStep()
    {
        _step = new IfStepDto();
        _stepSystemId = null;
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
            ObjectOwner = step.ObjectOwner,
            ObjectNm = step.ObjectNm,
            ObjectType = step.ObjectType,
            StepDesc = step.StepDesc,
            Params = step.Params,
        };

        // 단계 조회는 시스템 **이름**만 준다(뷰가 그렇게 만든다). 고르개는
        // 번호로 도므로 이름으로 되찾는다.
        _stepSystemId = _systems.FirstOrDefault(s => s.SystemNm == step.SystemNm)?.SystemId;

        _stepOpen = true;
    }

    private async Task SaveStepAsync()
    {
        if (ProjectRid is not int rid) return;

        if (string.IsNullOrWhiteSpace(_step.StepNm))
        {
            Say("단계 이름은 필수입니다.", NoticeTone.Warning);
            return;
        }

        // **params 를 여기서 검사한다.** 안 막으면 서버가 22P02 로 끊고,
        // 그 문구는 사람에게 「무엇이 틀렸는지」를 하나도 말해 주지 않는다.
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
            ["system_id"] = _stepSystemId,
            ["object_owner"] = _step.ObjectOwner,
            ["object_nm"] = _step.ObjectNm,
            ["object_type"] = _step.ObjectType,
            ["step_desc"] = _step.StepDesc,
            ["params"] = string.IsNullOrWhiteSpace(_step.Params) ? null : _step.Params,
        };

        var saved = _step.StepId == 0
            ? await RunAsync(() => Api.CreateStepAsync(rid, _ifId, body), "단계를 더했습니다.", "단계를 더하지 못했습니다")
            : await RunAsync(() => Api.UpdateStepAsync(rid, _ifId, _step.StepId, body), "저장했습니다.", "저장하지 못했습니다");

        if (!saved) return;

        _stepOpen = false;
        await LoadDetailAsync();
    }

    private async Task DeleteStepAsync(IfStepDto step)
    {
        if (ProjectRid is not int rid || _confirm is null) return;

        var ok = await _confirm.AskAsync(
            $"{step.StepNo}. {step.StepNm} 단계를 지웁니다.\n되돌릴 수 없습니다.", "단계 삭제");

        if (!ok) return;

        var gone = await RunAsync(
            () => Api.DeleteStepAsync(rid, _ifId, step.StepId), "지웠습니다.", "지우지 못했습니다");

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

    // ──────────────────────────────────────────── 메모

    private void StartNewNote()
    {
        _note = new IfNoteDto
        {
            NoteTypeCd = "MEMO",
            DoneYn = "N",
            NoteDt = DateTime.Today.ToString("yyyy-MM-dd"),
        };

        _noteStepId = null;
        _noteOpen = true;
    }

    private void EditNote(IfNoteDto note)
    {
        _note = new IfNoteDto
        {
            NoteId = note.NoteId,
            NoteTypeCd = note.NoteTypeCd,
            Title = note.Title,
            Content = note.Content,
            WriterNm = note.WriterNm,
            NoteDt = note.NoteDt,
            DoneYn = note.DoneYn,
        };

        _noteStepId = note.StepId;
        _noteOpen = true;
    }

    private async Task SaveNoteAsync()
    {
        if (ProjectRid is not int rid) return;

        if (string.IsNullOrWhiteSpace(_note.Title))
        {
            Say("메모 제목은 필수입니다.", NoticeTone.Warning);
            return;
        }

        var body = new Dictionary<string, object?>
        {
            ["note_type_cd"] = _note.NoteTypeCd,
            ["title"] = _note.Title,
            ["content"] = _note.Content,
            ["writer_nm"] = _note.WriterNm,
            ["note_dt"] = _note.NoteDt,
            ["done_yn"] = _note.DoneYn ?? "N",
            ["step_id"] = _noteStepId,
        };

        var saved = _note.NoteId == 0
            ? await RunAsync(() => Api.CreateNoteAsync(rid, _ifId, body), "메모를 더했습니다.", "메모를 더하지 못했습니다")
            : await RunAsync(() => Api.UpdateNoteAsync(rid, _ifId, _note.NoteId, body), "저장했습니다.", "저장하지 못했습니다");

        if (!saved) return;

        _noteOpen = false;
        await LoadDetailAsync();
    }

    /// <summary>완료를 켜고 끈다. <b>창을 열지 않는다.</b></summary>
    private async Task ToggleNoteAsync(IfNoteDto note)
    {
        if (ProjectRid is not int rid) return;

        var body = new Dictionary<string, object?> { ["done_yn"] = note.Done ? "N" : "Y" };

        var saved = await RunAsync(
            () => Api.UpdateNoteAsync(rid, _ifId, note.NoteId, body), "바꿨습니다.", "바꾸지 못했습니다");

        if (saved) await LoadDetailAsync();
    }

    private async Task DeleteNoteAsync(IfNoteDto note)
    {
        if (ProjectRid is not int rid || _confirm is null) return;

        var ok = await _confirm.AskAsync(
            $"「{note.Title}」 메모를 지웁니다.\n되돌릴 수 없습니다.", "메모 삭제");

        if (!ok) return;

        var gone = await RunAsync(
            () => Api.DeleteNoteAsync(rid, _ifId, note.NoteId), "지웠습니다.", "지우지 못했습니다");

        if (gone) await LoadDetailAsync();
    }

    // ──────────────────────────────────────────── 관리항목

    /// <summary>
    /// 필수와 JSON 을 여기서 검사한다.
    /// </summary>
    /// <remarks>
    /// <c>required_yn</c> 은 정의에 적힌 뜻이고 <b>서버는 그것을 강제하지
    /// 않는다</b> — 화면이 안 막으면 아무도 안 막는다.
    /// </remarks>
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
                Say($"{attr.AttrNm} 이(가) 올바른 JSON 이 아닙니다: {e.Message}", NoticeTone.Warning);
                return;
            }
        }

        var values = _attrs.ToDictionary(a => a.AttrDefId.ToString(), a => a.Value);

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
    }, "첨부가 없습니다.", "첨부를 읽지 못했습니다");

    private async Task UploadAsync()
    {
        if (ProjectRid is not int rid || _picked.Count == 0) return;

        var count = _picked.Count;

        var sent = await RunAsync(
            async () => _files = await Api.UploadFilesAsync(rid, _ifId, _picked),
            $"{count}개를 올렸습니다.", "올리지 못했습니다");

        if (!sent) return;

        _picked = [];

        if (_picker is not null) await _picker.ClearAsync();
    }

    private async Task DeleteFileAsync(IfFileDto file)
    {
        if (ProjectRid is not int rid || file.FileId is null) return;

        await Api.DeleteFileAsync(rid, _ifId, file.FileId);
    }

    /// <summary>
    /// 편집 중인 관리항목 하나.
    /// </summary>
    /// <remarks>
    /// 사전에 바로 묶지 않고 줄 목록으로 푸는 까닭 — <c>@@bind</c> 는 사전의
    /// 칸을 잡지 못하고, 타입마다 다른 편집기를 붙이려면 줄마다 값이 있어야 한다.
    /// </remarks>
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
    }
}
