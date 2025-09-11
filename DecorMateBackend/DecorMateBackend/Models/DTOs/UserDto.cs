namespace DecorMate_Backend_Web_app.Models.DTOs
{
    public class UserDto
    {
        public string Email { get; set; } = null!;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? ProfilePictureUrl { get; set; }
        public string? PhoneNumber { get; set; }
        public string[]? Roles { get; set; }
    }
}
