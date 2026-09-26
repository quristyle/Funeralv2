using Microsoft.AspNetCore.Components;
using JSini.Web.Http;
using JSini.Web.Components.Data;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class DbTester
{
    [Inject] private ProjMngClient Client { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(_projectName, _dbItem?.Name);

    /// <summary>
    /// 고른 프로젝트의 <b>이름</b>. 화면이 든 것은 코드뿐이고 목록은
    /// <c>CodeSelect</c> 안에 있어, 그 부품이 <c>@bind-Text</c> 로 올려 준다.
    /// </summary>
    private string? _projectName;

    private string? _projectCode;
    private string? _dbCode;
    private CommonCodeItem? _dbItem;

    private string _query = "SELECT 1 AS TEST;";
    private ProjMngTable _result = ProjMngTable.Empty;

    /// <summary>대상 DB 의 별칭. 서버가 이 값으로 접속 정보를 찾는다.</summary>
    private string? DbNick => _dbItem?.Others.GetValueOrDefault("db_nick");

    private void OnProjectChanged()
    {
        _dbCode = null;
        _dbItem = null;
        _result = ProjMngTable.Empty;
    }

    private Task ExecuteAsync() => LoadAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(DbNick))
        {
            throw new ApiException("실행할 DB 를 고르십시오.");
        }

        if (string.IsNullOrWhiteSpace(_query))
        {
            throw new ApiException("실행할 문장을 넣으십시오.");
        }

        var result = await Client.RawSqlAsync(DbNick, _query);
        _result = ProjMngTable.From(result);

        return _result.Table.Rows.Count;
    }, "결과가 없습니다.", "쿼리를 실행하지 못했습니다");
}
