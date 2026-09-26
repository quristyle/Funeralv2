using Microsoft.AspNetCore.Components;
using JSini.Web.Http;
using JSini.Web.Components.Data;
using JSini.Web.Components.Layout;
using JSini.Web.ProjMng.Api;
using System.Data;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class TableManage
{
    [Inject] private ProjMngClient Client { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(_projectName, _dbItem?.Name, _keyword);

    /// <summary>
    /// 고른 프로젝트의 <b>이름</b>. 화면이 든 것은 코드뿐이고 목록은
    /// <c>CodeSelect</c> 안에 있어, 그 부품이 <c>@bind-Text</c> 로 올려 준다.
    /// </summary>
    private string? _projectName;

    private string? _projectCode;
    private string? _dbCode;
    private CommonCodeItem? _dbItem;
    private string? _keyword;

    private string? _selectedTable;

    /// <summary>고른 줄. <b>표에 돌려줘야 강조가 유지된다.</b></summary>
    private DataRowView? _pickedTable;

    /// <summary>
    /// 칸 안에서 고칠 수 있는 칸. 설명 하나뿐이다 — 나머지는 대상 DB 의
    /// 사실이라 여기서 바꿀 수 있는 것이 아니다.
    /// </summary>
    private static readonly string[] DescColumns = ["description", "Description"];

    /// <summary>서버가 준 전량. 이름 거르기는 화면에서 한다.</summary>
    private ProjMngTable _allTables = ProjMngTable.Empty;
    private ProjMngTable _tables = ProjMngTable.Empty;
    private ProjMngTable _columns = ProjMngTable.Empty;

    private string? DbNick => _dbItem?.Others.GetValueOrDefault("db_nick");

    /// <summary>
    /// 개발 도구 통로가 접속을 찾는 데 쓰는 값.
    ///
    /// <c>schema</c> 는 <b>보내지 않는다</b> — 서버가 접속 정보에서 채운다.
    /// 보내도 덮어써진다.
    /// </summary>
    private Dictionary<string, object?> BaseParams() => new()
    {
        ["db"] = _dbItem?.Others.GetValueOrDefault("db_type") ?? string.Empty,
        ["dbnick"] = DbNick ?? string.Empty,
        ["db_rid"] = _dbCode ?? string.Empty,
    };

    private void OnProjectChanged()
    {
        _dbCode = null;
        _dbItem = null;
        Clear();
    }

    private async Task OnDbChangedAsync(CommonCodeItem? item)
    {
        _dbItem = item;
        Clear();

        if (item is not null)
        {
            await SearchAsync();
        }
    }

    private void Clear()
    {
        _allTables = ProjMngTable.Empty;
        _tables = ProjMngTable.Empty;
        _columns = ProjMngTable.Empty;
        _selectedTable = null;
    }

    private Task SearchAsync() => LoadAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(DbNick))
        {
            throw new ApiException("DB 를 고르십시오.");
        }

        _selectedTable = null;
        _columns = ProjMngTable.Empty;

        var result = await Client.JsContAsync("tablelist", BaseParams());

        if (result.ProcCode < 0)
        {
            throw new ApiException(result.Message ?? "테이블 목록을 읽지 못했습니다.");
        }

        _allTables = ProjMngTable.From(result);
        ApplyKeyword();

        return _tables.Table.Rows.Count;
    }, "이 DB 에서 읽을 수 있는 테이블이 없습니다.", "테이블 목록을 읽지 못했습니다");

    /// <summary>
    /// 이름으로 거른다.
    ///
    /// 등록된 질의에 이름 조건이 없어서 서버가 걸러 줄 수가 없다.
    /// 표 하나를 통째로 다시 만드는 대신 걸러낸 행만 새 표에 옮긴다.
    /// </summary>
    private void ApplyKeyword()
    {
        var keyword = _keyword?.Trim();

        if (string.IsNullOrEmpty(keyword))
        {
            _tables = _allTables;
            return;
        }

        var table = _allTables.Table.Clone();

        foreach (DataRow row in _allTables.Table.Rows)
        {
            var name = Cell(row, "tablename");

            if (name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                table.ImportRow(row);
            }
        }

        table.AcceptChanges();
        _tables = ProjMngTable.Wrap(table, _allTables.ColumnOrder);
    }

    private Task OnTableSelectAsync(DataRowView? picked)
    {
        _pickedTable = picked;

        var row = RuntimeCell.Row(picked);
        _selectedTable = row is null ? null : Cell(row, "tablename");

        if (string.IsNullOrEmpty(_selectedTable))
        {
            _columns = ProjMngTable.Empty;
            return Task.CompletedTask;
        }

        return LoadAsync(async () =>
        {
            var parameters = BaseParams();
            parameters["table_name"] = _selectedTable;

            var result = await Client.JsContAsync("columnsOftable", parameters);

            if (result.ProcCode < 0)
            {
                throw new ApiException(result.Message ?? "컬럼을 읽지 못했습니다.");
            }

            _columns = ProjMngTable.From(result);
            return _columns.Table.Rows.Count;
        }, "그 테이블에 컬럼이 없습니다.", "컬럼을 읽지 못했습니다");
    }

    /// <summary>
    /// 고친 테이블 설명을 대상 DB 에 반영한다.
    ///
    /// 한 줄씩 보낸다 — 등록된 질의가 한 번에 하나만 바꾸게 되어 있다.
    /// 중간에 실패하면 그 앞까지는 이미 반영돼 있으므로, 끝난 뒤 다시 읽어
    /// **실제로 무엇이 바뀌었는지**를 보여 준다.
    /// </summary>
    private async Task SaveTableCommentAsync()
    {
        var changed = _tables.ChangedRows();

        if (changed.Count == 0)
        {
            Say("고친 것이 없습니다.", NoticeTone.Info);
            return;
        }

        var done = await RunAsync(async () =>
        {
            foreach (var row in changed)
            {
                var parameters = BaseParams();
                parameters["table_name"] = Value(row, "tablename");
                parameters["table_desc"] = Quote(Value(row, "description"));

                var result = await Client.JsContAsync("tableCommentUpdate", parameters);

                if (result.ProcCode < 0)
                {
                    throw new ApiException(result.Message ?? "설명을 반영하지 못했습니다.");
                }
            }
        }, $"{changed.Count}개 테이블 설명을 반영했습니다.", "설명을 반영하지 못했습니다");

        if (done)
        {
            // **다시 읽고 나서 한 번 더 말한다.** 조회가 자기 안내를 쓰기
            // 때문에, 그냥 두면 「반영했습니다」가 뜨자마자 지워진다.
            await SearchAsync();
            Say($"{changed.Count}개 테이블 설명을 반영했습니다.", NoticeTone.Info);
        }
    }

    /// <summary>
    /// 고친 컬럼 설명을 반영한다.
    ///
    /// 코멘트가 **없던 컬럼**은 갱신이 아니라 추가여야 하는 DB 가 있다.
    /// 갱신이 실패하면 추가를 한 번 더 시도한다 — 옛 화면과 같은 판단이다.
    /// </summary>
    private async Task SaveColumnCommentAsync()
    {
        if (string.IsNullOrEmpty(_selectedTable))
        {
            return;
        }

        var changed = _columns.ChangedRows();

        if (changed.Count == 0)
        {
            Say("고친 것이 없습니다.", NoticeTone.Info);
            return;
        }

        var done = await RunAsync(async () =>
        {
            foreach (var row in changed)
            {
                var parameters = BaseParams();
                parameters["table_name"] = _selectedTable;

                // 서버가 준 칸 이름에 오타가 있다(`colunmname`). 고치면 이쪽이
                // 깨지므로 둘 다 본다.
                parameters["column_name"] = Value(row, "colunmname", "columnname");
                parameters["column_desc"] = Quote(Value(row, "description"));

                var result = await Client.JsContAsync("columnsCommentUpdate", parameters);

                if (result.ProcCode < 0)
                {
                    result = await Client.JsContAsync("columnsCommentAdd", parameters);
                }

                if (result.ProcCode < 0)
                {
                    throw new ApiException(result.Message ?? "설명을 반영하지 못했습니다.");
                }
            }
        }, $"{changed.Count}개 컬럼 설명을 반영했습니다.", "설명을 반영하지 못했습니다");

        if (done && _selectedTable is not null)
        {
            var parameters = BaseParams();
            parameters["table_name"] = _selectedTable;

            _columns = ProjMngTable.From(await Client.JsContAsync("columnsOftable", parameters));

            // 다시 읽은 뒤에 한 번 더 말한다(위와 같은 이유).
            Say($"{changed.Count}개 컬럼 설명을 반영했습니다.", NoticeTone.Info);
        }
    }

    /// <summary>
    /// 작은따옴표를 두 개로. 코멘트 질의가 값을 <b>문자열로 그대로 끼워 넣기</b>
    /// 때문이다 — 하나만 들어가도 질의가 깨지고, 그 자리에 다른 문장을 넣을 수도 있다.
    /// </summary>
    private static string Quote(string text) => text.Replace("'", "''");

    private static string Cell(DataRow row, string column) =>
        row.Table.Columns.Contains(column) ? row[column]?.ToString() ?? string.Empty : string.Empty;

    private static string Value(Dictionary<string, object?> row, params string[] names)
    {
        foreach (var name in names)
        {
            foreach (var (key, value) in row)
            {
                if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
                {
                    return value?.ToString() ?? string.Empty;
                }
            }
        }

        return string.Empty;
    }
}
