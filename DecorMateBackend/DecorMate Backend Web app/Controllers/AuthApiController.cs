using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using DecorMate_Backend_Web_app.Data;
using DecorMate_Backend_Web_app.Models;
using DecorMate_Backend_Web_app.Models.DTOs;
using DecorMate_Backend_Web_app.Services;
using Microsoft.AspNetCore.WebUtilities;

namespace DecorMate_Backend_Web_app.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthApiController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _db;
        private readonly JwtService _jwtService;
        private readonly IEmailSender _emailSender;
        private readonly JwtSettings _jwtSettings;
        private readonly ILogger<AuthApiController> _logger;
        private readonly IHttpClientFactory _httpFactory;

        public AuthApiController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ApplicationDbContext db,
            JwtService jwtService,
            IEmailSender emailSender,
            IOptions<JwtSettings> jwtOptions,
            ILogger<AuthApiController> logger,
            IHttpClientFactory httpFactory)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _db = db;
            _jwtService = jwtService;
            _emailSender = emailSender;
            _jwtSettings = jwtOptions.Value;
            _logger = logger;
            _httpFactory = httpFactory;
        }

        // POST api/auth/register
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            if (await _userManager.FindByEmailAsync(dto.Email) != null)
                return BadRequest(new { message = "Email already in use" });

            var user = new ApplicationUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Provider = AuthProvider.Local
            };

            var createRes = await _userManager.CreateAsync(user, dto.Password);
            if (!createRes.Succeeded)
                return BadRequest(createRes.Errors.Select(e => e.Description));

            await _userManager.AddToRoleAsync(user, "User");

            // Generate OTP (6 chars: digits + letters)
            var otp = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();
            user.OtpCode = otp;
            user.OtpExpiry = DateTime.UtcNow.AddMinutes(10);

            await _userManager.UpdateAsync(user);

            await _emailSender.SendEmailAsync(user.Email, "Confirm your account",
                $"Your OTP code is: <b>{otp}</b> (valid for 10 minutes)");

            return Ok(new { message = "Registered. Please check your email for the OTP." });
        }

        // POST api/auth/register-confirmation
        [HttpPost("register-confirmation")]
        public async Task<IActionResult> RegisterConfirmation([FromBody] ConfirmDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null) return BadRequest(new { message = "Invalid email" });

            if (user.EmailConfirmed)
                return BadRequest(new { message = "Email already confirmed" });

            if (user.OtpCode != dto.Otp || user.OtpExpiry < DateTime.UtcNow)
                return BadRequest(new { message = "Invalid or expired OTP" });

            user.EmailConfirmed = true;
            user.OtpCode = null;
            user.OtpExpiry = null;
            await _userManager.UpdateAsync(user);

            var roles = await _userManager.GetRolesAsync(user);

            var accessToken = _jwtService.GenerateToken(user, roles);

            var refreshToken = CreateRefreshToken(Request.HttpContext.Connection.RemoteIpAddress?.ToString());
            refreshToken.ApplicationUserId = user.Id;

            _db.RefreshTokens.Add(refreshToken);
            await _db.SaveChangesAsync();

            // Optionally set cookie (for web clients)
            SetRefreshTokenCookie(refreshToken.Token, refreshToken.Expires);

            return Ok(new AuthResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken.Token,
                AccessTokenExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
                RefreshTokenExpiresAt = refreshToken.Expires,
                User = new UserDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Roles = roles.ToArray()
                }
            });
        }

        // POST api/auth/login
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null) return Unauthorized(new { message = "Invalid credentials" });

            var check = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true);
            if (!check.Succeeded) return Unauthorized(new { message = "Invalid credentials" });

            if (!await _userManager.IsEmailConfirmedAsync(user))
                return BadRequest(new { message = "Email not confirmed" });

            var roles = await _userManager.GetRolesAsync(user);
            var accessToken = _jwtService.GenerateToken(user, roles);
            var refreshToken = CreateRefreshToken(Request.HttpContext.Connection.RemoteIpAddress?.ToString());
            refreshToken.ApplicationUserId = user.Id;
            _db.RefreshTokens.Add(refreshToken);
            await _db.SaveChangesAsync();

            // Optionally set cookie for web clients
            SetRefreshTokenCookie(refreshToken.Token, refreshToken.Expires);

            return Ok(new AuthResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken.Token,
                AccessTokenExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
                RefreshTokenExpiresAt = refreshToken.Expires,
                User = new UserDto { Id = user.Id, Email = user.Email, FirstName = user.FirstName, LastName = user.LastName, Roles = roles.ToArray() }
            });
        }

        // POST api/auth/forgot-password (mobile) - generates OTP + stores reset token server-side and sends email
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var user = await _userManager.FindByEmailAsync(dto.Email);
           
            if (user == null)
                return Ok(new { message = "If the account exists, an OTP has been sent to the email." });

            // Generate a password reset token (Identity token)
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);

            // store token safely (base64-encoded) + expiry (short-lived)
            user.PasswordResetToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(resetToken));
            user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1); // token valid 1 hour

            // Generate a 6-character OTP (alphanumeric)
            user.PasswordResetOtp = OTPService.GenerateOtp(6);
            user.PasswordResetOtpExpiry = DateTime.UtcNow.AddMinutes(10); // OTP valid 10 minutes

            await _userManager.UpdateAsync(user);

            // send OTP email (and optionally provide reset link for web)
            await _emailSender.SendEmailAsync(user.Email, "Reset your password",
                $"Your password reset OTP is: <b>{user.PasswordResetOtp}</b>. It expires in 10 minutes.<br/><br/>");

            return Ok(new { message = "If the account exists, an OTP has been sent to the email." });
        }

        // POST api/auth/reset-password (mobile) - accept email + otp + newPassword
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPasswordWithOtp([FromBody] ResetPasswordWithOtpDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
                return BadRequest(new { message = "Invalid request" });

            // Validate OTP
            if (string.IsNullOrEmpty(user.PasswordResetOtp) ||
                !user.PasswordResetOtp.Equals(dto.Otp, StringComparison.OrdinalIgnoreCase) ||
                !user.PasswordResetOtpExpiry.HasValue ||
                user.PasswordResetOtpExpiry.Value < DateTime.UtcNow)
            {
                return BadRequest(new { message = "Invalid or expired OTP" });
            }

            // Validate stored token
            if (string.IsNullOrEmpty(user.PasswordResetToken) ||
                !user.PasswordResetTokenExpiry.HasValue ||
                user.PasswordResetTokenExpiry.Value < DateTime.UtcNow)
            {
                return BadRequest(new { message = "Reset token expired or missing. Please request a new OTP." });
            }

            // decode token
            string decodedToken;
            try
            {
                decodedToken = Encoding.UTF8.GetString(Convert.FromBase64String(user.PasswordResetToken));
            }
            catch
            {
                return BadRequest(new { message = "Invalid reset token stored. Please request a new OTP." });
            }

            // Reset password using Identity
            var resetResult = await _userManager.ResetPasswordAsync(user, decodedToken, dto.NewPassword);
            if (!resetResult.Succeeded)
            {
                return BadRequest(new { errors = resetResult.Errors.Select(e => e.Description) });
            }

            // Clear stored token & OTP
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiry = null;
            user.PasswordResetOtp = null;
            user.PasswordResetOtpExpiry = null;
            await _userManager.UpdateAsync(user);

            // Optionally: mark email confirmed
            if (!await _userManager.IsEmailConfirmedAsync(user))
            {
                user.EmailConfirmed = true;
                await _userManager.UpdateAsync(user);
            }

            // Issue tokens (access + refresh) to log the mobile user in immediately
            var roles = await _userManager.GetRolesAsync(user);
            var accessToken = _jwtService.GenerateToken(user, roles);

            // create & save refresh token (reuse your existing CreateRefreshToken if present)
            var refreshToken = CreateRefreshToken(Request.HttpContext.Connection.RemoteIpAddress?.ToString());
            refreshToken.ApplicationUserId = user.Id;
            _db.RefreshTokens.Add(refreshToken);
            await _db.SaveChangesAsync();

            var response = new AuthResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken.Token,
                AccessTokenExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
                RefreshTokenExpiresAt = refreshToken.Expires,
                User = new UserDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Roles = roles.ToArray()
                }
            };

            // Optionally set http-only cookie (for web) — mobile clients ignore
            SetRefreshTokenCookie(refreshToken.Token, refreshToken.Expires);

            return Ok(response);
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshRequestDto body)
        {
            // Accept token either from body or cookie (fallback)
            string? token = body?.RefreshToken ?? Request.Cookies["refreshToken"];
            if (string.IsNullOrEmpty(token))
                return BadRequest(new { message = "Refresh token required" });

            // find token in DB and include the user
            var existing = await _db.RefreshTokens
                .Include(r => r.ApplicationUser)
                .FirstOrDefaultAsync(r => r.Token == token);

            if (existing == null)
                return Unauthorized(new { message = "Invalid refresh token" });

            if (!existing.IsActive)
                return Unauthorized(new { message = "Refresh token is not active" });

            // rotate: revoke current token and create a new one
            existing.Revoked = DateTime.UtcNow;
            existing.RevokedByIp = Request.HttpContext.Connection.RemoteIpAddress?.ToString();

            var newRefresh = CreateRefreshToken(Request.HttpContext.Connection.RemoteIpAddress?.ToString());
            newRefresh.ApplicationUserId = existing.ApplicationUserId;
            existing.ReplacedByToken = newRefresh.Token;

            _db.RefreshTokens.Add(newRefresh);
            await _db.SaveChangesAsync();

            var user = existing.ApplicationUser!;
            var roles = await _userManager.GetRolesAsync(user);
            var accessToken = _jwtService.GenerateToken(user, roles);

            // Optional: set cookie for web clients
            SetRefreshTokenCookie(newRefresh.Token, newRefresh.Expires);

            var response = new AuthResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = newRefresh.Token,
                AccessTokenExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
                RefreshTokenExpiresAt = newRefresh.Expires,
                User = new UserDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Roles = roles.ToArray()
                }
            };

            return Ok(response);
        }


        // POST api/auth/revoke
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost("revoke")]
        public async Task<IActionResult> Revoke([FromBody] dynamic body)
        {
            string? token = (body?.refreshToken ?? (Request.Cookies["refreshToken"] ?? null))?.ToString();
            if (string.IsNullOrEmpty(token)) return BadRequest(new { message = "Token required" });

            var existing = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.Token == token);
            if (existing == null) return NotFound();

            existing.Revoked = DateTime.UtcNow;
            existing.RevokedByIp = Request.HttpContext.Connection.RemoteIpAddress?.ToString();
            await _db.SaveChangesAsync();
            return Ok();
        }

        // -----------------------
        //        Helpers
        // -----------------------
        private RefreshToken CreateRefreshToken(string? ipAddress)
        {
            var randomBytes = new byte[64];
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);

            // Use URL-safe Base64 to avoid problems when sending in URLs/JSON
            var token = WebEncoders.Base64UrlEncode(randomBytes);

            return new RefreshToken
            {
                Token = token,
                Expires = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays > 0 ? _jwtSettings.RefreshTokenExpirationDays : 30),
                Created = DateTime.UtcNow,
                CreatedByIp = ipAddress
            };
        }

        private void SetRefreshTokenCookie(string token, DateTime expires)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Expires = expires,
                Secure = true,
                SameSite = SameSiteMode.Strict
            };
            Response.Cookies.Append("refreshToken", token, cookieOptions);
        }

    }
}
