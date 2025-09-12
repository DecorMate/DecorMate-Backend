// DecorMateBackend.Services/EmailService.cs
using DecorMate_Backend_Web_app.Models; // ApplicationUser
using DecorMateBackend.Models;
using Microsoft.AspNetCore.Identity;

namespace DecorMateBackend.Services
{
    public class EmailService
    {
        private readonly IEmailSenderO _sender;
        private readonly ILogger<EmailService> _logger;
        private readonly UserManager<ApplicationUser> _userManager;

        public EmailService(IEmailSenderO sender, ILogger<EmailService> logger, UserManager<ApplicationUser> userManager)
        {
            _sender = sender ?? throw new ArgumentNullException(nameof(sender));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        }

        public async Task SendConfirmationAsync(ApplicationUser user, string callbackUrl, bool includeButton, string? otp = null, CancellationToken ct = default)
        {
            var display = user.FirstName ?? user.Email ?? "User";
            var (html, text) = EmailTemplates.ConfirmEmail(display, includeButton ? callbackUrl : null, otp, showButton: includeButton);

            // Save OTP to user if provided (ensure it's already set by caller if preferred)
            if (!string.IsNullOrEmpty(otp))
            {
                user.OtpCode = otp;
                user.OtpExpiry = DateTime.UtcNow.AddMinutes(10);
                var upd = await _userManager.UpdateAsync(user);
                if (!upd.Succeeded)
                    _logger.LogWarning("Failed to save verification code for {Email}: {Errors}", user.Email, string.Join(", ", upd.Errors.Select(e => e.Description)));
            }

            await _sender.SendTemplatedEmailAsync(user.Email!, "Confirm your DecorMate account", html, text, null, ct);
        }

        public async Task SendPasswordResetOtpAsync(ApplicationUser user, string otp, CancellationToken ct = default)
        {
            var display = user.FirstName ?? user.Email ?? "User";
            var (html, text) = EmailTemplates.PasswordResetOtp(display, otp);

            // save OTP + expiry
            user.PasswordResetOtp = otp;
            user.PasswordResetOtpExpiry = DateTime.UtcNow.AddMinutes(10);
            user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);
            await _userManager.UpdateAsync(user);

            await _sender.SendTemplatedEmailAsync(user.Email!, "Reset your DecorMate password", html, text, null, ct);
        }
    }
}
