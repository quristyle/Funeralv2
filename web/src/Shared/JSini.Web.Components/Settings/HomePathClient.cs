using JSini.Web.Components.Layout;
using JSini.Web.Http;
using Microsoft.AspNetCore.Components.Authorization;

namespace JSini.Web.Components.Settings;

/// <summary>로그인한 뒤 처음 열리는 화면을 계정에 담는다. 읽는 길은 여기 없다 — 값은 부트스트랩이 이미 실어 오므로 읽기를 더 두면 환경설정을 열 때마다 왕복이 하나 는다. 여기서 하는 일은 쓰기 하나와 뒷정리 둘이다.
/// 같은 값이 세 군데에 있다: 서버, 이 회로의 CurrentUser, 부트스트랩 응답을 2분간 든 통. 서버만 고치면 업무를 한 번 옮기는 순간 옛 값이 돌아온다 — 모듈이 갈리면 레이아웃이 새로 생기고 부트스트랩은 게이트웨이 대신 통을 쓴다. 저장을 부르는 화면이 셋을 기억하게 두지 않고 여기서 한 번에 한다.
/// 고르개는 장례식장 환경설정에 있지만 고른 값을 쓰는 곳은 셸의 홈 화면이라, 셸은 업무 모듈을 이름으로 알 수 없어 규칙과 저장을 여기 둔다(NoticeClient · NotifySender 와 같은 까닭).</summary>
public sealed class HomePathClient(
    GatewayClient gateway,
    CurrentUser me,
    PortalBootstrapStore store,
    AuthenticationStateProvider auth)
{
    /// <summary>첫 화면을 정한다.</summary>
    /// <param name="homePath">DB 의 메뉴 경로(/setting/environment). 비우면 지정을 푼다 — 그때는 환경설정이 열린다(PortalHome.Resolve).</param>
    /// <param name="ct">취소 토큰.</param>
    public async Task SaveAsync(string? homePath, CancellationToken ct = default)
    {
        await gateway.PutAsync("auth/user/home-path", new { homePath }, ct);

        // 이 회로. 헤더도 홈도 같은 통을 보므로 바로 반영된다. 서버가 안 골랐을 때 채워 보내는 값과 같은 것을 넣는다 — 빈 글자면 다음 부트스트랩이 기본값을 실어 와 화면이 한 번 갈린 것처럼 보인다.
        me.SetHomePath(string.IsNullOrWhiteSpace(homePath) ? UnsetFromServer : homePath);

        // ② 2분짜리 통. 안 비우면 업무를 옮기는 순간 옛 값이 돌아온다.
        store.Forget(PortalBootstrapStore.KeyFor(
            (await auth.GetAuthenticationStateAsync()).User));
    }

    /// <summary>안 골랐을 때 서버가 채워 보내는 값(UserInfoDto.HomePath 의 기본값). 여기서 고르는 값이 아니다 — 서버와 맞춰 두려고 적어 둔다.</summary>
    private const string UnsetFromServer = "/workspace";
}
