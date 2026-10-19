namespace JSini.Web.Abstractions;

/// <summary>지금 로그인한 사용자의 권한을 답하는 곳. 사이드바 거르기와 진입 가드가 같은 판정을 써야 「목록에 보이는데 누르면 403」이 생기지 않는다. 셸은 권한을 직접 계산하지 않고 여기에만 묻는다.</summary>
public interface IPermissionContext
{
    /// <summary>권한표를 받았는가. 받기 전에는 아무것도 거르지 않는다(「권한 없음」으로 다루면 사이드바가 비었다 채워진다).</summary>
    bool IsLoaded { get; }

    /// <summary>이 경로의 화면을 열 수 있는가. <c>use_view</c> 를 본다.</summary>
    bool CanView(string path);

    /// <summary>이 경로의 화면에서 그 동작을 할 수 있는가.</summary>
    bool Can(string path, MenuAction action);

    /// <summary>권한표를 서버에서 다시 읽는다. 메뉴 관리에서 권한을 고치면 화면을 새로 열지 않아도 반영된다. 라우트는 컴파일 시점에 고정이라 권한표와 메뉴 트리만 다시 읽으면 된다.</summary>
    Task ReloadAsync(CancellationToken cancellationToken = default);
}
