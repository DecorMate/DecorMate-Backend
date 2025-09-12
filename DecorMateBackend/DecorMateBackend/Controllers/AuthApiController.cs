using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.WebUtilities;

using DecorMate_Backend_Web_app.Data;
using DecorMate_Backend_Web_app.Models;
using DecorMate_Backend_Web_app.Models.DTOs;
using System.Security.Claims;
using DecorMate_Backend_Web_app.Services;
using DecorMateBackend.Models.Enums;
using DecorMateBackend.Services;
using static System.Net.WebRequestMethods;

namespace DecorMateBackend.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthApiController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _db;
        private readonly JwtService _jwtService;
        private readonly JwtSettings _jwtSettings;
        private readonly ILogger<AuthApiController> _logger;
        private readonly IHttpClientFactory _httpFactory;
        private readonly IConfiguration _configuration; 
        private readonly CloudinaryService _cloudinary;
        private readonly EmailService _emailService;

        public AuthApiController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ApplicationDbContext db,
            JwtService jwtService,
            IOptions<JwtSettings> jwtOptions,
            ILogger<AuthApiController> logger,
            IHttpClientFactory httpFactory,
            IConfiguration configuration,
            CloudinaryService cloudinaryService,
             EmailService emailService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _db = db;
            _jwtService = jwtService;
            _jwtSettings = jwtOptions.Value;
            _logger = logger;
            _httpFactory = httpFactory;
            _configuration = configuration;
            _cloudinary = cloudinaryService;
            _emailService = emailService;
        }

        // -----------------------
        // Register (creates user + sends OTP by email)
        // -----------------------
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var existing = await _userManager.FindByEmailAsync(dto.Email);
            if (existing != null)
            {
                if (existing.EmailConfirmed)
                    return BadRequest(new { message = "Email already in use" });
                // user exists but not confirmed -> delete to allow re-register
                var delRes = await _userManager.DeleteAsync(existing);
                if (!delRes.Succeeded)
                {
                    _logger.LogWarning("Failed to delete existing unconfirmed user {Email}: {Errors}",
                        dto.Email, string.Join(",", delRes.Errors.Select(e => e.Description)));
                    // continue, but inform client
                }
            }

            var user = new ApplicationUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Provider = AuthProvider.Local,
                EmailConfirmed = false
            };

            var createRes = await _userManager.CreateAsync(user, dto.Password);
            if (!createRes.Succeeded)
            {
                _logger.LogWarning("Create user failed for {Email}: {Errors}", dto.Email, string.Join(",", createRes.Errors.Select(e => e.Description)));
                return BadRequest(createRes.Errors.Select(e => e.Description));
            }

            await _userManager.AddToRoleAsync(user, "User");

            // Generate and persist OTP
            var otp = GenerateOtp(6);
            user.OtpCode = otp;
            user.OtpExpiry = DateTime.UtcNow.AddMinutes(10);
            var upd = await _userManager.UpdateAsync(user);
            if (!upd.Succeeded)
            {
                _logger.LogWarning("Failed to update user with verification code for {Email}: {Errors}", user.Email, string.Join(",", upd.Errors.Select(e => e.Description)));
                // still attempt to send email, but record the failure
            }

            // TRY TO SEND EMAIL — do NOT swallow exceptions silently
            try
            {
                // Decide callback only if you want a link; for mobile we used OTP only
                string? callback = null; // or $"{Request.Scheme}://{Request.Host}/Auth/ConfirmEmail?userId={user.Id}"
                await _emailService.SendConfirmationAsync(user, callback, includeButton: false, otp: otp, ct: CancellationToken.None);

                _logger.LogInformation("Register: verification code email sent successfully to {Email}", user.Email);
                return Ok(new { message = "Registered. Please check your email for the verification code." });
            }
            catch (Exception ex)
            {
                // Log full exception (stack trace) for diagnosis
                _logger.LogError(ex, "Register: Failed to send verification code email for {Email}", user.Email);

                // Return helpful response to client (in production you may hide details)
                return StatusCode(StatusCodes.Status502BadGateway, new
                {
                    message = "Registered but failed to send confirmation email.",
                    error = ex.Message
                });
            }
        }


        // -----------------------
        // Register confirmation (OTP) -> returns tokens
        // -----------------------
        [HttpPost("register-confirmation")]
        public async Task<IActionResult> RegisterConfirmation([FromBody] ConfirmDto dto)
        {
            if (string.IsNullOrEmpty(dto.Email) || string.IsNullOrEmpty(dto.Otp))
                return BadRequest(new { message = "Email and verification code are required" });

            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null) return BadRequest(new { message = "Invalid email" });

            if (user.EmailConfirmed) return BadRequest(new { message = "Email already confirmed" });

            if (user.OtpCode != dto.Otp || !user.OtpExpiry.HasValue || user.OtpExpiry.Value < DateTime.UtcNow)
                return BadRequest(new { message = "Invalid or expired verification code" });

            user.EmailConfirmed = true;
            user.OtpCode = null;
            user.OtpExpiry = null;
            await _userManager.UpdateAsync(user);

            var tokens = await _jwtService.GenerateTokensAsync(user);
            var roles = await _userManager.GetRolesAsync(user);
            tokens.User = new UserDto
            {
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                ProfilePictureUrl = user.ProfilePictureUrl,
                PhoneNumber = user.PhoneNumber,
                Roles = roles.ToArray()
            };
            var refreshEntity = new RefreshToken
            {
                Token = tokens.RefreshToken,
                Expires = tokens.RefreshTokenExpiresAt,
                Created = DateTime.UtcNow,
                CreatedByIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
                ApplicationUserId = user.Id
            };
            _db.RefreshTokens.Add(refreshEntity);
            await _db.SaveChangesAsync();

            tokens.RefreshToken = refreshEntity.Token;
            return Ok(tokens);
        }


        // -----------------------
        // Login (returns access + refresh)
        // -----------------------
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
            if (roles.FirstOrDefault("Company")== "Company")
                return BadRequest(new { message = "Use the Dashboard"});
            var tokens = await _jwtService.GenerateTokensAsync(user);
            tokens.User =new UserDto
            {
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                ProfilePictureUrl = user.ProfilePictureUrl,
                PhoneNumber = user.PhoneNumber,
                Roles = roles.ToArray()
            };
            var refreshEntity = new RefreshToken
            {
                Token = tokens.RefreshToken,
                Expires = tokens.RefreshTokenExpiresAt,
                Created = DateTime.UtcNow,
                CreatedByIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
                ApplicationUserId = user.Id
            };

            _db.RefreshTokens.Add(refreshEntity);
            await _db.SaveChangesAsync();

            tokens.RefreshToken = refreshEntity.Token;
            return Ok(tokens);
        }


        // -----------------------
        // Forgot password (mobile) => sends OTP and stores encoded reset token server-side
        // -----------------------
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var user = await _userManager.FindByEmailAsync(dto.Email);
            // don't reveal whether user exists
            if (user == null)
                return Ok(new { message = "If the account exists, an verification code has been sent to the email." });

            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            user.PasswordResetToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(resetToken));
            user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);

            var otp = GenerateOtp(6);
            user.PasswordResetOtp = otp;
            user.PasswordResetOtpExpiry = DateTime.UtcNow.AddMinutes(10);

            await _userManager.UpdateAsync(user);
            await _db.SaveChangesAsync();
            // send OTP email
            await _emailService.SendPasswordResetOtpAsync(user, otp);

            return Ok(new { message = "If the account exists, an verification code has been sent to the email." });
        }

        // -----------------------
        // Reset password using OTP (mobile)
        // -----------------------
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPasswordWithOtp([FromBody] ResetPasswordWithOtpDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null) return BadRequest(new { message = "Invalid request" });

            // validate OTP
            if (string.IsNullOrEmpty(user.PasswordResetOtp) ||
                !user.PasswordResetOtp.Equals(dto.Otp, StringComparison.OrdinalIgnoreCase) ||
                !user.PasswordResetOtpExpiry.HasValue ||
                user.PasswordResetOtpExpiry.Value < DateTime.UtcNow)
            {
                return BadRequest(new { message = "Invalid or expired verification code" });
            }

            // validate stored token
            if (string.IsNullOrEmpty(user.PasswordResetToken) ||
                !user.PasswordResetTokenExpiry.HasValue ||
                user.PasswordResetTokenExpiry.Value < DateTime.UtcNow)
            {
                return BadRequest(new { message = "Reset token expired or missing. Please request a new verification code." });
            }

            // decode token
            string decodedToken;
            try
            {
                decodedToken = Encoding.UTF8.GetString(Convert.FromBase64String(user.PasswordResetToken));
            }
            catch
            {
                return BadRequest(new { message = "Invalid reset token stored. Please request a new verification code." });
            }

            // reset password
            var resetResult = await _userManager.ResetPasswordAsync(user, decodedToken, dto.NewPassword);
            if (!resetResult.Succeeded)
            {
                return BadRequest(new { errors = resetResult.Errors.Select(e => e.Description) });
            }

            // clear stored token & otp
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiry = null;
            user.PasswordResetOtp = null;
            user.PasswordResetOtpExpiry = null;
            await _userManager.UpdateAsync(user);

            // mark email confirmed if not
            if (!await _userManager.IsEmailConfirmedAsync(user))
            {
                user.EmailConfirmed = true;
                await _userManager.UpdateAsync(user);
            }

           
            await _db.SaveChangesAsync();


            return Ok(new { message = "Password updated successfully, Please login again" });
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost("update-password")]
        public async Task<IActionResult> UpdatePassword([FromBody] ResetPasswordDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                         ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "Invalid token / user." });

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return Unauthorized(new { message = "User not found." });

            if (dto.NewPassword != dto.ConfirmPassword)
                return BadRequest(new { message = "New password and confirmation do not match." });

            // Reset password using token since we don't require CurrentPassword
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetRes = await _userManager.ResetPasswordAsync(user, resetToken, dto.NewPassword);

            if (!resetRes.Succeeded)
                return BadRequest(new { errors = resetRes.Errors.Select(e => e.Description) });

            return Ok(new { message = "Password updated successfully, Please login again" });
        }

        // -----------------------
        // Refresh token (rotate)
        // -----------------------
        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshRequestDto body)
        {
            string? token = body?.RefreshToken ?? Request.Cookies["refreshToken"];
            if (string.IsNullOrEmpty(token))
                return BadRequest(new { message = "Refresh token required" });

            var existing = await _db.RefreshTokens.Include(r => r.ApplicationUser)
                .FirstOrDefaultAsync(r => r.Token == token);

            if (existing == null || !existing.IsActive)
                return Unauthorized(new { message = "Invalid refresh token" });

            // revoke old
            existing.Revoked = DateTime.UtcNow;
            existing.RevokedByIp = HttpContext.Connection.RemoteIpAddress?.ToString();

            // generate new tokens (access + new refresh string)
            var user = existing.ApplicationUser!;
            var tokens = await _jwtService.GenerateTokensAsync(user);

            // persist new refresh token
            var newRefresh = new RefreshToken
            {
                Token = tokens.RefreshToken,
                Expires = tokens.RefreshTokenExpiresAt,
                Created = DateTime.UtcNow,
                CreatedByIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
                ApplicationUserId = user.Id,
                ReplacedByToken = null
            };

            existing.ReplacedByToken = newRefresh.Token;
            _db.RefreshTokens.Add(newRefresh);
            await _db.SaveChangesAsync();

            return Ok(tokens);
        }


        // -----------------------
        // Revoke refresh token (logout)
        // -----------------------
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost("revoke")]
        public async Task<IActionResult> Revoke([FromBody] RefreshRequestDto body)
        {
            string? token = body?.RefreshToken ?? Request.Cookies["refreshToken"];
            if (string.IsNullOrEmpty(token)) return BadRequest(new { message = "Token required" });

            var existing = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.Token == token);
            if (existing == null) return NotFound();

            existing.Revoked = DateTime.UtcNow;
            existing.RevokedByIp = Request.HttpContext.Connection.RemoteIpAddress?.ToString();
            await _db.SaveChangesAsync();
            return Ok(new { message = "Revoked" });
        }

        // -----------------------
        // Protected: me
        // -----------------------
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpGet("User")]
        public async Task<IActionResult> Me()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                         ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            if (userId == null) return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return Unauthorized();

            var roles = await _userManager.GetRolesAsync(user);
            return Ok(new UserDto
            {
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                ProfilePictureUrl = user.ProfilePictureUrl,
                PhoneNumber = user.PhoneNumber,
                Roles = roles.ToArray()
            });
        }

        // -----------------------
        // Update profile (text + optional image)
        // -----------------------
        [HttpPut("profile")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> UpdateProfile([FromForm] ProfileUpdateDto dto, CancellationToken ct)
        {
            // 1. get current user id from token
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                         ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            // capture previous values for cleanup if needed
            var previousProfileUrl = user.ProfilePictureUrl;
            var previousPublicId = user.ProfilePicturePublicId;

            // 2. validate companyName edit
            if (!string.IsNullOrEmpty(dto.CompanyName) && !await _userManager.IsInRoleAsync(user, "Company"))
                return Forbid("Only company accounts can update CompanyName.");

            // 3. apply textual updates
            if (!string.IsNullOrEmpty(dto.FirstName)) user.FirstName = dto.FirstName;
            if (!string.IsNullOrEmpty(dto.LastName)) user.LastName = dto.LastName;
            if (!string.IsNullOrEmpty(dto.PhoneNumber)) user.PhoneNumber = dto.PhoneNumber;
            if (!string.IsNullOrEmpty(dto.CompanyName) && await _userManager.IsInRoleAsync(user, "Company"))
                user.CompanyName = dto.CompanyName;

            // 4. handle image if provided
            string? newUrl = null;
            string? newPublicId = null;

            if (dto.ProfileImage != null)
            {
                // basic validations
                var allowed = new[] { "image/jpeg", "image/png", "image/webp" };
                if (!allowed.Contains(dto.ProfileImage.ContentType?.ToLower()))
                    return BadRequest(new { message = "Unsupported image type. Allowed: jpeg, png, webp." });

                const long maxBytes = 5 * 1024 * 1024; // 5MB limite
                if (dto.ProfileImage.Length > maxBytes)
                    return BadRequest(new { message = "Image too large. Max 5MB." });

                try
                {
                    await using var ms = new MemoryStream();
                    await dto.ProfileImage.CopyToAsync(ms, ct);
                    ms.Position = 0;

                    // upload to Cloudinary
                    var uploadRes = await _cloudinary.UploadImageAsync(ms, dto.ProfileImage.FileName, dto.ProfileImage.ContentType, ct);
                    newUrl = uploadRes.Url;
                    newPublicId = uploadRes.PublicId;

                    // assign new url/public id to user
                    user.ProfilePictureUrl = newUrl;
                    user.ProfilePicturePublicId = newPublicId;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error uploading profile image");
                    return StatusCode(500, new { message = "Failed to upload image" });
                }
            }

            // 5. persist changes
            var updateRes = await _userManager.UpdateAsync(user);
            if (!updateRes.Succeeded)
            {
                // if upload succeeded but DB update failed -> delete uploaded image to avoid orphan
                if (!string.IsNullOrEmpty(newPublicId))
                {
                    try { await _cloudinary.DeleteAsync(newPublicId, ct); } catch { /* ignore */ }
                }
                return BadRequest(new { errors = updateRes.Errors.Select(e => e.Description) });
            }

            // 6. delete previous image from Cloudinary if existed and different (best-effort)
            try
            {
                if (!string.IsNullOrEmpty(previousPublicId) && previousPublicId != user.ProfilePicturePublicId)
                {
                    await _cloudinary.DeleteAsync(previousPublicId, ct);
                }
                else if (!string.IsNullOrEmpty(previousProfileUrl) && previousProfileUrl.Contains("/res.cloudinary.com/"))
                {
                    // fallback: try to extract public id from previous URL
                    var prevId = CloudinaryService.ExtractPublicIdFromUrl(previousProfileUrl, _configuration["Cloudinary:CloudName"]);
                    if (!string.IsNullOrEmpty(prevId) && prevId != user.ProfilePicturePublicId)
                    {
                        await _cloudinary.DeleteAsync(prevId, ct);
                    }
                }
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete previous Cloudinary image"); /* swallow */ }

            // 7. return updated user dto
            var roles = await _userManager.GetRolesAsync(user);
            var result = new UserDto
            {
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                PhoneNumber = user.PhoneNumber,
                ProfilePictureUrl = user.ProfilePictureUrl,
                Roles = roles.ToArray()
            };

            return Ok(result);
        }


        // -----------------------
        // Helpers
        // -----------------------
        private RefreshToken CreateRefreshToken(string? ipAddress)
        {
            var randomBytes = new byte[64];
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);

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

        private static string GenerateOtp(int length = 6)
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // avoid confusing chars
            var sb = new StringBuilder();
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            var bytes = new byte[length];
            rng.GetBytes(bytes);
            for (int i = 0; i < length; i++)
            {
                sb.Append(chars[bytes[i] % chars.Length]);
            }
            return sb.ToString();
        }
    }
}
