using Microsoft.Extensions.DependencyInjection;

namespace JSini.Web.Components.Layout;

/// <summary>
/// 상단 레이아웃 아이콘과 AI 일정 메모 공통 팝업을 연결하는 상태 관리자.
/// 단추는 HeaderTools 에 있고, 팝업 호스트는 MainLayout(ScheduleNotePopupHost)에 있어
/// 둘 사이에 여닫힘 상태를 중계한다. scoped 로 등록되어 사용자마다 개별 상태를 유지한다.
/// </summary>
public sealed class ScheduleNoteReveal
{
    private readonly IServiceProvider _services;

    public ScheduleNoteReveal(IServiceProvider services)
    {
        _services = services;
    }

    public event Action? Changed;

    /// <summary>공통 팝업이 열려 있는가.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>모듈이 등록한 팝업 부품 타입.</summary>
    public Type? PopupType => _services.GetService<ScheduleNotePopupContent>()?.PopupType;

    /// <summary>팝업을 띄울 준비가 되었는가(모듈이 알맹이를 등록했는가).</summary>
    public bool CanOpen => PopupType is not null;

    /// <summary>팝업을 연다.</summary>
    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        Changed?.Invoke();
    }

    /// <summary>팝업을 닫는다.</summary>
    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        Changed?.Invoke();
    }

    /// <summary>팝업을 토글한다.</summary>
    public void Toggle()
    {
        IsOpen = !IsOpen;
        Changed?.Invoke();
    }
}
