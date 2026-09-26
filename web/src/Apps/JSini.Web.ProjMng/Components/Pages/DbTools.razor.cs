using Microsoft.AspNetCore.Components;
using JSini.Web.Http;
using JSini.Web.Components.Data;
using JSini.Web.ProjMng.Api;
using System.Data;

namespace JSini.Web.ProjMng.Components.Pages;

public partial class DbTools
{
    [Inject] private ProjMngClient Client { get; set; } = default!;
    [Inject] private ProjectDbClient Dbs { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Of(_projectName, _dbItem?.Name, _keyword);

    /// <summary>
    /// 고른 프로젝트의 <b>이름</b>. 화면이 든 것은 코드뿐이고 목록은
    /// <c>CodeSelect</c> 안에 있어, 그 부품이 <c>@bind-Text</c> 로 올려 준다.
    /// </summary>
    private string? _projectName;

    /// <summary>
    /// 감출 칸. 본문은 한 줄이 수천 글자라 표에 그리면 표를 읽을 수가 없다 —
    /// 본문은 오른쪽 편집기에서 본다. 이름이 둘인 것은 통로마다 대소문자가
    /// 달라서다.
    /// </summary>
    private static readonly string[] BodyColumns = ["routine_definition", "Routine_Definition"];

    /// <summary>프로시저·함수의 이름 칸. 통로마다 이름이 달라 차례로 본다.</summary>
    private static readonly string[] RoutineNameColumns =
        ["procedurename", "proc_name", "procname", "name", "routine_name"];

    /// <summary>
    /// DB 종류별 기본 프로시저 뼈대. 옛 <c>WasmUtil</c> 의 상수를 옮긴 것이다.
    /// EDB 는 PostgreSQL 호환이라 같은 것을 쓴다(옛 화면과 같은 분기).
    /// </summary>
    private const string PostgresTemplate = """
        CREATE OR REPLACE PROCEDURE {schema}.{name}(
          INOUT p_cursor refcursor,
          p_param1 text DEFAULT NULL
        )
        LANGUAGE plpgsql AS $$
        BEGIN
          OPEN p_cursor FOR SELECT 1;
        END;
        $$;
        """;

    private const string MssqlTemplate = """
        CREATE OR ALTER PROCEDURE {schema}.{name}
          @p_param1 nvarchar(100) = NULL
        AS
        BEGIN
          SET NOCOUNT ON;
          -- TODO
        END
        """;

    private string? _projectCode;
    private string? _dbCode;
    private CommonCodeItem? _dbItem;
    private string? _keyword;

    private int _tab;

    private ProjMngTable _tables = ProjMngTable.Empty;
    private ProjMngTable _procs = ProjMngTable.Empty;
    private ProjMngTable _funcs = ProjMngTable.Empty;
    private ProjMngTable _preview = ProjMngTable.Empty;

    private string? _previewOf;

    /// <summary>고른 줄. <b>표에 돌려줘야 강조가 유지된다.</b></summary>
    private DataRowView? _pickedTable;

    /// <inheritdoc cref="_pickedTable" />
    private DataRowView? _pickedRoutine;
    private string? _routineName;
    private string? _routineBody;
    private string? _template;

    private string? DbNick => _dbItem?.Others.GetValueOrDefault("db_nick");
    private string DbType => _dbItem?.Others.GetValueOrDefault("db_type") ?? string.Empty;

    /// <summary>
    /// 개발 도구 통로에 실어 보낼 값들.
    ///
    /// 넷 다 있어야 한다 — 서버가 <c>db</c>(종류)로 등록된 질의를 고르고,
    /// <c>dbnick</c> 으로 접속하고, <c>schema</c> 로 범위를 좁힌다.
    /// </summary>
    private Dictionary<string, object?> Params() => new()
    {
        ["db"] = DbType,
        ["dbnick"] = DbNick ?? string.Empty,
        ["schema"] = _dbItem?.Others.GetValueOrDefault("db_schema") ?? string.Empty,
        ["db_rid"] = _dbCode ?? string.Empty,
        ["param1"] = _keyword ?? string.Empty,
    };

    private void OnProjectChanged()
    {
        _dbCode = null;
        _dbItem = null;
        Clear();
    }

    private async Task OnDbChanged(CommonCodeItem? item)
    {
        _dbItem = item;
        Clear();

        if (item is not null)
        {
            await ReloadAsync();
        }
    }

    private void Clear()
    {
        _tables = ProjMngTable.Empty;
        _procs = ProjMngTable.Empty;
        _funcs = ProjMngTable.Empty;
        _preview = ProjMngTable.Empty;
        _previewOf = null;
        _routineName = null;
        _routineBody = null;
        _template = null;
    }

    private Task ReloadAsync() => LoadAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(DbNick))
        {
            throw new ApiException("DB 를 고르십시오.");
        }

