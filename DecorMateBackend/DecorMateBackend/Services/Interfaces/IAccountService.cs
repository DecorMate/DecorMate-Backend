using DecorMate_Backend_Web_app.Models;
using DecorMate_Backend_Web_app.Models.DTOs;
using DecorMateBackend.Models;
using DecorMateBackend.Models.DTOs;
using Microsoft.AspNetCore.Identity;

namespace DecorMateBackend.Services.Interfaces
{
    public interface IAccountService
    {
        // Registration
        Task<(bool Success, string? ErrorMessage)> RegisterAsync(RegisterDto dto, string? callbackUrl = null, CancellationToken ct = default);
        Task<(bool Success, AuthResponseDto? Tokens, string? ErrorMessage)> ConfirmRegistrationAsync(ConfirmDto dto, string? clientIpAddress, CancellationToken ct = default);
        Task<(bool Success, string? ErrorMessage)> ResendOtpAsync(ResendOtpDto dto, CancellationToken ct = default);

        // Authentication
        Task<(bool Success, AuthResponseDto? Tokens, string? ErrorMessage)> LoginAsync(LoginDto dto, string? clientIpAddress, CancellationToken ct = default);
        Task<(bool Success, AuthResponseDto? Tokens, string? ErrorMessage)> GoogleSignInAsync(ExternalAuthDto dto, string? clientIpAddress, CancellationToken ct = default);
        Task<(bool Success, AuthResponseDto? Tokens, string? ErrorMessage)> RefreshTokenAsync(string? refreshToken, string? clientIpAddress, CancellationToken ct = default);
        Task<(bool Success, string? ErrorMessage)> RevokeTokenAsync(string? refreshToken, string? clientIpAddress, CancellationToken ct = default);

        // Password Management
        Task<(bool Success, string? ErrorMessage)> ForgotPasswordAsync(ForgotPasswordDto dto, CancellationToken ct = default);
        Task<(bool Success, string? ErrorMessage)> VerifyResetOtpAsync(VerifyResetOtpDto dto, CancellationToken ct = default);
        Task<(bool Success, string? ErrorMessage)> SetNewPasswordAsync(SetNewPasswordDto dto, CancellationToken ct = default);
    }
}
