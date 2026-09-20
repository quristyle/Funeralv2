namespace AuthServer.DTOs;

/// <summary>
/// 사람 하나의 <b>이름과 얼굴</b>. 화면이 「누가 한 일인가」를 그릴 때 쓴다.
/// </summary>
/// <remarks>
/// <para>
/// <b>왜 <see cref="AccountDto"/> 를 쓰지 않나.</b> 그쪽은 계정 관리 화면의
/// 모양이라 메일·전화·역할·소속까지 실려 있다. 목록 카드에 얼굴 한 장을
/// 그리려고 그것을 통째로 내려보내면 <b>업무 화면 하나가 열릴 때마다 전
/// 직원의 연락처가 브라우저까지 간다.</b> 얼굴에 필요한 것은 세 칸뿐이다.
/// </para>
/// <para>
/// <see cref="Avatar"/> 는 <b>DB 에 적힌 값 그대로</b>다
/// (<c>/api/file/download/id/{guid}</c> 같은 Vue 시절 상대경로).
/// 포털(:5557)에는 <c>/api</c> 가 없으므로 화면이 셸 중계 경로로 옮겨 건다
/// (<c>web/.../Data/FileDownload.cs</c>).
/// </para>
/// </remarks>
public class UserFaceDto
{
    /// <summary>포털 로그인 아이디(<c>scom.accounts.user_id</c>). 물어본 그 값이다.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>사람이 읽는 이름. 없으면 로그인 아이디다.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>프로필 사진 주소. 올린 적이 없으면 <c>null</c>.</summary>
    public string? Avatar { get; set; }
}
