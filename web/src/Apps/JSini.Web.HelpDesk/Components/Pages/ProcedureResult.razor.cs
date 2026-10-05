using Microsoft.AspNetCore.Components;
using System.Data;
using System.Text.Json;
using JSini.Web.Components.Data;
using JSini.Web.HelpDesk.Api;

namespace JSini.Web.HelpDesk.Components.Pages;

public partial class ProcedureResult
{
    /// <summary>목록에서 떼고 보여 주는 공통 접두어. 옛 화면과 같다.</summary>
    private const string NamePrefix = "P_QURI_";

    [Inject] private OadrApi Oadr { get; set; } = default!;

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
        public string? Description { get; init; }
        public int MaxLength { get; init; }
        public string? Value { get; set; }
    }

    private IReadOnlyList<ProcedureInfo> _procedures = [];
    private IReadOnlyList<ProcedureParameter> _parameters = [];

    /// <summary>
    /// 목록을 거르는 낱말. 거르는 일은 브라우저 안에서 끝난다 —
    /// 52건짜리 목록이라 서버에 다시 물을 값어치가 없다.
    /// </summary>
    private string? _keyword;

    private string? _selected;
    private DataTable _rows = JsonTable.Empty;

    private string? Description =>
        _procedures.FirstOrDefault(p => string.Equals(p.Name, _selected, StringComparison.Ordinal))?.Description;

    /// <summary>
    /// 낱말로 거른 목록. 이름과 설명을 모두 본다 — 옛 화면도 그랬다.
    /// 프로시저 이름이 영문이라 대소문자를 가리지 않는다.
    /// </summary>
    private IReadOnlyList<ProcedureInfo> Shown
    {
        get
        {
            var keyword = _keyword?.Trim();

            if (string.IsNullOrEmpty(keyword))
            {
                return _procedures;
            }

            return [.. _procedures.Where(p =>
                p.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || (p.Description?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false))];
        }
    }

    /// <summary>
    /// 왼쪽 판 머리의 건수. 거른 목록이 있으면 <b>거른 뒤의 수</b>가 지금
    /// 보고 있는 것이라 그쪽을 함께 적는다(<c>CommGrd</c> 머리줄과 같은 규칙).
    /// </summary>
    private string ListHint
    {
        get
        {
            var total = _procedures.Count;
            var shown = Shown.Count;

            return shown == total ? $"{total}개" : $"{total}개 중 {shown}개";
        }
    }

    /// <summary>
    /// 오른쪽 판 머리. 온전한 프로시저 이름과 <b>총</b> 건수다.
    /// 표 안의 검색칸으로 거르면 보이는 줄은 줄지만 이 수는 그대로다 —
    /// 「총」을 붙여 두지 않으면 그때 조용히 틀린 수가 된다.
    /// </summary>
    private string ResultHint =>
        _rows.Rows.Count == 0 ? _selected ?? string.Empty : $"{_selected} · 총 {_rows.Rows.Count}건";

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
                    Description = Text(row, "ParamDescription"),
                    MaxLength = Number(row, "MaxLength"),
                });
            }
        }

        _procedures = [.. map.Values.OrderBy(p => p.Name, StringComparer.Ordinal)];

        // 목록을 다시 읽으면 고른 프로시저가 사라졌을 수 있다. 그대로 두면
        // 오른쪽 판이 없는 프로시저의 인자 칸을 들고 남는다.
        if (_selected is { Length: > 0 } && !map.ContainsKey(_selected))
        {
            Pick(null);
        }

        return _procedures.Count;
    }, "돌릴 수 있는 프로시저가 없습니다.", "프로시저 목록을 읽지 못했습니다");

    /// <summary>목록에서 고른 줄인가. 왼쪽 판이 강조에 쓴다.</summary>
    private bool IsPicked(string name) => string.Equals(name, _selected, StringComparison.Ordinal);

    /// <summary>
    /// 목록에는 공통 접두어를 떼고 보여 준다(원본과 같다). 온전한 이름은
    /// 줄의 <c>title</c> 과 오른쪽 판 머리에 남는다.
    /// </summary>
    private static string ShortName(string name) =>
        name.StartsWith(NamePrefix, StringComparison.Ordinal) ? name[NamePrefix.Length..] : name;

    /// <summary>
    /// 인자 칸 아래 옅게 붙는 말. 설명·형·길이를 순서대로 잇는다.
    /// <b>값을 막는 데 쓰지 않는다</b> — 화면이 형을 따지지 않는 이유는
    /// razor 머리말에 있다.
    /// </summary>
    private static string ParamHint(ProcedureParameter parameter)
    {
        var type = parameter.DataType is { Length: > 0 } dataType
            ? parameter.MaxLength > 0 ? $"{dataType}({parameter.MaxLength})" : dataType
            : null;

        return string.Join(" · ", new[] { parameter.Description, type }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    /// <summary>고른 프로시저의 인자 칸을 갈아 끼운다. 앞서 넣은 값은 버린다.</summary>
    private void Pick(string? name)
    {
        _selected = name;
        _rows = JsonTable.Empty;

        var info = _procedures.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.Ordinal));

        _parameters = info is null
            ? []
            : [.. info.Parameters.Select(p => new ProcedureParameter
            {
                Name = p.Name,
                DataType = p.DataType,
                Description = p.Description,
                MaxLength = p.MaxLength,
            })];
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

    /// <summary>칸이 없거나 수가 아니면 0. 길이는 힌트로만 쓰므로 그걸로 충분하다.</summary>
    private static int Number(DataRow row, string column) =>
        int.TryParse(Text(row, column), out var value) ? value : 0;
}
