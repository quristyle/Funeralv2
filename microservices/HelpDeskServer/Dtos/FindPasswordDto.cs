namespace HelpDeskServer.Dtos
{
    /// <summary>
    /// 비밀번호 찾기 요청을 위한 DTO
    /// </summary>
    public class FindPasswordDto
    {
        /// <summary>로그인 ID</summary>
        public string LoginId { get; set; }
        /// <summary>
        /// 이메일 주소
        /// </summary>
        public string Email { get; set; }
    }
}
