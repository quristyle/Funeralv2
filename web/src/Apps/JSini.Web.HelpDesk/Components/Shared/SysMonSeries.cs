namespace JSini.Web.HelpDesk.Components.Shared;

/// <summary>
/// 경량 SVG 모니터링 차트의 시리즈 데이터 정의.
/// DevExpress DxChart 의 무거운 JS 클라이언트 렌더링 대신 서버에서 SVG 패스를 직접 계산하여 브라우저 메인 스레드 멈춤 없이 즉시 렌더링한다.
/// </summary>
public sealed record SysMonSeries(
    string Name,
    string Color,
    IReadOnlyList<double> Values,
    bool IsArea = false,
    double AreaOpacity = 0.15,
    string? DashStyle = null);

/// <summary>
/// 경량 SVG 모니터링 차트의 가로 기준선 정의 (예: CPU 80%, I/O 20ms, PLE 1000s 등).
/// </summary>
public sealed record SysMonRefLine(
    string Name,
    double Value,
    string Color,
    string DashStyle = "4 4");
