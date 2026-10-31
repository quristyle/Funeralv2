using Microsoft.AspNetCore.Components;
using JSini.Web.ProjMng.Api;

namespace JSini.Web.ProjMng.Components.Shared;

public partial class WbsEditForm
{
    /// <summary>고치고 있는 일감. <b>편집 모델을 그대로 받는다</b> — 복사하지 않는다.</summary>
    [Parameter, EditorRequired] public WbsItemDto Item { get; set; } = default!;

    /// <summary>넓은 창에서 네 칸씩 늘어놓고 라벨을 입력칸 테두리에 걸친다. 세로가 모자란 일정표 편집 창이 쓴다.</summary>
    [Parameter] public bool Compact { get; set; }

    /// <summary>같은 격자 맨 앞에 얹을 칸. 따로 폼을 두면 줄이 둘로 갈려 칸이 맞지 않는다.</summary>
    [Parameter] public RenderFragment? Leading { get; set; }

    /// <summary>날짜·시간 변환 도우미 (하위 호환 유지).</summary>
    private static DateTime? Day(DateOnly? value) => value?.ToDateTime(TimeOnly.MinValue);

    /// <inheritdoc cref="Day" />
    private static DateOnly? Only(DateTime? value) =>
        value is null ? null : DateOnly.FromDateTime(value.Value);
}
