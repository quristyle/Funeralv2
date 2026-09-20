namespace NotificationServer.Models
{
    /// <summary>
    /// SMTP 직발송(<c>/emails/send</c>) 설정.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>비밀번호만 비밀이다.</b> 나머지(호스트·포트·계정·보낸이 이름)는
    /// appsettings.json 에 그대로 적어 두고, <c>Password</c> 만
    /// <c>appsettings.Local.json</c> 이나 환경변수(<c>EmailSettings__Password</c>)로
    /// 넣는다. 운영 서버는 <c>/srv/jsini/config/NotificationServer/appsettings.Local.json</c> 이다.
    /// </para>
    ///
    /// <para>
    /// [이 설정이 비어 있으면 무엇이 멈추는가]
    /// </para>
    ///
    /// <para>
    /// <b>이 서비스를 거치는 메일이 전부 멈춘다</b> — 비밀번호 찾기(AuthServer) ·
    /// 소개 사이트 문의 접수(SiteServer) · AI 작업 완료 알림(ProjMngServer) ·
    /// 포털 관리의 메일 보내기 화면이 모두 <c>/emails/send</c> 한 곳으로 온다.
    /// 그런데 비밀번호 찾기는 아이디 노출을 막으려고 화면에 <b>언제나</b>
    /// 「보냈습니다」를 띄우므로, 설정이 없으면 <b>아무도 모르는 채로</b> 끊긴다.
    /// 그래서 기동할 때 한 번 경고를 남기고(<c>Program.cs</c>),
    /// 보낼 때는 「설정이 없다」고 분명히 말하며 실패한다(<c>SmtpEmailSender</c>).
    /// </para>
    /// </remarks>
    public class EmailSettings
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public string User { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FromDisplay { get; set; } = string.Empty;
        public bool UseSsl { get; set; }
        public bool IgnoreCertificateErrors { get; set; }

        /// <summary>
        /// SMTP 한 번에 걸어 두는 제한 시간(밀리초).
        /// </summary>
        /// <remarks>
        /// <b>부르는 쪽보다 짧아야 한다.</b> AuthServer 의 메일 클라이언트는 20초에
        /// 끊는데(<c>AccountMailClient</c>), MailKit 의 기본값은 2분이라 그냥 두면
        /// 메일 서버가 대답하지 않을 때 <b>부르는 쪽이 먼저 포기하고</b> 우리는
        /// 무슨 일이 있었는지 로그에 남기지도 못한다.
        /// </remarks>
        public int TimeoutMs { get; set; } = 15000;

        /// <summary>
        /// 보낼 수 있는 꼴을 갖췄나. <b>비밀번호까지 본다</b> — 호스트만 적어 두고
        /// 비밀번호를 빠뜨리는 것이 가장 흔한 반쪽 설정이다.
        /// </summary>
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(Host)
            && Port > 0
            && !string.IsNullOrWhiteSpace(User)
            && !string.IsNullOrWhiteSpace(Password)
            && !Password.StartsWith("__SET_IN_", StringComparison.Ordinal);

        /// <summary>비어 있는 항목 이름들. 경고 문구에 그대로 적는다.</summary>
        public string MissingKeys()
        {
            var missing = new List<string>();

            if (string.IsNullOrWhiteSpace(Host)) missing.Add("Host");
            if (Port <= 0) missing.Add("Port");
            if (string.IsNullOrWhiteSpace(User)) missing.Add("User");
            if (string.IsNullOrWhiteSpace(Password)
                || Password.StartsWith("__SET_IN_", StringComparison.Ordinal)) missing.Add("Password");

            return string.Join(" · ", missing.Select(k => $"EmailSettings:{k}"));
        }
    }
}
