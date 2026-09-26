using Microsoft.AspNetCore.Components;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class DbLogicView
{
    [Inject] private DbLogicClient Api { get; set; } = default!;

    /// <summary>
    /// 고를 수 있는 DB 종류. <b>서버가 접속을 만들 때 보는 값과 같아야 한다</b>
    /// (`ConstInfo.DbTypes` + 실제 등록된 `EDB`).
    /// </summary>
    private static readonly string[] DbTypes = ["POSTGRESQL", "MSSQL", "MYSQL", "EDB"];

    private IReadOnlyList<DbLogicBaseDto> _bases = [];
    private IReadOnlyList<DbLogicQueryDto> _queries = [];

    private DbLogicBaseDto? _base;

    /// <summary>표에 보여 줄 질의의 앞머리. 여러 줄이면 첫 줄만.</summary>
    private static string Head(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var line = value.Split('\n').FirstOrDefault(l => l.Trim().Length > 0)?.Trim() ?? string.Empty;
        return line.Length <= 90 ? line : line[..90] + "…";
    }

    protected override Task OnInitializedAsync() => SearchAsync();

    private Task SearchAsync() => LoadAsync(async () =>
    {
        _bases = await Api.ListAsync();

        // 고른 이름표가 사라졌으면 오른쪽을 비운다.
        if (_base is not null && _bases.All(b => b.DslCd != _base.DslCd))
        {
            _base = null;
            _queries = [];
        }

        return _bases.Count;
    }, "등록된 이름표가 없습니다.", "이름표를 읽지 못했습니다");

    private Task PickBaseAsync(DbLogicBaseDto? item)
    {
        _base = item;

        if (item is null)
        {
            _queries = [];
            return Task.CompletedTask;
        }

        return LoadQueriesAsync();
    }

    private Task LoadQueriesAsync() => _base is null ? Task.CompletedTask : LoadAsync(async () =>
    {
        _queries = await Api.QueriesAsync(_base.DslCd);
        return _queries.Count;
    }, "이 이름표에 등록된 질의가 없습니다.", "질의를 읽지 못했습니다");

    private Task SaveBaseAsync((DbLogicBaseDto Item, bool IsNew) e) => Api.SaveAsync(e.Item);

    /// <summary>이름표 삭제. 딸린 질의가 있으면 서버가 막는다(머리말).</summary>
    private async Task DeleteBaseAsync(DbLogicBaseDto b)
    {
        await Api.DeleteAsync(b.DslCd);

        if (_base?.DslCd == b.DslCd)
        {
            _base = null;
            _queries = [];
        }
    }

    /// <summary>새 질의는 <b>고른 이름표에 붙는다.</b> 서버도 경로로 한 번 더 정한다.</summary>
    private void FillNewQuery(DbLogicQueryDto q) => q.DslCd = _base?.DslCd;

    private async Task SaveQueryAsync((DbLogicQueryDto Item, bool IsNew) e)
    {
        if (_base is null)
        {
            return;
        }

        if (e.IsNew)
        {
            await Api.CreateQueryAsync(_base.DslCd, e.Item);
        }
        else
        {
            await Api.UpdateQueryAsync(_base.DslCd, e.Item);
        }

        // 왼쪽의 「질의」 수가 방금 바뀌었다.
        await SearchAsync();
    }

    private async Task DeleteQueryAsync(DbLogicQueryDto q)
    {
        if (_base is null)
        {
            return;
        }

        await Api.DeleteQueryAsync(_base.DslCd, q.DslId);
        await SearchAsync();
    }
}
