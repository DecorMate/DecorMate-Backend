// DecorMateBackend.Services/EmailService.cs
using DecorMate_Backend_Web_app.Models; // ApplicationUser
using DecorMateBackend.Models;
using DecorMateBackend.Repositories;
using Microsoft.AspNetCore.Identity;

namespace DecorMateBackend.Services
{
    public class EmailService
    {
        private readonly IEmailSenderO _sender;
        private readonly ILogger<EmailService> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUnitOfWork _unitOfWork;

        public EmailService(
            IEmailSenderO sender, 
            ILogger<EmailService> logger, 
            UserManager<ApplicationUser> userManager,
            IUnitOfWork unitOfWork)
        {
            _sender = sender ?? throw new ArgumentNullException(nameof(sender));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        public async Task SendConfirmationAsync(ApplicationUser user, string callbackBaseUrl, bool includeButton, string? otp = null, CancellationToken ct = default)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            // Generate the actual Identity confirmation token
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);

            // Create EmailConfirmation record with GUID
            var emailConfirmation = new EmailConfirmation
            {
                ConfirmationGuid = Guid.NewGuid(),
                Token = token, // Store the actual token securely in database
                ApplicationUserId = user.Id,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(24), // Token expires in 24 hours
                IsUsed = false
            };

            // Store in database
            _unitOfWork.EmailConfirmations.AddEmailConfirmation(emailConfirmation);
            await _unitOfWork.SaveChangesAsync(ct);

            // Build callback URL with GUID only (no token exposed)
            // callbackBaseUrl expected like: "https://yourhost/Auth/ConfirmEmail"
            string callback;
            if (string.IsNullOrWhiteSpace(callbackBaseUrl))
            {
                callback = string.Empty;
            }
            else
            {
                // Use GUID instead of token - much more secure and cleaner
                var separator = callbackBaseUrl.Contains('?') ? "&" : "?";
                callback = $"{callbackBaseUrl}{separator}guid={emailConfirmation.ConfirmationGuid}";
            }

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
