namespace JSini.Web.Shell.Routing;

/// <summary>
/// 셸이 앞에서 받아 넘겨 줄 업무 MFE 한 개.
///
/// [왜 설정에서 읽나 — 예전에는 어셈블리를 훑었다]
///
/// 업무 앱이 RCL 이던 시절에는 셸이 출력 폴더를 훑어
/// <c>IPortalModule</c> 구현을 찾을 수 있었다. 이제 앱이 <b>독립 프로세스</b>라
/// 셸의 폴더에 그 DLL 이 없다 — 훑을 대상 자체가 없다.
///
/// 그래서 목록은 설정(appsettings 의 <c>PortalApps</c>)에서 온다. 이 편이
/// 오히려 맞다: 앱 주소는 환경마다 다르고(개발은 localhost 포트, 운영은 컨테이너
/// 이름), 그건 원래 설정이 정할 일이다.
/// </summary>
public sealed class PortalApp
{
    /// <summary>앱 식별자 (<c>funeral</c>). 로그·진단 화면에서 쓴다.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>사람이 읽는 이름 (<c>장례식장</c>). 진단 화면에만 쓴다.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 이 앱이 가져가는 경로 접두사 (<c>/funeral</c>).
    /// 앱 쪽 <c>IPortalModule.RoutePrefix</c> 와 같아야 한다.
    /// </summary>
    public string RoutePrefix { get; set; } = string.Empty;

    /// <summary>앱의 실제 주소 (<c>http://localhost:5561/</c>).</summary>
    public string Address { get; set; } = string.Empty;
}
