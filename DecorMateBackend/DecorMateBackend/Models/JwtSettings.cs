namespace DecorMate_Backend_Web_app.Models
{
    public class JwtSettings
    {
        public string Key { get; set; } = null!; // symmetric key
        public string Issuer { get; set; } = null!;
        public string Audience { get; set; } = null!;
        public int AccessTokenExpirationMinutes { get; set; } = 30;
        public int RefreshTokenExpirationDays { get; set; } = 30;
    }
}
