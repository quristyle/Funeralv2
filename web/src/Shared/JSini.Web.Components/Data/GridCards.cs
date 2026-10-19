using DevExpress.Blazor;

namespace JSini.Web.Components.Data;

/// <summary>휴대폰에서 표를 카드로 펴는 약속(표시 클래스와 셀의 칸 이름). 펴는 일은 CSS 가 하고(app.css 의 .commgrd--cards) 이 클래스는 그 이름을 글자로 맞잡는다 — 한쪽만 고치면 표가 이름 없는 카드가 되는데 오류는 안 난다(GridCardTests 가 지킨다). 부르는 자리는 CommGrd 하나다.</summary>
public static class GridCards
{
    /// <summary>표에 다는 표시. 이 값이 CSS 규칙의 열쇠다.</summary>
    public const string CssClass = "commgrd--cards";

    /// <summary>CSS 가 <c>content: attr(...)</c> 로 꺼내 쓰는 속성 이름.</summary>
    public const string CaptionAttribute = "data-caption";

    /// <summary>카드로 펼 때 값 앞에 세울 칸 이름을 셀에 적는다(CustomizeElement 안에서 부른다). 이름이 없는 칸(체크 칸처럼 자리로 말하는 칸)에는 적지 않는다 — CSS 가 첫 값 칸을 카드 제목으로 삼으므로 손잡이 칸에 이름을 적으면 제목이 빈 칸으로 밀린다.</summary>
    public static void Label(GridCustomizeElementEventArgs e)
    {
        if (e.ElementType != GridElementType.DataCell || e.Column is null)
        {
            return;
        }

        var caption = e.Column.Caption;

        if (string.IsNullOrWhiteSpace(caption) && e.Column is IGridDataColumn data)
        {
            caption = data.FieldName;
        }

        if (!string.IsNullOrWhiteSpace(caption))
        {
            e.Attributes[CaptionAttribute] = caption;
        }
    }
}
