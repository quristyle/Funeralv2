using Microsoft.AspNetCore.Components;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Shared;

public partial class WbsEditForm
{
    /// <summary>고치고 있는 일감. <b>편집 모델을 그대로 받는다</b> — 복사하지 않는다.</summary>
    [Parameter, EditorRequired] public WbsItemDto Item { get; set; } = default!;

    /// <summary>
    /// 날짜 칸은 <c>DateTime?</c> 으로 오간다 — DevExpress 달력이 그 형만 받는다.
    /// 자료 쪽은 시각이 없는 날짜라 <c>DateOnly</c> 다.
    /// </summary>
    private static DateTime? Day(DateOnly? value) => value?.ToDateTime(TimeOnly.MinValue);

    /// <inheritdoc cref="Day" />
    private static DateOnly? Only(DateTime? value) =>
        value is null ? null : DateOnly.FromDateTime(value.Value);
}
