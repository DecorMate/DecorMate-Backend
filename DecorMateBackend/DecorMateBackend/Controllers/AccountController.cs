using DecorMate_Backend_Web_app.Data;
using DecorMate_Backend_Web_app.Models;
using DecorMate_Backend_Web_app.Models.DTOs;
using DecorMate_Backend_Web_app.Services;
using DecorMateBackend.Controllers.Api;
using DecorMateBackend.Repositories;
using DecorMateBackend.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text;

namespace DecorMateBackend.Controllers
{
    [Route("api/Account")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly JwtService _jwtService;
        private readonly ILogger<AuthApiController> _logger;
        private readonly EmailService _emailService;

        public AccountController(
            IUnitOfWork unitOfWork,
            JwtService jwtService,
            ILogger<AuthApiController> logger,
             EmailService emailService)
        {
            _unitOfWork = unitOfWork;
            _jwtService = jwtService;
            _logger = logger;
            _emailService = emailService;
        }


        // -----------------------
        // Register (creates user + sends OTP by email)
        // -----------------------

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var existing = await _unitOfWork.Users.FindByEmailAsync(dto.Email);
            if (existing != null)
            {
                if (existing.EmailConfirmed)
                    return BadRequest(new { message = "Email already in use" });
                // user exists but not confirmed -> delete to allow re-register
                var delRes = await _unitOfWork.Users.DeleteAsync(existing);
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

            var createRes = await _unitOfWork.Users.CreateAsync(user, dto.Password);
            if (!createRes.Succeeded)
            {
                _logger.LogWarning("Create user failed for {Email}: {Errors}", dto.Email, string.Join(",", createRes.Errors.Select(e => e.Description)));
                return BadRequest(createRes.Errors.Select(e => e.Description));
            }

            await _unitOfWork.Users.AddToRoleAsync(user, "User");

            // Generate and persist OTP
            var otp = OTPService.GenerateOtp(6);
            user.OtpCode = otp;
            user.OtpExpiry = DateTime.UtcNow.AddMinutes(10);
            var upd = await _unitOfWork.Users.UpdateAsync(user);
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

            var user = await _unitOfWork.Users.FindByEmailAsync(dto.Email);
            if (user == null) return BadRequest(new { message = "Invalid email" });

            if (user.EmailConfirmed)
                return BadRequest(new { message = "Email already confirmed" });

            if (user.OtpCode != dto.Otp || !user.OtpExpiry.HasValue || user.OtpExpiry.Value < DateTime.UtcNow)
                return BadRequest(new { message = "Invalid or expired verification code" });

            user.EmailConfirmed = true;
            user.OtpCode = null;
            user.OtpExpiry = null;

            await _unitOfWork.Users.UpdateAsync(user);

            var tokens = await _jwtService.GenerateTokensAsync(user);
            var roles = await _unitOfWork.Users.GetRolesAsync(user);
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
            //   _logger.LogInformation($"this is the refresh Entity {refreshEntity}");
            _unitOfWork.RefreshTokens.AddRefreshToken(refreshEntity);
            await _unitOfWork.SaveChangesAsync();

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

            var user = await _unitOfWork.Users.FindByEmailAsync(dto.Email);
            if (user == null)
                return Unauthorized(new { message = "Invalid credentials" });

            var check = await _unitOfWork.Users.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true);
            if (!check.Succeeded)
                return Unauthorized(new { message = "Invalid credentials" });

            if (!await _unitOfWork.Users.IsEmailConfirmedAsync(user))
                return BadRequest(new { message = "Email not confirmed" });
            var roles = await _unitOfWork.Users.GetRolesAsync(user);
            if (roles.FirstOrDefault("Company") == "Company")
                return BadRequest(new { message = "Use the Dashboard" });
            var tokens = await _jwtService.GenerateTokensAsync(user);
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

            _unitOfWork.RefreshTokens.AddRefreshToken(refreshEntity);
            await _unitOfWork.SaveChangesAsync();

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

            var user = await _unitOfWork.Users.FindByEmailAsync(dto.Email);
            // don't reveal whether user exists
            if (user == null)
                return Ok(new { message = "If the account exists, an verification code has been sent to the email." });

            var resetToken = await _unitOfWork.Users.GeneratePasswordResetTokenAsync(user);
            user.PasswordResetToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(resetToken));
            user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);

            var otp = OTPService.GenerateOtp(6);
            user.PasswordResetOtp = otp;
            user.PasswordResetOtpExpiry = DateTime.UtcNow.AddMinutes(10);

            await _unitOfWork.Users.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();
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

            var user = await _unitOfWork.Users.FindByEmailAsync(dto.Email);
            if (user == null)
                return BadRequest(new { message = "Invalid request" });

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
            var resetResult = await _unitOfWork.Users.ResetPasswordAsync(user, decodedToken, dto.NewPassword);
            if (!resetResult.Succeeded)
            {
                return BadRequest(new { errors = resetResult.Errors.Select(e => e.Description) });
            }

            // clear stored token & otp
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiry = null;
            user.PasswordResetOtp = null;
            user.PasswordResetOtpExpiry = null;
            await _unitOfWork.Users.UpdateAsync(user);

            // mark email confirmed if not
            if (!await _unitOfWork.Users.IsEmailConfirmedAsync(user))
            {
                user.EmailConfirmed = true;
                await _unitOfWork.Users.UpdateAsync(user);
            }

            await _unitOfWork.SaveChangesAsync();


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


            var existing = await _unitOfWork.RefreshTokens.GetRefreshTokenAsync(token, true);

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
            _unitOfWork.RefreshTokens.AddRefreshToken(newRefresh);
            await _unitOfWork.SaveChangesAsync();

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

            var existing = await _unitOfWork.RefreshTokens.GetRefreshTokenAsync(token, false);
            if (existing == null) return NotFound();

            existing.Revoked = DateTime.UtcNow;
            existing.RevokedByIp = Request.HttpContext.Connection.RemoteIpAddress?.ToString();
            await _unitOfWork.SaveChangesAsync();
            return Ok(new { message = "Revoked" });
        }

    }
    
}
