using DecorMate_Backend_Web_app.Models;
using Microsoft.AspNetCore.Identity;

namespace DecorMateBackend.Repositories
{
    public interface IUserRepository
    {
        Task<ApplicationUser> FindByEmailAsync(string email);
        Task<IdentityResult> DeleteAsync(ApplicationUser user);
        Task<IdentityResult> CreateAsync(ApplicationUser user, string password);
        Task<IdentityResult> AddToRoleAsync(ApplicationUser user, string role);
        Task<IdentityResult> UpdateAsync(ApplicationUser user);
        Task<IList<string>> GetRolesAsync(ApplicationUser user);
        Task<SignInResult> CheckPasswordSignInAsync(ApplicationUser user, string password, bool lockoutOnFailure);
        Task<bool> IsEmailConfirmedAsync(ApplicationUser user);
        Task<string> GeneratePasswordResetTokenAsync(ApplicationUser user);
        Task<IdentityResult> ResetPasswordAsync(ApplicationUser user, string token, string newPassword);
        Task<ApplicationUser> FindByIdAsync(string userId);

        Task<bool> IsInRoleAsync(ApplicationUser user, string role);
        Task<IEnumerable<ApplicationUser>> GetUsersByRolesAsync(string[] roleNames);
    }
}
