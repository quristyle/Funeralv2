namespace HelpDeskServer.Models;


    /// <summary>
    /// 로그인 요청 DTO
    /// </summary>
    public class LoginRequest
    {
        /// <summary>로그인 ID</summary>
        public string LoginId { get; set; } = string.Empty;
        /// <summary>비밀번호</summary>
        public string Password { get; set; } = string.Empty;
        /// <summary>로그인 타입 (미사용)</summary>
        public string LoginType { get; set; } = string.Empty;
    }
