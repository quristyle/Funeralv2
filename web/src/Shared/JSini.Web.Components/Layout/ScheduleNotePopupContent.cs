namespace JSini.Web.Components.Layout;

/// <summary>
/// AI 일정 메모 공통 팝업이 그릴 알맹이 부품 타입.
/// ProjMng 모듈이 등록한다(QuickAskContent 와 동일한 DI 역전 구조).
/// </summary>
public sealed class ScheduleNotePopupContent(Type popupType)
{
    /// <summary>팝업 부품 타입.</summary>
    public Type PopupType { get; } = popupType;
}
