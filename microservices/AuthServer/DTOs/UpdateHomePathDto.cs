namespace AuthServer.DTOs;

/// <summary>
/// 로그인한 뒤 처음 열리는 화면을 정한다. <c>PUT /user/home-path</c> 의 본문.
/// </summary>
/// <remarks>
/// <para>
/// 담기는 곳은 <c>account_profile_details</c> 의 <c>HomePath</c> 한 줄이다 —
/// <c>UserInfoDto.HomePath</c> 가 읽어 가는 바로 그 값이고, 계정에 붙으므로
/// 회사 컴퓨터에서 고른 것이 휴대폰에서도 같다.
/// </para>
/// <para>
/// <b>비우면 「지정 안 함」이다.</b> 그때 줄을 지운다 — 기본값을 글자로 박아
/// 두면 나중에 기본이 바뀌어도 그 사람만 옛 화면에 묶인다.
/// </para>
/// <para>
/// <b>서버는 이 값이 어느 화면인지 모른다.</b> 라우팅의 임자가 프론트의
/// <c>@page</c> 로 넘어갔기 때문에(web/CLAUDE.md), 여기서 할 수 있는 검사는
/// 「이 사이트 안의 절대 경로인가」까지다. 권한이 있는 메뉴인지는 고르는
/// 자리(환경설정)와 여는 자리(셸의 홈)가 메뉴 트리로 따진다.
/// </para>
/// </remarks>
public class UpdateHomePathDto
{
    /// <summary>
    /// 갈 곳. 메뉴의 경로(<c>/setting/environment</c>)다. 비었으면 지정을 푼다.
    /// </summary>
    public string? HomePath { get; set; }
}