        var parameters = Params();

        // 나란히 부른다. 한쪽이 그 DB 종류에 등록돼 있지 않아도 나머지는
        // 보여 준다 — 「이 종류에는 없다」가 정상인 경우가 있다.
        var tables = Client.JsContAsync("tablelist", parameters);
        var routines = Client.JsContAsync("proclist", parameters);
        var template = LoadTemplateAsync();

        await Task.WhenAll(tables, routines, template);

        _tables = Narrow(ProjMngTable.From(tables.Result), "tablename", "table_name", "name");
        SplitRoutines(routines.Result);

        return _tables.Table.Rows.Count + _procs.Table.Rows.Count + _funcs.Table.Rows.Count;
    }, "이 DB 에서 읽을 수 있는 개체가 없습니다.", "개체 목록을 읽지 못했습니다");

    /// <summary>
    /// 이름으로 좁힌다. <b>서버가 못 거르는 DB 종류가 대부분이라 여기서 한다</b>
    /// (머리말). 이름 칸이 종류마다 달라 몇 가지를 차례로 본다.
    /// </summary>
    /// <remarks>
    /// 거를 값이 없으면 받은 그대로 돌려준다 — 표를 복사할 이유가 없다.
    /// </remarks>
    private ProjMngTable Narrow(ProjMngTable source, params string[] nameColumns)
    {
        if (string.IsNullOrWhiteSpace(_keyword))
        {
            return source;
        }

        var table = source.Table.Clone();

        foreach (DataRow row in source.Table.Rows)
        {
            var name = FirstValue(row, nameColumns) ?? string.Empty;

            if (name.Contains(_keyword, StringComparison.OrdinalIgnoreCase))
            {
                table.ImportRow(row);
            }
        }

        return ProjMngTable.Wrap(table, source.ColumnOrder);
    }

    /// <summary>
    /// <c>proclist</c> 한 번이 프로시저와 함수를 다 준다. 가르는 것은
    /// <c>pgtype</c> — <c>sql</c> 이면 함수다.
    ///
    /// 그 칸이 없는 DB 종류에서는 <b>가르지 않는다.</b> 잘못 갈라 한쪽 탭이
    /// 통째로 비면 「이 DB 에는 프로시저가 없다」로 읽힌다.
    /// </summary>
    private void SplitRoutines(ProjMngResult result)
    {
        var rows = result.Rows ?? [];

        var hasKind = rows.Any(r =>
            r.Keys.Any(k => string.Equals(k, "pgtype", StringComparison.OrdinalIgnoreCase)));

        if (!hasKind)
        {
            _procs = Narrow(ProjMngTable.From(result), RoutineNameColumns);
            _funcs = ProjMngTable.Empty;
            return;
        }

        static bool IsFunction(ProjMngRow row) =>
            string.Equals(Value(row, "pgtype"), "sql", StringComparison.OrdinalIgnoreCase);

        _procs = Narrow(ProjMngTable.From(new ProjMngResult
        {
            Cols = result.Cols,
            Rows = [.. rows.Where(r => !IsFunction(r))],
        }), RoutineNameColumns);

        _funcs = Narrow(ProjMngTable.From(new ProjMngResult
        {
            Cols = result.Cols,
            Rows = [.. rows.Where(IsFunction)],
        }), RoutineNameColumns);
    }

    /// <summary>
    /// 프로시저 생성 뼈대. DB 속성에 등록된 것이 우선이고, 없으면 종류별 기본.
    ///
    /// 없다고 실패로 만들지 않는다 — 이 탭이 비는 것과 화면 전체가 실패하는
    /// 것은 다른 일이다.
    /// </summary>
    private async Task LoadTemplateAsync()
    {
        // **프로시저를 부르지 않는다.** 옛 길은 `sp_dev_db_prop_exec` 였고
        // 지금은 그 표를 REST 로 읽는다(`ProjectDbClient`).
        var saved = int.TryParse(_dbCode, out var dbRid)
            ? await Dbs.PropsAsync(dbRid)
            : [];

        var registered = saved.FirstOrDefault(p =>
            string.Equals(p.DbPkey, "sp_fmt", StringComparison.OrdinalIgnoreCase))?.DbPvalue
            ?? string.Empty;

        _template = !string.IsNullOrWhiteSpace(registered)
            ? registered
            : DbType.ToUpperInvariant() switch
            {
                "MSSQL" => MssqlTemplate,
                _ => PostgresTemplate,
            };
    }

    /// <summary>
    /// 고른 테이블의 앞 10건.
    ///
    /// 등록된 질의가 아니라 문장을 만들어 보낸다. 그래서 이름을 **서버가 준
    /// 것에서만** 가져온다 — 사용자가 친 글자를 넣지 않는다.
    /// </summary>
    private Task PreviewAsync(DataRowView? picked)
    {
        _pickedTable = picked;

        var row = RuntimeCell.Row(picked);
        var name = row is null ? null : FirstValue(row, "tablename", "table_name", "name");

        if (name is null)
        {
            _previewOf = null;
            _preview = ProjMngTable.Empty;
            return Task.CompletedTask;
        }

        return LoadAsync(async () =>
        {
            // 스키마도 서버가 준 것을 쓴다. 목록에 실려 오면 그것이 정확하다 —
            // DB 설정의 기본 스키마와 다른 자리에 있는 표가 있다.
            var schema = FirstValue(row!, "nspname", "schemaname")
                         ?? _dbItem?.Others.GetValueOrDefault("db_schema");

            var qualified = string.IsNullOrWhiteSpace(schema) ? name : $"{schema}.{name}";

            // 문법이 종류마다 다르다. 옛 화면은 오라클 문법(`rownum`)만 있었고,
            // 실제 대상이 PostgreSQL·MSSQL 이라 그 둘로 나눈다.
            var sql = DbType.Equals("MSSQL", StringComparison.OrdinalIgnoreCase)
                ? $"SELECT TOP 10 * FROM {qualified}"
                : $"SELECT * FROM {qualified} LIMIT 10";

            var result = await Client.RawSqlAsync(DbNick!, sql);

            if (result.ProcCode < 0)
            {
                throw new ApiException(result.Message ?? "미리보기를 읽지 못했습니다.");
            }

            _previewOf = qualified;
            _preview = ProjMngTable.From(result);

            return _preview.Table.Rows.Count;
        }, "그 테이블에 자료가 없습니다.", "미리보기를 읽지 못했습니다");
    }

    /// <summary>고른 프로시저·함수의 본문. 목록에 이미 실려 오면 그것을 쓴다.</summary>
    private Task ShowRoutineAsync(DataRowView? picked)
    {
        _pickedRoutine = picked;

        var row = RuntimeCell.Row(picked);

        if (row is null)
        {
            _routineName = null;
            _routineBody = null;
            return Task.CompletedTask;
        }

        _routineName = FirstValue(row, "procedurename", "proc_name", "procname", "name", "routine_name");
        _routineBody = FirstValue(row, "routine_definition", "proc_body", "definition", "src", "text");

        if (_routineBody is null && _routineName is not null)
        {
            // 목록에 본문이 없으면 따로 물어본다. 이 액션이 없는 DB 종류가 있다.
            return LoadAsync(async () =>
            {
                var parameters = Params();
                parameters["param1"] = _routineName;

                var result = await Client.JsContAsync("procInfo", parameters);
                var table = ProjMngTable.From(result);

                _routineBody = table.Table.Rows.Count > 0
                    ? FirstValue(table.Table.Rows[0], "routine_definition", "proc_body", "definition", "src", "text")
                    : null;

                return _routineBody is null ? 0 : 1;
            }, "이 DB 종류에서는 본문을 읽을 수 없습니다.", "본문을 읽지 못했습니다");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 여러 이름 중 먼저 있는 칸의 값.
    ///
    /// DB 종류마다 등록된 질의가 달라 <b>칸 이름이 제각각</b>이다. 하나만
    /// 보고 있으면 그 종류에서만 빈칸이 되는데, 원인이 화면에서 안 보인다.
    /// </summary>
    private static string? FirstValue(DataRow row, params string[] columns)
    {
        foreach (var column in columns)
        {
            if (row.Table.Columns.Contains(column) && row[column] is not DBNull and var value)
            {
                var text = value?.ToString();

                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        return null;
    }

    /// <summary>와이어에서 온 행의 값. 칸 이름의 대소문자를 가리지 않는다.</summary>
    private static string Value(ProjMngRow row, string column)
    {
        foreach (var (key, value) in row)
        {
            if (string.Equals(key, column, StringComparison.OrdinalIgnoreCase))
            {
                return value?.ToString() ?? string.Empty;
            }
        }

        return string.Empty;
    }
}
