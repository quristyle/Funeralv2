namespace NotificationServer.Models
{
    public class EmailSettings
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public string User { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FromDisplay { get; set; } = string.Empty;
        public bool UseSsl { get; set; }
        public bool IgnoreCertificateErrors { get; set; }
        public int TimeoutMs { get; set; }
    }
}
