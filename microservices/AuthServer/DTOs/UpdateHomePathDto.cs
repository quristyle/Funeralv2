namespace AuthServer.DTOs;

/// <summary>로그인한 뒤 처음 열리는 화면을 정한다. <c>PUT /user/home-path</c> 의 본문이다. 담기는 곳은 <c>account_profile_details</c> 의 HomePath 한 줄이고, 비우면 그 줄을 지운다.</summary>
public class UpdateHomePathDto
{
    /// <summary>갈 곳. 메뉴의 경로(<c>/setting/environment</c>)다. 비었으면 지정을 푼다.</summary>
    public string? HomePath { get; set; }
}
