namespace NotificationServer.Models
{
    /// <summary>SMTP 직발송 설정. 비밀번호만 비밀이다(Password 는 appsettings.Local.json 이나 환경변수에 둔다).
    /// 설정이 비면 메일이 전부 멈추므로 기동할 때 경고한다.</summary>
    public class EmailSettings
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public string User { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FromDisplay { get; set; } = string.Empty;
        public bool UseSsl { get; set; }
        public bool IgnoreCertificateErrors { get; set; }

        /// <summary>SMTP 한 번에 걸어 두는 제한 시간(밀리초). 부르는 쪽(AuthServer 의 20초)보다 짧아야 우리가 실패를 로그에 남긴다.</summary>
        public int TimeoutMs { get; set; } = 15000;

        /// <summary>보낼 수 있는 꼴을 갖췄나. 비밀번호까지 본다 — 호스트만 적고 비밀번호를 빠뜨리는 것이 가장 흔한 반쪽 설정이다.</summary>
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
