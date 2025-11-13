using DecorMate_Backend_Web_app.Data;
using DecorMate_Backend_Web_app.Models;
using DecorMate_Backend_Web_app.Models.DTOs;
using DecorMate_Backend_Web_app.Services;
using DecorMateBackend.Repositories;
using DecorMateBackend.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using DecorMateBackend.Models.DTOs;

namespace DecorMateBackend.Controllers
{
    [Route("api/Profile")]
    [ApiController]
    public class ProfileController : ControllerBase
    {

        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ProfileController> _logger;
        private readonly IConfiguration _configuration;
        private readonly CloudinaryService _cloudinary;

        public ProfileController(
            IUnitOfWork unitOfWork,
            ILogger<ProfileController> logger,
            IHttpClientFactory httpFactory,
            IConfiguration configuration,
            CloudinaryService cloudinaryService)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _configuration = configuration;
            _cloudinary = cloudinaryService;
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

            var user = await _unitOfWork.Users.FindByIdAsync(userId);
            if (user == null) return Unauthorized();

            var roles = await _unitOfWork.Users.GetRolesAsync(user);
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
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPut("Update-profile")]
        public async Task<IActionResult> UpdateProfile([FromForm] ProfileUpdateDto dto, CancellationToken ct)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                         ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var user = await _unitOfWork.Users.FindByIdAsync(userId);
            if (user == null)
                return NotFound();
            
            if (!string.IsNullOrEmpty(dto.CompanyName) && !await _unitOfWork.Users.IsInRoleAsync(user, "Company"))
                return Forbid("Only company accounts can update CompanyName.");

            if (!string.IsNullOrEmpty(dto.FirstName)) user.FirstName = dto.FirstName;
            if (!string.IsNullOrEmpty(dto.LastName)) user.LastName = dto.LastName;
            if (!string.IsNullOrEmpty(dto.PhoneNumber)) user.PhoneNumber = dto.PhoneNumber;
            if (!string.IsNullOrEmpty(dto.CompanyName) && await _unitOfWork.Users.IsInRoleAsync(user, "Company"))
                user.CompanyName = dto.CompanyName;
            
            var roles = await _unitOfWork.Users.GetRolesAsync(user);
            return Ok(new
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                PhoneNumber = user.PhoneNumber,
                Roles = roles.ToArray()
            });
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPut("update-profile-picture")]
        public async Task<IActionResult> UpdateProfilePicture([FromForm] ProfilePictureDto dto, CancellationToken ct)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                         ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var user = await _unitOfWork.Users.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            var prevUrl = user.ProfilePictureUrl;
            var prevPublicId = user.ProfilePicturePublicId;
            
            string? newUrl = null;
            string? newPublicId = null;

            if (dto.ProfileImage != null)
            {
                var allowed = new[] { "image/jpeg", "image/png", "image/webp" };
                if (!allowed.Contains(dto.ProfileImage.ContentType?.ToLower()))
                    return BadRequest(new { message = "Unsupported image type. Allowed: jpeg, png, webp." });

                const long maxBytes = 5 * 1024 * 1024;
                if (dto.ProfileImage.Length > maxBytes)
                    return BadRequest(new { message = "Image too large. Max 5MB." });

                try
                {
                    using var ms = new MemoryStream();
                    await dto.ProfileImage.CopyToAsync(ms, ct);
                    ms.Position = 0;

                    (newUrl, newPublicId) = await _cloudinary.UploadImageAsync(ms, dto.ProfileImage.FileName, "profiles", ct);

                    user.ProfilePictureUrl = newUrl;
                    user.ProfilePicturePublicId = newPublicId;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Cloudinary upload error");
                    return StatusCode(500, new { message = "Failed to upload image" });
                }
            }

            var upd = await _unitOfWork.Users.UpdateAsync(user);
            if (!upd.Succeeded)
            {
                if (!string.IsNullOrEmpty(newPublicId))
                {
                    await _cloudinary.DeleteAsync(newPublicId, ct);
                }
                return BadRequest(new { errors = upd.Errors.Select(e => e.Description) });
            }

            try
            {
                if (!string.IsNullOrEmpty(prevPublicId) && prevPublicId != user.ProfilePicturePublicId)
                {
                    await _cloudinary.DeleteAsync(prevPublicId, ct);
                }
                else if (!string.IsNullOrEmpty(prevUrl) && prevUrl.Contains("/res.cloudinary.com/"))
                {
                    var prevId = CloudinaryService.ExtractPublicIdFromUrl(prevUrl, _configuration["Cloudinary:CloudName"]);
                    if (!string.IsNullOrEmpty(prevId) && prevId != user.ProfilePicturePublicId)
                        await _cloudinary.DeleteAsync(prevId, ct);
                }
            }
            catch { /* ignore */ }

            return Ok(new {ProfilePictureUrl = user.ProfilePictureUrl});
        }


        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost("update-password")]
        public async Task<IActionResult> UpdatePassword([FromBody] ResetPasswordDto dto)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogError("A7a");
                return BadRequest(ModelState);
            }
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                         ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogError("invalid token");
                return Unauthorized(new { message = "Invalid token / user." });
            }
            var user = await _unitOfWork.Users.FindByIdAsync(userId);
            if (user == null)
            {
                _logger.LogError("User is not here ya 7omar");
                return Unauthorized(new { message = "User not found." });
            }

            if (dto.NewPassword != dto.ConfirmPassword)
                return BadRequest(new { message = "New password and confirmation do not match." });

            // Reset password using token since we don't require CurrentPassword
            var resetToken = await _unitOfWork.Users.GeneratePasswordResetTokenAsync(user);
            var resetRes = await _unitOfWork.Users.ResetPasswordAsync(user, resetToken, dto.NewPassword);

            if (!resetRes.Succeeded)
            {
                _logger.LogError("The Error is in line 365");
                return BadRequest(new { message = "aboooos", errors = resetRes.Errors.Select(e => e.Description) });
            }
            return Ok(new { message = "Password updated successfully, Please login again" });
        }
    }
}
