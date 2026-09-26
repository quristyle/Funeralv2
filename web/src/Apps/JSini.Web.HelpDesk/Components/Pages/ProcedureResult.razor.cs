using Microsoft.AspNetCore.Components;
using System.Data;
using System.Text.Json;
using JSini.Web.Components.Data;
using JSini.Web.HelpDesk.Api;

namespace JSini.Web.HelpDesk.Components.Pages;

public partial class ProcedureResult
{
    [Inject] private OadrApi Oadr { get; set; } = default!;

    /// <summary>접힌 조회줄에 적을 지금 조건(<c>CommSch.MobileSummary</c>).</summary>
    private string ConditionSummary => SchSummary.Or(_selected, "프로시저를 고르세요");

    /// <summary>돌릴 수 있는 프로시저 하나와 그 인자들.</summary>
    private sealed class ProcedureInfo
    {
        public string Name { get; init; } = string.Empty;
        public string? Description { get; set; }
        public List<ProcedureParameter> Parameters { get; } = [];
    }

    /// <summary>인자 한 칸. 값은 사람이 채운다.</summary>
    private sealed class ProcedureParameter
    {
        public string Name { get; init; } = string.Empty;
        public string? DataType { get; init; }
        public string? Value { get; set; }
    }

    private IReadOnlyList<ProcedureInfo> _procedures = [];
    private IReadOnlyList<ProcedureParameter> _parameters = [];

    private string? _selected;
    private DataTable _rows = JsonTable.Empty;

    private string? Description =>
        _procedures.FirstOrDefault(p => string.Equals(p.Name, _selected, StringComparison.Ordinal))?.Description;

    protected override Task OnInitializedAsync() => ReloadAsync();

    /// <summary>
    /// 돌릴 수 있는 프로시저 목록. <b>한 프로시저가 인자 수만큼 줄로 온다</b> —
    /// 이름으로 묶어 인자를 모은다(원본과 같은 처리).
    /// </summary>
    private Task ReloadAsync() => LoadAsync(async () =>
    {
        var raw = await Oadr.ExecuteProcedureAsync<JsonElement>("P_QURI_PROC");
        var table = JsonTable.From(raw);

        var map = new Dictionary<string, ProcedureInfo>(StringComparer.Ordinal);

        foreach (DataRow row in table.Rows)
        {
            var name = Text(row, "ProcedureName");

            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (!map.TryGetValue(name, out var info))
            {
                info = new ProcedureInfo { Name = name, Description = Text(row, "ProcDescription") };
                map[name] = info;
            }

            var parameter = Text(row, "ParameterName");

            if (!string.IsNullOrWhiteSpace(parameter))
            {
                info.Parameters.Add(new ProcedureParameter
                {
                    Name = parameter,
                    DataType = Text(row, "DataType"),
                });
            }
        }

        _procedures = [.. map.Values.OrderBy(p => p.Name, StringComparer.Ordinal)];

        return _procedures.Count;
    }, "돌릴 수 있는 프로시저가 없습니다.", "프로시저 목록을 읽지 못했습니다");

    /// <summary>고른 프로시저의 인자 칸을 갈아 끼운다. 앞서 넣은 값은 버린다.</summary>
    private void Pick(string? name)
    {
        _selected = name;
        _rows = JsonTable.Empty;

        var info = _procedures.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.Ordinal));

        _parameters = info is null
            ? []
            : [.. info.Parameters.Select(p => new ProcedureParameter { Name = p.Name, DataType = p.DataType })];
    }

    private Task RunAsync() => LoadAsync(async () =>
    {
        if (_selected is not { Length: > 0 })
        {
            return -1;
        }

        var parameters = _parameters
            .Select(p => new OadrApi.OadrParameter(p.Name, p.Value ?? string.Empty))
            .ToList();

        var raw = await Oadr.ExecuteProcedureAsync<JsonElement>(_selected, parameters);
        _rows = JsonTable.From(raw);

        return _rows.Rows.Count;
    }, "결과가 없습니다.", "프로시저를 돌리지 못했습니다");

    /// <summary>칸이 없으면 빈 글자. 프로시저마다 주는 칸이 다르다.</summary>
    private static string? Text(DataRow row, string column) =>
        row.Table.Columns.Contains(column) ? row[column]?.ToString() : null;
}
