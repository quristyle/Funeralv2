using Microsoft.AspNetCore.Components;
using JSini.Web.Http;
using JSini.Web.Components.Data;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class SourceScanner
{
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

    private ProjMngTable _data = ProjMngTable.Empty;

    private string? _projectCode;
    private string? _sourceCode;

    /// <summary>
    /// 서버가 등록된 소스 경로를 훑는다.
    ///
    /// 훑는 것은 <b>서버 장비의 디스크</b>다. 등록된 경로가 그 장비에 없으면
    /// 아무것도 안 나온다 — 흔한 일이라 오류가 아니라 안내로 보여 준다.
    /// 예전에는 실패를 삼켜 빈 표만 남았고, 그래서 「자료가 없다」와
    /// 「못 훑었다」가 같아 보였다.
    /// </summary>
    private Task ScanAsync() => LoadAsync(async () =>
    {
        var result = await Client.MdContAsync("md_blazor_scan", new Dictionary<string, object?>
        {
            ["prj_rid"] = _projectCode ?? string.Empty,
            ["src_rid"] = _sourceCode ?? string.Empty,
        });

        if (result.ProcCode < 0)
        {
            throw new ApiException(result.Message ?? "소스를 훑지 못했습니다.");
        }

        _data = ProjMngTable.From(result);
        return _data.Table.Rows.Count;
    }, "훑어서 찾은 화면이 없습니다. 「소스 정보」에 경로가 등록돼 있는지 보십시오.", "소스를 훑지 못했습니다");
}
