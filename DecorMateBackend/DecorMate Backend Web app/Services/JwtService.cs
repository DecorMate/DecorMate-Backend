using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
namespace DecorMate_Backend_Web_app.Services;
public class JwtService
{
    private readonly IConfiguration _configuration;
    public JwtService(IConfiguration configuration) => _configuration = configuration;

    public string GenerateToken(IdentityUser user, IEnumerable<string> roles)
    {
        var key = _configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("JWT Key is not configured (Jwt:Key).");

        var issuer = _configuration["Jwt:Issuer"] ?? "YourIssuer";
        var audience = _configuration["Jwt:Audience"] ?? "YourAudience";
        var expiryMinutes = int.TryParse(_configuration["Jwt:AccessTokenExpirationMinutes"], out var m) ? m : 60;

        var claims = new List<Claim>
        {
            // Standard JWT claims
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),

            // Useful ASP.NET-friendly claims
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.UserName ?? user.Email ?? string.Empty)
        };

        // Add roles both as ClaimTypes.Role (URI) and plain "role" to maximize compatibility
        foreach (var r in roles ?? Enumerable.Empty<string>())
        {
            claims.Add(new Claim(ClaimTypes.Role, r));
            claims.Add(new Claim("role", r));
        }

        var keyBytes = Encoding.UTF8.GetBytes(key);
        var signingKey = new SymmetricSecurityKey(keyBytes);
        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
