using Microsoft.AspNetCore.Components;
using DevExpress.Blazor;
using JSini.Web.Abstractions;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.ProjMng.Api;
using JSini.Web.ProjMng.Components.Shared;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class MailTemplateList
{
    [Inject] private MailTemplateClient Api { get; set; } = default!;

    private IReadOnlyList<MailTemplateDto> _rows = [];

    private MailTemplateDto? _picked;

    /// <summary>오른쪽에서 고치는 사본. 목록의 줄을 바로 고치면 저장하지 않은 글이 목록에 먼저 보인다.</summary>
    private MailTemplateDto? _form;

    private bool _isNew;

    private MailHtmlEditor? _editor;

    /// <summary>다른 템플릿으로 갈 때 편집기를 새로 세운다 — 되돌리기 기록이 앞 템플릿 것으로 남지 않게.</summary>
    private int _editorKey;

    private ConfirmDialog? _confirm;

    private bool _previewing;
    private string _previewSubject = string.Empty;
    private string _previewBody = string.Empty;

    private bool CanSave => _isNew ? Can(MenuAction.Create) : Can(MenuAction.Update);

    protected override Task OnInitializedAsync() => LoadListAsync();

    private Task LoadListAsync() => LoadAsync(async () =>
    {
        _rows = await Api.ListAsync(MailTemplateDto.KindSchedule);

        if (_picked is not null)
        {
            _picked = _rows.FirstOrDefault(r => r.TplId == _picked.TplId);
        }

        return _rows.Count;
    }, "등록된 템플릿이 없습니다.", "템플릿을 읽지 못했습니다");

    private async Task PickAsync(MailTemplateDto? row)
    {
        if (row is null || row.TplId == _form?.TplId && !_isNew)
        {
            _picked = row ?? _picked;
            return;
        }

        if (!await LeaveEditsAsync())
        {
            return;
        }

        _picked = row;
        _form = row.Copy();
        _isNew = false;
        _editorKey++;
    }

    private async Task StartNewAsync()
    {
        if (!await LeaveEditsAsync())
        {
            return;
        }

        _picked = null;
        _isNew = true;
        _form = new MailTemplateDto
        {
            TplNm = "새 템플릿",
            BodyHtml = ScheduleMailBody.DefaultTemplate,
            SortNo = _rows.Count == 0 ? 10 : _rows.Max(r => r.SortNo) + 10,
        };
        _editorKey++;
    }

    /// <summary>고치던 것이 저장된 것과 다르면 버려도 되는지 묻는다.</summary>
    private async Task<bool> LeaveEditsAsync()
    {
        if (_form is null || _confirm is null)
        {
            return true;
        }

        if (_editor is not null)
        {
            _form.BodyHtml = await _editor.FlushAsync() ?? _form.BodyHtml;
        }

        var saved = _rows.FirstOrDefault(r => r.TplId == _form.TplId);
        var dirty = _isNew
            || saved is null
            || saved.TplNm != _form.TplNm || saved.TplDesc != _form.TplDesc || saved.Subject != _form.Subject
            || saved.BodyHtml != _form.BodyHtml || saved.UseYn != _form.UseYn || saved.SortNo != _form.SortNo;

        return !dirty || !CanSave
            || await _confirm.AskAsync("저장하지 않은 고침이 있습니다.\n버리고 넘어갈까요?", "고침 버리기", "버리기");
    }

    private async Task SaveAsync()
    {
        if (_form is null)
        {
            return;
        }

        if (_editor is not null)
        {
            _form.BodyHtml = await _editor.FlushAsync() ?? _form.BodyHtml;
        }

        if (string.IsNullOrWhiteSpace(_form.TplNm))
        {
            Say("템플릿 이름을 적으십시오.", NoticeTone.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(_form.BodyHtml))
        {
            Say("본문이 비어 있습니다.", NoticeTone.Warning);
            return;
        }

        if (!_form.BodyHtml.Contains("{{timeline}}", StringComparison.Ordinal)
            && _confirm is not null
            && !await _confirm.AskAsync("본문에 {{timeline}} 이 없어 일정이 메일에 안 들어갑니다.\n그대로 저장할까요?",
                "치환자 확인", "그대로 저장", ButtonRenderStyle.Primary))
        {
            return;
        }

        MailTemplateDto? saved = null;
        var form = _form;
        var isNew = _isNew;

        var ok = await RunAsync(async () =>
        {
            saved = isNew ? await Api.CreateAsync(form) : await Api.UpdateAsync(form);
        }, "저장했습니다.", "저장하지 못했습니다");

        if (!ok)
        {
            return;
        }

        await LoadListAsync();

        var id = saved?.TplId ?? form.TplId;
        _picked = _rows.FirstOrDefault(r => r.TplId == id);

        if (_picked is not null)
        {
            _form = _picked.Copy();
            _isNew = false;
        }
    }

    private async Task DeleteAsync()
    {
        if (_form is null || _isNew || _confirm is null)
        {
            return;
        }

        if (!await _confirm.AskAsync($"템플릿 「{_form.TplNm}」 을(를) 지웁니다.\n되돌릴 수 없습니다."))
        {
            return;
        }

        var id = _form.TplId;

        if (await RunAsync(() => Api.DeleteAsync(id), "지웠습니다.", "지우지 못했습니다"))
        {
            _form = null;
            _picked = null;
            await LoadListAsync();
        }
    }

    private async Task LoadDefaultAsync()
    {
        if (_form is null || _confirm is null)
        {
            return;
        }

        if (!await _confirm.AskAsync("지금 본문을 기본 틀로 바꿉니다.", "기본 틀 불러오기", "바꾸기", ButtonRenderStyle.Primary))
        {
            return;
        }

        _form.BodyHtml = ScheduleMailBody.DefaultTemplate;
    }

    private async Task InsertKeyAsync(string key)
    {
        if (_editor is null || !CanSave)
        {
            return;
        }

        if (!await _editor.InsertAsync($"{{{{{key}}}}}"))
        {
            Say($"「HTML」 탭에서는 원하는 자리에 {{{{{key}}}}} 을 직접 적으십시오.", NoticeTone.Info);
        }
    }

    /// <summary>오늘 앞뒤 일주일에 견본 일정 넷을 깔아 채운다 — 실제 자료를 읽지 않아도 모양을 본다.</summary>
    private async Task PreviewAsync()
    {
        if (_form is null)
        {
            return;
        }

        if (_editor is not null)
        {
            _form.BodyHtml = await _editor.FlushAsync() ?? _form.BodyHtml;
        }

        var today = AppTime.Today;
        var (from, to) = ScheduleMailBody.Window(today);
        var at = today.ToDateTime(TimeOnly.MinValue);

        var input = new ScheduleMailInput(from, to, today,
        [
            new(at.AddDays(-3), at.AddDays(-3), "[견본] 요구사항 검토 회의"),
            new(at.AddHours(10), at.AddHours(11), "[견본] 주간 진행 점검"),
            new(at.AddDays(2), at.AddDays(4), "[견본] 통합 시험"),
            new(at.AddDays(6), at.AddDays(6), "[견본] 중간 보고"),
        ], "견본 프로젝트", "홍길동", null);

        _previewSubject = ScheduleMailBody.Subject(input, _form.Subject);
        _previewBody = ScheduleMailBody.Build(input, _form.BodyHtml);
        _previewing = true;
    }
}
