using Microsoft.AspNetCore.Components;
using JSini.Web.Http;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class GlueTrace
{
    [Inject] private ActivityInfoClient Api { get; set; } = default!;
    [Inject] private ProjMngClient Client { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(_projectName, _sourceName);

    /// <summary>
    /// 고른 프로젝트의 <b>이름</b>. 화면이 든 것은 코드뿐이고 목록은
    /// <c>CodeSelect</c> 안에 있어, 그 부품이 <c>@bind-Text</c> 로 올려 준다.
    /// </summary>
    private string? _projectName;

    /// <summary>고른 소스의 이름. 프로젝트와 같은 까닭으로 부품이 올려 준다.</summary>
    private string? _sourceName;

    private IReadOnlyList<ActivityInfoDto> _rows = [];
    private ActivityInfoDto? _picked;

    private string? _projectCode;
    private string? _sourceCode;

    private bool _confirmRecollect;

    private string? _query;
    private string? _queryOf;

    private async Task OnSourceChangedAsync(SourceItem? item)
    {
        Clear();

        if (item is not null)
        {
            await SearchAsync();
        }
    }

    private void Clear()
    {
        _picked = null;
        _query = null;
        _queryOf = null;
    }

    private Task SearchAsync() => LoadAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(_sourceCode))
        {
            throw new ApiException("조회할 소스를 고르십시오.");
        }

        Clear();

        _rows = await Api.ListAsync(_sourceCode);
        return _rows.Count;
    }, "그 소스에 쌓인 Glue 자료가 없습니다. 「재수집」으로 모을 수 있습니다.", "Glue 자료를 읽지 못했습니다");

    /// <summary>
    /// 고른 줄의 쿼리.
    ///
    /// 종류가 <c>sql</c> 인 줄만 본문이 있다. 나머지(<c>proc</c>)는 프로시저
    /// 이름만 있고 본문은 DB 쪽에 있다 — 옛 화면도 그래서 비워 두었다.
    /// </summary>
    private Task PickAsync(ActivityInfoDto? row)
    {
        _picked = row;

        if (row is null || !row.HasQuery)
        {
            _query = null;
            _queryOf = null;
            return Task.CompletedTask;
        }

        _query = row.ActiveContext;
        _queryOf = $"{row.ServiceName} · {row.TransitionName}";

        return Task.CompletedTask;
    }

    /// <summary>
    /// 서버가 파일을 다시 훑어 DB 를 채운다.
    ///
    /// 결과 자체는 비어서 온다(수집만 하고 돌려주지 않는다). 그래서 끝난 뒤
    /// 목록을 <b>다시 읽어</b> 얼마나 쌓였는지 보여 준다 — 안 그러면 사용자가
    /// 무엇이 달라졌는지 알 수 없다.
    /// </summary>
    private async Task RecollectAsync()
    {
        _confirmRecollect = false;

        if (string.IsNullOrWhiteSpace(_sourceCode))
        {
            Say("재수집할 소스를 고르십시오.", NoticeTone.Warning);
            return;
        }

        var ok = await RunAsync(async () =>
        {
            var result = await Client.MdContAsync("md_glue_service", new Dictionary<string, object?>
            {
                ["prj_rid"] = _projectCode ?? string.Empty,
                ["src_rid"] = _sourceCode,
            });

            if (result.ProcCode < 0)
            {
                throw new ApiException(result.Message ?? "재수집하지 못했습니다.");
            }
        }, "재수집했습니다.", "재수집하지 못했습니다");

        if (ok)
        {
            await SearchAsync();
        }
    }
}
