using DecorMate_Backend_Web_app.Models.DTOs;
using DecorMateBackend.Models.DTOs;
using DecorMateBackend.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DecorMateBackend.Controllers
{
    [Route("api/Account")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly IAccountService _accountService;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            IAccountService accountService,
            ILogger<AccountController> logger)
        {
            _accountService = accountService;
            _logger = logger;
        }


        // -----------------------
        // Register (creates user + sends OTP by email)
        // -----------------------
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var (success, errorMessage) = await _accountService.RegisterAsync(dto, null, CancellationToken.None);

            if (!success)
            {
                if (errorMessage?.Contains("failed to send confirmation email") == true)
                    return StatusCode(StatusCodes.Status502BadGateway, new { message = errorMessage });
                return BadRequest(new { message = errorMessage });
            }

            return Ok(new { message = "Registered. Please check your email for the verification code." });
        }

        // -----------------------
        // Register confirmation (OTP) -> returns tokens
        // -----------------------
        [HttpPost("register-confirmation")]
        public async Task<IActionResult> RegisterConfirmation([FromBody] ConfirmDto dto)
        {
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (success, tokens, errorMessage) = await _accountService.ConfirmRegistrationAsync(dto, clientIp, CancellationToken.None);

            if (!success)
                return BadRequest(new { message = errorMessage });

            return Ok(tokens);
        }

        [HttpPost("resend-otp")]
        public async Task<IActionResult> ResendOtp([FromBody] ResendOtpDto resendOtpDto)
        {
            var (success, errorMessage) = await _accountService.ResendOtpAsync(resendOtpDto, CancellationToken.None);

            if (!success)
                return BadRequest(new { message = errorMessage });

            return Ok(new { message = "Verification code resent" });
        }

        // -----------------------
        // Login (returns access + refresh)
        // -----------------------
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (success, tokens, errorMessage) = await _accountService.LoginAsync(dto, clientIp, CancellationToken.None);

            if (!success)
            {
                if (errorMessage == "Invalid credentials")
                    return Unauthorized(new { message = errorMessage });
                return BadRequest(new { message = errorMessage });
            }

            return Ok(tokens);
        }

        // -----------------------
        // Google Sign-In (mobile)
        // -----------------------
        [HttpPost("google-signin")]
        public async Task<IActionResult> GoogleSignIn([FromBody] ExternalAuthDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (success, tokens, errorMessage) = await _accountService.GoogleSignInAsync(dto, clientIp, CancellationToken.None);

            if (!success)
            {
                if (errorMessage?.Contains("Invalid") == true)
                    return Unauthorized(new { message = errorMessage });
                return BadRequest(new { message = errorMessage });
            }

            return Ok(tokens);
        }


        // -----------------------
        // Forgot password (mobile) => sends OTP and stores encoded reset token server-side
        // -----------------------
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            await _accountService.ForgotPasswordAsync(dto, CancellationToken.None);
            // Always return success to prevent email enumeration
            return Ok(new { message = "If the account exists, an verification code has been sent to the email." });
        }

        // -----------------------
        // Step 1: Verify OTP for password reset
        // -----------------------
        [HttpPost("verify-reset-otp")]
        public async Task<IActionResult> VerifyResetOtp([FromBody] VerifyResetOtpDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var (success, errorMessage) = await _accountService.VerifyResetOtpAsync(dto, CancellationToken.None);

            if (!success)
                return BadRequest(new { message = errorMessage });

            return Ok(new { message = "Verification code verified successfully. You can now set a new password." });
        }

        // -----------------------
        // Step 2: Set new password after OTP verification
        // -----------------------
        [HttpPost("set-new-password")]
        public async Task<IActionResult> SetNewPassword([FromBody] SetNewPasswordDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var (success, errorMessage) = await _accountService.SetNewPasswordAsync(dto, CancellationToken.None);

            if (!success)
                return BadRequest(new { message = errorMessage });

            return Ok(new { message = "Password updated successfully. Please login again." });
        }

        // -----------------------
        // Refresh token (rotate)
        // -----------------------
        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshRequestDto body)
        {
            string? token = body?.RefreshToken ?? Request.Cookies["refreshToken"];
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

            var (success, tokens, errorMessage) = await _accountService.RefreshTokenAsync(token, clientIp, CancellationToken.None);

            if (!success)
            {
                if (errorMessage == "Invalid refresh token")
                    return Unauthorized(new { message = errorMessage });
                return BadRequest(new { message = errorMessage });
            }

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
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

            var (success, errorMessage) = await _accountService.RevokeTokenAsync(token, clientIp, CancellationToken.None);

            if (!success)
            {
                if (errorMessage == "Token not found")
                    return NotFound(new { message = errorMessage });
                return BadRequest(new { message = errorMessage });
            }

            return Ok(new { message = "Revoked" });
        }

    }
    
}
