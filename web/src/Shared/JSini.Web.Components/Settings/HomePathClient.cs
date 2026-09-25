using JSini.Web.Components.Layout;
using JSini.Web.Http;
using Microsoft.AspNetCore.Components.Authorization;

namespace JSini.Web.Components.Settings;

/// <summary>
/// <b>로그인한 뒤 처음 열리는 화면</b>을 계정에 담는다.
/// </summary>
/// <remarks>
/// <para>
/// [읽는 길은 여기 없다]
/// </para>
///
/// <para>
/// 값은 부트스트랩이 이미 실어 온다(<see cref="CurrentUser.HomePath"/>) —
/// 읽기를 여기 하나 더 두면 환경설정을 열 때마다 왕복이 하나 는다. 그래서
/// 이 클래스가 하는 일은 <b>쓰기 하나</b>와 그에 딸린 <b>뒷정리 둘</b>이다.
/// </para>
///
/// <para>
/// [뒷정리를 여기서 하는 까닭]
/// </para>
///
/// <para>
/// 같은 값이 <b>세 군데</b>에 있다 — 서버 · 이 회로의
/// <see cref="CurrentUser"/> · 부트스트랩 응답을 2분간 들고 있는 통
/// (<see cref="PortalBootstrapStore"/>). 서버만 고치면 <b>업무를 한 번
/// 옮기는 순간 옛 값이 돌아온다</b>: 모듈이 갈리면 레이아웃이 새로 생기고,
/// 그때 부트스트랩은 게이트웨이 대신 통에 있는 것을 쓴다.
/// </para>
///
/// <para>
/// 저장을 부르는 화면이 그 셋을 기억하게 두지 않는다. 한 자리에서 다 한다.
/// </para>
///
/// <para>
/// [왜 업무 모듈이 아니라 여기 있나]
/// </para>
///
/// <para>
/// 고르개는 장례식장의 환경설정에 있지만, 고른 값을 <b>실제로 쓰는 곳은
/// 셸의 홈 화면</b>이다(<c>Home.razor</c>). 셸은 업무 모듈을 이름으로 알지
/// 못하므로(web/CLAUDE.md 의 의존 규칙 4번) 규칙과 저장을 여기 둔다 —
/// <see cref="NoticeClient"/>·<see cref="NotifySender"/> 가 여기 있는 것과
/// 같은 까닭이다.
/// </para>
/// </remarks>
public sealed class HomePathClient(
    GatewayClient gateway,
    CurrentUser me,
    PortalBootstrapStore store,
    AuthenticationStateProvider auth)
{
    /// <summary>
    /// 첫 화면을 정한다.
    /// </summary>
    /// <param name="homePath">
    /// DB 의 메뉴 경로(<c>/setting/environment</c>). 비우면 지정을 푼다 —
    /// 그때는 환경설정이 열린다(<see cref="PortalHome.Resolve"/>).
    /// </param>
    /// <param name="ct">취소 토큰.</param>
    public async Task SaveAsync(string? homePath, CancellationToken ct = default)
    {
        await gateway.PutAsync("auth/user/home-path", new { homePath }, ct);

        // ① 이 회로. 헤더도 홈도 같은 통을 보므로 바로 반영된다.
        //
        // **서버가 안 골랐을 때 채워 보내는 값과 같은 것을 넣는다.** 빈 글자를
        // 넣어 두면 다음 부트스트랩이 `/workspace` 를 실어 와서 화면이 한 번
        // 갈린 것처럼 보인다 — 둘 다 「안 골랐다」로 읽히지만(PortalHome.IsUnset)
        // 같은 뜻이면 같은 글자여야 나중에 둘을 비교하는 코드가 안 틀린다.
        me.SetHomePath(string.IsNullOrWhiteSpace(homePath) ? UnsetFromServer : homePath);

        // ② 2분짜리 통. 안 비우면 업무를 옮기는 순간 옛 값이 돌아온다.
        store.Forget(PortalBootstrapStore.KeyFor(
            (await auth.GetAuthenticationStateAsync()).User));
    }

    /// <summary>
    /// 안 골랐을 때 서버가 채워 보내는 값(<c>UserInfoDto.HomePath</c> 의 기본값).
    /// <b>여기서 고르는 값이 아니다</b> — 서버와 맞춰 두려고 적어 둔다.
    /// </summary>
    private const string UnsetFromServer = "/workspace";
}
