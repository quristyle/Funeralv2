using DevExpress.Blazor;

namespace JSini.Web.Components.Data;

/// <summary>
/// 휴대폰에서 <b>표를 카드로 펴는</b> 약속을 담는다.
///
/// <para>
/// 펴는 일은 전부 CSS 가 한다(<c>app.css</c> 의 <c>.commgrd--cards</c> ·
/// <c>@media (max-width: 767px)</c>). 코드가 할 일은 둘뿐이다 —
/// 표에 <see cref="CssClass"/> 를 달고, 셀마다 칸 이름을 적어 두는 것
/// (<see cref="Label"/>). 머리줄을 감추므로 그 이름이 아니면 어느 값인지
/// 알 길이 없다.
/// </para>
///
/// <para>
/// <b>한 곳에 모아 둔 까닭</b>은 표를 그리는 자리가 셋이라서다
/// (<c>CommGrd</c> · 헬프데스크 <c>AutoGrid</c> · 뉴스속보의 맨 <c>DxGrid</c>).
/// 클래스 이름이나 속성 이름을 자리마다 적으면 한쪽만 고치는 날이 오고,
/// 그때 증상은 <b>그 표만 이름 없는 카드가 되는 것</b>이라 오류가 안 난다.
/// </para>
/// </summary>
public static class GridCards
{
    /// <summary>표에 다는 표시. 이 값이 CSS 규칙의 열쇠다.</summary>
    public const string CssClass = "commgrd--cards";

    /// <summary>CSS 가 <c>content: attr(...)</c> 로 꺼내 쓰는 속성 이름.</summary>
    public const string CaptionAttribute = "data-caption";

    /// <summary>
    /// 카드로 펼 때 값 앞에 세울 <b>칸 이름</b>을 셀에 적는다.
    /// <c>CustomizeElement</c> 안에서 부른다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>이름이 없는 칸에는 적지 않는다.</b> 체크 칸처럼 머리줄에도 이름이
    /// 없는 것이 그렇다 — 자리로 말하는 것이지 이름으로 말하는 것이 아니다.
    /// </para>
    /// <para>
    /// 적지 않는 것에는 값이 하나 더 있다 — CSS 가
    /// <c>[data-caption] ~ [data-caption]</c> 로 <b>첫 값 칸</b>을 가려내
    /// 카드의 제목으로 삼는다(앞에 같은 것이 없는 하나). 손잡이 칸에까지
    /// 이름을 적으면 제목이 빈 체크 칸으로 밀린다.
    /// </para>
    /// </remarks>
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
