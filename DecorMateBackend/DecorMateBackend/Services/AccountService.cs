using AutoMapper;
using DecorMate_Backend_Web_app.Models;
using DecorMate_Backend_Web_app.Models.DTOs;
using DecorMate_Backend_Web_app.Services;
using DecorMateBackend.Models;
using DecorMateBackend.Models.DTOs;
using DecorMateBackend.Models.Enums;
using DecorMateBackend.Repositories;
using DecorMateBackend.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using System.Text;
using Google.Apis.Auth;

namespace DecorMateBackend.Services
{
    public class AccountService : IAccountService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly JwtService _jwtService;
        private readonly EmailService _emailService;
        private readonly IMapper _mapper;
        private readonly ILogger<AccountService> _logger;
        private readonly IConfiguration _configuration;

        public AccountService(
            IUnitOfWork unitOfWork,
            JwtService jwtService,
            EmailService emailService,
            IMapper mapper,
            ILogger<AccountService> logger,
            IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _jwtService = jwtService;
            _emailService = emailService;
            _mapper = mapper;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<(bool Success, string? ErrorMessage)> RegisterAsync(RegisterDto dto, string? callbackUrl = null, CancellationToken ct = default)
        {
            try
            {
                // Check if user already exists
                var existing = await _unitOfWork.Users.FindByEmailAsync(dto.Email);
                if (existing != null)
                {
                    if (existing.EmailConfirmed)
                        return (false, "Email already in use");

                    // User exists but not confirmed -> delete to allow re-register
                    var delRes = await _unitOfWork.Users.DeleteAsync(existing);
                    if (!delRes.Succeeded)
                    {
                        _logger.LogWarning("Failed to delete existing unconfirmed user {Email}: {Errors}",
                            dto.Email, string.Join(",", delRes.Errors.Select(e => e.Description)));
                    }
                }

                // Map DTO to entity
                var user = _mapper.Map<ApplicationUser>(dto);

                // Create user
                var createRes = await _unitOfWork.Users.CreateAsync(user, dto.Password);
                if (!createRes.Succeeded)
                {
                    _logger.LogWarning("Create user failed for {Email}: {Errors}", 
                        dto.Email, string.Join(",", createRes.Errors.Select(e => e.Description)));
                    return (false, string.Join(", ", createRes.Errors.Select(e => e.Description)));
                }

                // Assign default role
                await _unitOfWork.Users.AddToRoleAsync(user, "User");

                // Generate and persist OTP
                var otp = OTPCodeGenerator.Generate(6);
                user.OtpCode = otp;
                user.OtpExpiry = DateTime.UtcNow.AddMinutes(10);
                var upd = await _unitOfWork.Users.UpdateAsync(user);
                if (!upd.Succeeded)
                {
                    _logger.LogWarning("Failed to update user with verification code for {Email}: {Errors}", 
                        user.Email, string.Join(",", upd.Errors.Select(e => e.Description)));
                }

                // Send confirmation email
                try
                {
                    await _emailService.SendConfirmationAsync(user, callbackUrl ?? string.Empty, includeButton: false, otp: otp, ct: ct);
                    _logger.LogInformation("Register: verification code email sent successfully to {Email}", user.Email);
                    return (true, null);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Register: Failed to send verification code email for {Email}", user.Email);
                    return (false, $"Registered but failed to send confirmation email: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during registration for {Email}", dto.Email);
                return (false, "An unexpected error occurred during registration");
            }
        }

        public async Task<(bool Success, AuthResponseDto? Tokens, string? ErrorMessage)> ConfirmRegistrationAsync(
            ConfirmDto dto, string? clientIpAddress, CancellationToken ct = default)
        {
            try
            {
                if (string.IsNullOrEmpty(dto.Email) || string.IsNullOrEmpty(dto.Otp))
                    return (false, null, "Email and verification code are required");

                var user = await _unitOfWork.Users.FindByEmailAsync(dto.Email);
                if (user == null)
                    return (false, null, "Invalid email");

                if (user.EmailConfirmed)
                    return (false, null, "Email already confirmed");

                if (user.OtpCode != dto.Otp || !user.OtpExpiry.HasValue || user.OtpExpiry.Value < DateTime.UtcNow)
                    return (false, null, "Invalid or expired verification code");

                // Confirm email
                user.EmailConfirmed = true;
                user.OtpCode = null;
                user.OtpExpiry = null;
                await _unitOfWork.Users.UpdateAsync(user);

                // Generate tokens
                var tokens = await _jwtService.GenerateTokensAsync(user);
                var roles = await _unitOfWork.Users.GetRolesAsync(user);
                var userDto = _mapper.Map<UserDto>(user);
                userDto.Roles = roles.ToArray();
                tokens.User = userDto;

                // Create refresh token
                var refreshEntity = new RefreshToken
                {
                    Token = tokens.RefreshToken,
                    Expires = tokens.RefreshTokenExpiresAt,
                    Created = DateTime.UtcNow,
                    CreatedByIp = clientIpAddress,
                    ApplicationUserId = user.Id
                };

                _unitOfWork.RefreshTokens.AddRefreshToken(refreshEntity);
                await _unitOfWork.SaveChangesAsync(ct);

                tokens.RefreshToken = refreshEntity.Token;
                return (true, tokens, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration confirmation for {Email}", dto.Email);
                return (false, null, "An error occurred during confirmation");
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> ResendOtpAsync(ResendOtpDto dto, CancellationToken ct = default)
        {
            try
            {
                if (string.IsNullOrEmpty(dto.Email))
                    return (false, "Email is required");

                var user = await _unitOfWork.Users.FindByEmailAsync(dto.Email);
                if (user == null)
                    return (false, "Invalid email");

                if (user.EmailConfirmed)
                    return (false, "Email already confirmed");

                // Generate new OTP
                var otp = OTPCodeGenerator.Generate(6);
                user.OtpCode = otp;
                user.OtpExpiry = DateTime.UtcNow.AddMinutes(10);

                await _unitOfWork.Users.UpdateAsync(user);
                await _unitOfWork.SaveChangesAsync(ct);

                // Send email
                await _emailService.SendConfirmationAsync(user, string.Empty, includeButton: false, otp: otp, ct: ct);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resending OTP for {Email}", dto.Email);
                return (false, "An error occurred while resending the verification code");
            }
        }

        public async Task<(bool Success, AuthResponseDto? Tokens, string? ErrorMessage)> LoginAsync(
            LoginDto dto, string? clientIpAddress, CancellationToken ct = default)
        {
            try
            {
                var user = await _unitOfWork.Users.FindByEmailAsync(dto.Email);
                if (user == null)
                {
                    _logger.LogWarning("Login failed: User {Email} not found", dto.Email);
                    return (false, null, "Invalid credentials");
                }

                var check = await _unitOfWork.Users.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true);
                if (!check.Succeeded)
                {
                    _logger.LogWarning("Login failed: Password incorrect for {Email}", dto.Email);
                    return (false, null, "Invalid credentials");
                }

                if (!await _unitOfWork.Users.IsEmailConfirmedAsync(user))
                    return (false, null, "Email not confirmed");

                var roles = await _unitOfWork.Users.GetRolesAsync(user);
                if (roles.FirstOrDefault("Company") == "Company")
                    return (false, null, "Use the Dashboard");

                // Generate tokens
                var tokens = await _jwtService.GenerateTokensAsync(user);
                var userDto = _mapper.Map<UserDto>(user);
                userDto.Roles = roles.ToArray();
                tokens.User = userDto;

                // Create refresh token
                var refreshEntity = new RefreshToken
                {
                    Token = tokens.RefreshToken,
                    Expires = tokens.RefreshTokenExpiresAt,
                    Created = DateTime.UtcNow,
                    CreatedByIp = clientIpAddress,
                    ApplicationUserId = user.Id
                };

                _unitOfWork.RefreshTokens.AddRefreshToken(refreshEntity);
                await _unitOfWork.SaveChangesAsync(ct);

                tokens.RefreshToken = refreshEntity.Token;
                return (true, tokens, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for {Email}", dto.Email);
                return (false, null, "An error occurred during login");
            }
        }

        public async Task<(bool Success, AuthResponseDto? Tokens, string? ErrorMessage)> GoogleSignInAsync(
            ExternalAuthDto dto, string? clientIpAddress, CancellationToken ct = default)
        {
            try
            {
                if (string.IsNullOrEmpty(dto.IdToken))
                    return (false, null, "ID token is required");

                // Verify Google ID token
                var googleClientId = _configuration["Authentication:Google:ClientId"];
                if (string.IsNullOrEmpty(googleClientId))
                {
                    _logger.LogError("Google Client ID not configured");
                    return (false, null, "Google authentication not configured");
                }

                GoogleJsonWebSignature.Payload payload;
                try
                {
                    var settings = new GoogleJsonWebSignature.ValidationSettings
                    {
                        Audience = new[] { googleClientId }
                    };
                    payload = await GoogleJsonWebSignature.ValidateAsync(dto.IdToken, settings);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Invalid Google ID token");
                    return (false, null, "Invalid Google token");
                }

                // Extract user information from Google token
                var email = payload.Email;
                var firstName = payload.GivenName ?? "";
                var lastName = payload.FamilyName ?? "";
                var profilePictureUrl = payload.Picture;

                if (string.IsNullOrEmpty(email))
                {
                    _logger.LogWarning("Google token missing email");
                    return (false, null, "Email not provided by Google");
                }

                // Check if user exists
                var user = await _unitOfWork.Users.FindByEmailAsync(email);
                
                if (user == null)
                {
                    // Create new user
                    user = new ApplicationUser
                    {
                        UserName = email,
                        Email = email,
                        EmailConfirmed = true, // Google already verified the email
                        FirstName = firstName,
                        LastName = lastName,
                        Provider = AuthProvider.Google,
                        ProfilePictureUrl = profilePictureUrl
                    };

                    var createResult = await _unitOfWork.Users.CreateAsync(user, GenerateRandomPassword());
                    if (!createResult.Succeeded)
                    {
                        _logger.LogError("Failed to create Google user {Email}: {Errors}", 
                            email, string.Join(", ", createResult.Errors.Select(e => e.Description)));
                        return (false, null, "Failed to create user account");
                    }

                    // Assign User role for mobile users
                    await _unitOfWork.Users.AddToRoleAsync(user, "User");
                    _logger.LogInformation("Created new Google user {Email}", email);
                }
                else
                {
                    // User exists - verify they can use mobile app
                    var userRoles = await _unitOfWork.Users.GetRolesAsync(user);
                    if (userRoles.Contains("Company"))
                    {
                        return (false, null, "Company accounts cannot sign in via mobile app");
                    }

                    // Update profile picture if changed
                    if (!string.IsNullOrEmpty(profilePictureUrl) && user.ProfilePictureUrl != profilePictureUrl)
                    {
                        user.ProfilePictureUrl = profilePictureUrl;
                        await _unitOfWork.Users.UpdateAsync(user);
                    }
                }

                // Generate JWT tokens
                var tokens = await _jwtService.GenerateTokensAsync(user);
                var roles = await _unitOfWork.Users.GetRolesAsync(user);
                var userDto = _mapper.Map<UserDto>(user);
                userDto.Roles = roles.ToArray();
                tokens.User = userDto;

                // Create refresh token
                var refreshEntity = new RefreshToken
                {
                    Token = tokens.RefreshToken,
                    Expires = tokens.RefreshTokenExpiresAt,
                    Created = DateTime.UtcNow,
                    CreatedByIp = clientIpAddress,
                    ApplicationUserId = user.Id
                };

                _unitOfWork.RefreshTokens.AddRefreshToken(refreshEntity);
                await _unitOfWork.SaveChangesAsync(ct);

                tokens.RefreshToken = refreshEntity.Token;
                _logger.LogInformation("Google sign-in successful for {Email}", email);
                return (true, tokens, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Google sign-in");
                return (false, null, "An error occurred during Google sign-in");
            }
        }

        private static string GenerateRandomPassword()
        {
            // Generate a secure random password for Google users (they won't use it)
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
            var random = new System.Security.Cryptography.RNGCryptoServiceProvider();
            var bytes = new byte[32];
            random.GetBytes(bytes);
            return new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
        }

        public async Task<(bool Success, AuthResponseDto? Tokens, string? ErrorMessage)> RefreshTokenAsync(
            string? refreshToken, string? clientIpAddress, CancellationToken ct = default)
        {
            try
            {
                if (string.IsNullOrEmpty(refreshToken))
                    return (false, null, "Refresh token required");

                var existing = await _unitOfWork.RefreshTokens.GetRefreshTokenAsync(refreshToken, true);
                if (existing == null || !existing.IsActive)
                    return (false, null, "Invalid refresh token");

                // Revoke old token
                existing.Revoked = DateTime.UtcNow;
                existing.RevokedByIp = clientIpAddress;

                // Generate new tokens
                var user = existing.ApplicationUser!;
                var tokens = await _jwtService.GenerateTokensAsync(user);

                // Create new refresh token
                var newRefresh = new RefreshToken
                {
                    Token = tokens.RefreshToken,
                    Expires = tokens.RefreshTokenExpiresAt,
                    Created = DateTime.UtcNow,
                    CreatedByIp = clientIpAddress,
                    ApplicationUserId = user.Id,
                    ReplacedByToken = null
                };

                existing.ReplacedByToken = newRefresh.Token;
                _unitOfWork.RefreshTokens.AddRefreshToken(newRefresh);
                await _unitOfWork.SaveChangesAsync(ct);

                return (true, tokens, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token");
                return (false, null, "An error occurred while refreshing the token");
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> RevokeTokenAsync(
            string? refreshToken, string? clientIpAddress, CancellationToken ct = default)
        {
            try
            {
                if (string.IsNullOrEmpty(refreshToken))
                    return (false, "Token required");

                var existing = await _unitOfWork.RefreshTokens.GetRefreshTokenAsync(refreshToken, false);
                if (existing == null)
                    return (false, "Token not found");

                existing.Revoked = DateTime.UtcNow;
                existing.RevokedByIp = clientIpAddress;
                await _unitOfWork.SaveChangesAsync(ct);

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking token");
                return (false, "An error occurred while revoking the token");
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> ForgotPasswordAsync(
            ForgotPasswordDto dto, CancellationToken ct = default)
        {
            try
            {
                var user = await _unitOfWork.Users.FindByEmailAsync(dto.Email);
                // Don't reveal whether user exists
                if (user == null)
                    return (true, null); // Return success to prevent email enumeration

                // Generate reset token
                var resetToken = await _unitOfWork.Users.GeneratePasswordResetTokenAsync(user);
                user.PasswordResetToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(resetToken));
                user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);

                // Generate OTP
                var otp = OTPCodeGenerator.Generate(6);
                user.PasswordResetOtp = otp;
                user.PasswordResetOtpExpiry = DateTime.UtcNow.AddMinutes(10);

                await _unitOfWork.Users.UpdateAsync(user);
                await _unitOfWork.SaveChangesAsync(ct);

                // Send OTP email
                await _emailService.SendPasswordResetOtpAsync(user, otp, ct);

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during forgot password for {Email}", dto.Email);
                // Still return success to prevent email enumeration
                return (true, null);
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> VerifyResetOtpAsync(
            VerifyResetOtpDto dto, CancellationToken ct = default)
        {
            try
            {
                // Step 1: Find user
                var user = await _unitOfWork.Users.FindByEmailAsync(dto.Email);
                if (user == null)
                    return (false, "Invalid request");

                // Step 2: Verify OTP exists
                if (string.IsNullOrEmpty(user.PasswordResetOtp))
                {
                    return (false, "No verification code found. Please request a new password reset.");
                }

                // Step 3: Verify OTP expiry
                if (!user.PasswordResetOtpExpiry.HasValue)
                {
                    return (false, "Verification code has expired. Please request a new password reset.");
                }

                if (user.PasswordResetOtpExpiry.Value < DateTime.UtcNow)
                {
                    return (false, "Verification code has expired. Please request a new password reset.");
                }

                // Step 4: Verify OTP matches
                if (!user.PasswordResetOtp.Equals(dto.Otp, StringComparison.OrdinalIgnoreCase))
                {
                    return (false, "Invalid verification code. Please check and try again.");
                }

                // Step 5: Verify reset token exists and is valid
                if (string.IsNullOrEmpty(user.PasswordResetToken))
                {
                    return (false, "Reset token missing. Please request a new password reset.");
                }

                if (!user.PasswordResetTokenExpiry.HasValue || user.PasswordResetTokenExpiry.Value < DateTime.UtcNow)
                {
                    return (false, "Reset token expired. Please request a new password reset.");
                }

                // Step 6: Mark OTP as verified (clear OTP but keep token for password reset)
                user.PasswordResetOtpVerifiedAt = DateTime.UtcNow;
                user.PasswordResetOtp = null; // Clear OTP after verification
                user.PasswordResetOtpExpiry = null;
                
                await _unitOfWork.Users.UpdateAsync(user);
                await _unitOfWork.SaveChangesAsync(ct);

                _logger.LogInformation("OTP verified successfully for password reset for user {Email}", dto.Email);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying OTP for password reset for {Email}", dto.Email);
                return (false, "An error occurred while verifying the code");
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> SetNewPasswordAsync(
            SetNewPasswordDto dto, CancellationToken ct = default)
        {
            try
            {
                // Step 1: Find user
                var user = await _unitOfWork.Users.FindByEmailAsync(dto.Email);
                if (user == null)
                    return (false, "Invalid request");

                // Step 2: Verify that OTP was verified first
                if (!user.PasswordResetOtpVerifiedAt.HasValue)
                {
                    return (false, "Please verify the code first before setting a new password.");
                }

                // Step 3: Verify OTP verification is still valid (within 15 minutes)
                var verificationAge = DateTime.UtcNow - user.PasswordResetOtpVerifiedAt.Value;
                if (verificationAge.TotalMinutes > 15)
                {
                    return (false, "Verification code has expired. Please request a new password reset and verify again.");
                }

                // Step 4: Verify reset token exists and is valid
                if (string.IsNullOrEmpty(user.PasswordResetToken))
                {
                    return (false, "Reset token missing. Please request a new password reset.");
                }

                if (!user.PasswordResetTokenExpiry.HasValue || user.PasswordResetTokenExpiry.Value < DateTime.UtcNow)
                {
                    return (false, "Reset token expired. Please request a new password reset.");
                }

                // Step 5: Decode token
                string decodedToken;
                try
                {
                    decodedToken = Encoding.UTF8.GetString(Convert.FromBase64String(user.PasswordResetToken));
                }
                catch
                {
                    return (false, "Invalid reset token. Please request a new password reset.");
                }

                // Step 6: Validate new password
                if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 6)
                {
                    return (false, "Password must be at least 6 characters long.");
                }

                // Step 7: Reset password using the decoded token
                var resetResult = await _unitOfWork.Users.ResetPasswordAsync(user, decodedToken, dto.NewPassword);
                if (!resetResult.Succeeded)
                {
                    return (false, string.Join(", ", resetResult.Errors.Select(e => e.Description)));
                }

                // Step 8: Clear all reset-related fields after successful password reset
                user.PasswordResetToken = null;
                user.PasswordResetTokenExpiry = null;
                user.PasswordResetOtp = null;
                user.PasswordResetOtpExpiry = null;
                user.PasswordResetOtpVerifiedAt = null;
                await _unitOfWork.Users.UpdateAsync(user);

                // Step 9: Mark email confirmed if not already confirmed
                if (!await _unitOfWork.Users.IsEmailConfirmedAsync(user))
                {
                    user.EmailConfirmed = true;
                    await _unitOfWork.Users.UpdateAsync(user);
                }

                await _unitOfWork.SaveChangesAsync(ct);
                _logger.LogInformation("Password reset successful for user {Email}", dto.Email);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting new password for {Email}", dto.Email);
                return (false, "An error occurred while setting the new password");
            }
        }

    }
}
