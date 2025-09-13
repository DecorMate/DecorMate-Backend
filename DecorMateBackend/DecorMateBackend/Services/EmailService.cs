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

        public async Task SendConfirmationAsync(ApplicationUser user, string callbackBaseUrl, bool includeButton, string? otp = null, CancellationToken ct = default)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            if (string.IsNullOrEmpty(callbackBaseUrl)) throw new ArgumentNullException(nameof(callbackBaseUrl));

            // Generate token and URL-encode it
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var encodedToken = System.Web.HttpUtility.UrlEncode(token);

            // Build callback URL with userId + token
            // callbackBaseUrl expected like: "https://yourhost/Auth/ConfirmEmail"
            var callback = $"{callbackBaseUrl}?userId={user.Id}&token={encodedToken}";

            // Prepare HTML + text using template utility (you can reuse EmailTemplates.ConfirmEmail)
            var (html, text) = EmailTemplates.ConfirmEmail(user.FirstName ?? user.Email, callback, otp, includeButton);

            // send via underlying sender
            await _sender.SendTemplatedEmailAsync(user.Email!, "Confirm your DecorMate account", html, text, embedLocalLogoPath: null, ct);
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
