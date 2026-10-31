using DevExpress.Blazor;
using DevExpress.Data.Filtering;
using Microsoft.AspNetCore.Components;

namespace JSini.Web.ProjMng.Components.Shared;

/// <summary>체크 목록 한 줄. 값은 칸에 적힌 그대로, 글자는 사람이 읽는 이름이다.</summary>
public sealed record GridCheckFilterOption(string Value, string Text);

public partial class GridCheckFilter
{
    /// <summary>「빈값」 줄의 값. 칸에 실제로 들어갈 수 없는 글자라 진짜 값과 겹치지 않는다.</summary>
    public const string BlankValue = "\u0000";

    public const string BlankText = "(빈값)";

    [Parameter, EditorRequired] public GridDataColumnFilterRowCellTemplateContext Filter { get; set; } = default!;

    [Parameter] public IReadOnlyList<GridCheckFilterOption> Options { get; set; } = [];

    /// <summary>값 목록을 고르개 줄로 만든다. 빈 값(공백뿐인 것 포함)은 맨 앞 「빈값」 한 줄로 모은다.</summary>
    public static IReadOnlyList<GridCheckFilterOption> Collect(IEnumerable<string?> values, Func<string, string> text)
    {
        var list = values.ToList();
        var named = list
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!)
            .Distinct(StringComparer.Ordinal)
            .Select(v => new GridCheckFilterOption(v, text(v)))
            .OrderBy(o => o.Text, StringComparer.CurrentCulture);

        return list.Any(string.IsNullOrWhiteSpace)
            ? [new GridCheckFilterOption(BlankValue, BlankText), .. named]
            : [.. named];
    }

    /// <summary>체크한 값들을 「그중 하나와 같다 또는 비어 있다」로 만든다. 고른 것이 없으면 null(거르지 않음).</summary>
    public static CriteriaOperator? BuildCriteria(string field, IEnumerable<string>? picked)
    {
        var all = picked?.Distinct(StringComparer.Ordinal).ToArray() ?? [];
        var named = all.Where(v => v != BlankValue).ToArray();

        CriteriaOperator? inOp = named.Length == 0 ? null : new InOperator(field, named);

        // 공백뿐인 값도 목록에서 「빈값」으로 묶었으니 거를 때도 Trim 해서 본다.
        CriteriaOperator? blankOp = all.Contains(BlankValue)
            ? new FunctionOperator(FunctionOperatorType.IsNullOrEmpty,
                new FunctionOperator(FunctionOperatorType.Trim, new OperandProperty(field)))
            : null;

        return CriteriaOperator.Or(inOp, blankOp);
    }

    /// <summary>칸에 걸린 기준에서 체크한 값을 되읽는다. 값을 따로 들고 있지 않는다 — 표가 기준을 지우면 체크도 함께 풀려야 한다.</summary>
    public static IReadOnlyList<string> ReadPicked(CriteriaOperator? criteria) => criteria switch
    {
        InOperator op => [.. op.Operands.OfType<OperandValue>().Select(o => o.Value as string).OfType<string>()],
        BinaryOperator { RightOperand: OperandValue { Value: string v } } => [v],
        FunctionOperator { OperatorType: FunctionOperatorType.IsNullOrEmpty } => [BlankValue],
        GroupOperator { OperatorType: GroupOperatorType.Or } g => [.. g.Operands.SelectMany(ReadPicked)],
        _ => [],
    };

    private IReadOnlyList<string> Picked => ReadPicked(Filter.FilterCriteria);

    /// <summary>칸 값. 고른 것이 없으면 null 이라야 「전체」가 뜨고 지우기 단추가 숨는다.</summary>
    private object? BoxValue => Picked.Count == 0 ? null : string.Join('\u001f', Picked);

    private string BoxText =>
        string.Join(", ", Picked.Select(v => Options.FirstOrDefault(o => o.Value == v)?.Text
                                              ?? (v == BlankValue ? BlankText : v)));

    /// <summary>칸 값이 바뀌는 길은 지우기 단추뿐이다. null 일 때만 체크를 전부 푼다.</summary>
    private void OnBoxValueChanged(object? value)
    {
        if (value is null)
        {
            Apply([]);
        }
    }

    private void Apply(IEnumerable<string>? values) =>
        Filter.FilterCriteria = BuildCriteria(Filter.DataColumn.FieldName, values);
}
