using Microsoft.AspNetCore.Components;
using JSini.Web.Components.Data;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class Source
{
    [Inject] private SourceInfoClient Api { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Or(_projectName);

    /// <summary>
    /// 고른 프로젝트의 <b>이름</b>. 화면이 든 것은 코드뿐이고 목록은
    /// <c>CodeSelect</c> 안에 있어, 그 부품이 <c>@bind-Text</c> 로 올려 준다.
    /// </summary>
    private string? _projectName;

    private IReadOnlyList<SourceInfoDto> _sources = [];
    private IReadOnlyList<SourceInfoDetailDto> _details = [];

    private string? _projectCode;
    private SourceInfoDto? _source;

    /// <summary>편집 창이 붙잡고 있는 <b>사본</b>. 표의 줄이 아니다(머리말).</summary>
    private SourceInfoDto _form = new();

    private bool _editing;
    private bool _isNew;

    private int? ProjectRid => int.TryParse(_projectCode, out var rid) ? rid : null;

    protected override Task OnInitializedAsync() => SearchAsync();

    private Task SearchAsync() => LoadAsync(async () =>
    {
        _sources = await Api.ListAsync(ProjectRid);

        if (_source is not null)
        {
            // 다시 읽은 줄로 갈아 끼운다. 안 바꾸면 오른쪽 제목이 **고치기
            // 전의 별칭**을 계속 들고 있다. 목록에서 빠졌으면(프로젝트를
            // 바꿨거나) 오른쪽을 비운다.
            _source = _sources.FirstOrDefault(s => s.SrcRid == _source.SrcRid);

            if (_source is null)
            {
                _details = [];
            }
        }

        return _sources.Count;
    }, "등록된 소스가 없습니다.", "소스를 읽지 못했습니다");

    private Task PickSourceAsync(SourceInfoDto? source)
    {
        _source = source;

        if (source is null)
        {
            _details = [];
            return Task.CompletedTask;
        }

        return LoadDetailsAsync();
    }

    private Task LoadDetailsAsync() => _source is null ? Task.CompletedTask : LoadAsync(async () =>
    {
        _details = await Api.DetailsAsync(_source.SrcRid);
        return _details.Count;
    }, "이 소스에 패턴이 없습니다.", "패턴을 읽지 못했습니다");

    /// <summary>새 소스는 <b>고른 프로젝트에 붙는다.</b> 「전체」면 비어 간다.</summary>
    private void StartNewSource()
    {
        _form = new SourceInfoDto { PrjRid = ProjectRid };
        _isNew = true;
        _editing = true;
    }

    private void StartEditSource(SourceInfoDto s)
    {
        _form = Copy(s);
        _isNew = false;
        _editing = true;
    }

    /// <summary>표의 줄을 그대로 고치지 않으려고 뜬다(머리말).</summary>
    private static SourceInfoDto Copy(SourceInfoDto s) => new()
    {
        SrcRid = s.SrcRid,
        PrjRid = s.PrjRid,
        PrjName = s.PrjName,
        PrjNick = s.PrjNick,
        SrcOs = s.SrcOs,
        SrcPath = s.SrcPath,
        SrcNick = s.SrcNick,
        SrcType = s.SrcType,
        SrcLang = s.SrcLang,
        SrcComm = s.SrcComm,
        SrcUiRoot = s.SrcUiRoot,
        PrjNamespace = s.PrjNamespace,
        UrlPattern = s.UrlPattern,
    };

    private async Task SaveSourceAsync()
    {
        var ok = await RunAsync(async () =>
        {
            if (_isNew)
            {
                await Api.CreateAsync(_form);
            }
            else
            {
                await Api.UpdateAsync(_form);
            }
        }, "저장했습니다.", "저장하지 못했습니다");

        if (!ok)
        {
            // 창을 열어 둔다 — 닫으면 적던 것이 사라지고 다시 적어야 한다.
            return;
        }

        _editing = false;
        await SearchAsync();
    }

    /// <summary>새 패턴은 <b>고른 소스에 붙는다.</b> 서버도 경로로 한 번 더 정한다.</summary>
    private void FillNewDetail(SourceInfoDetailDto d) => d.SrcRid = _source?.SrcRid;

    private async Task SaveDetailAsync((SourceInfoDetailDto Item, bool IsNew) e)
    {
        if (_source is null)
        {
            return;
        }

        if (e.IsNew)
        {
            await Api.CreateDetailAsync(_source.SrcRid, e.Item);
        }
        else
        {
            await Api.UpdateDetailAsync(_source.SrcRid, e.Item);
        }

        // URL 패턴이 왼쪽 목록에 올라가므로 함께 다시 읽는다(머리말).
        await SearchAsync();
    }

    private async Task DeleteDetailAsync(SourceInfoDetailDto d)
    {
        if (_source is null)
        {
            return;
        }

        await Api.DeleteDetailAsync(_source.SrcRid, d.SrcDtlRid);
        await SearchAsync();
    }
}
