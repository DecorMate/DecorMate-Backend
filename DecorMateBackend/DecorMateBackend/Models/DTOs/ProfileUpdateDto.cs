using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

public class ProfileUpdateDto
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }

    [Phone]
    public string? PhoneNumber { get; set; }
    public string? CompanyName { get; set; }
    public IFormFile? ProfileImage { get; set; }
}
