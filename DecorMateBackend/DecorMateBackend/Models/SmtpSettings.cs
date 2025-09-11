namespace DecorMate_Backend_Web_app.Models
{
    public class SmtpSettings
    {
        public string Host { get; set; } = null!;
        public int Port { get; set; } = 587;
        public string User { get; set; } = null!;
        public string Pass { get; set; } = null!;
        public string From { get; set; } = null!;
        public string FromName { get; set; } = "App";
        public bool UseSsl { get; set; } = false;    
        public bool UseStartTls { get; set; } = true; 
    }

}
