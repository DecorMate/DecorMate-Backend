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
using DecorMateBackend.Models.DTOs;
using DecorMateBackend.Models;
using System.Text.Json;
using System.Net.Http.Headers;

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
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromForm] ProfileUpdateDto dto, CancellationToken ct)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                         ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var prevUrl = user.ProfilePictureUrl;
            var prevPublicId = user.ProfilePicturePublicId;

            if (!string.IsNullOrEmpty(dto.CompanyName) && !await _userManager.IsInRoleAsync(user, "Company"))
                return Forbid("Only company accounts can update CompanyName.");

            if (!string.IsNullOrEmpty(dto.FirstName)) user.FirstName = dto.FirstName;
            if (!string.IsNullOrEmpty(dto.LastName)) user.LastName = dto.LastName;
            if (!string.IsNullOrEmpty(dto.PhoneNumber)) user.PhoneNumber = dto.PhoneNumber;
            if (!string.IsNullOrEmpty(dto.CompanyName) && await _userManager.IsInRoleAsync(user, "Company"))
                user.CompanyName = dto.CompanyName;

            string? newUrl = null;
            string? newPublicId = null;

            if (dto.ProfileImage != null)
            {
                var allowed = new[] { "image/jpeg", "image/png", "image/webp" };
                if (!allowed.Contains(dto.ProfileImage.ContentType?.ToLower()))
                    return BadRequest(new { message = "Unsupported image type. Allowed: jpeg, png, webp." });

                const long maxBytes = 5 * 1024 * 1024;
                if (dto.ProfileImage.Length > maxBytes) return BadRequest(new { message = "Image too large. Max 5MB." });

                try
                {
                    await using var ms = new MemoryStream();
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

            var upd = await _userManager.UpdateAsync(user);
            if (!upd.Succeeded)
            {
                if (!string.IsNullOrEmpty(newPublicId))
                {
                    await _cloudinary.DeleteAsync(newPublicId, ct);
                }
                return BadRequest(new { errors = upd.Errors.Select(e => e.Description) });
            }

            // delete previous image if different
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

            var roles = await _userManager.GetRolesAsync(user);
            return Ok(new
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                PhoneNumber = user.PhoneNumber,
                ProfilePictureUrl = user.ProfilePictureUrl,
                Roles = roles.ToArray()
            });
        }

        // ----------------------
        // Generate image using external AI service, upload to Cloudinary and store history
        // ----------------------
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost("generate-image")]
        public async Task<IActionResult> GenerateImage([FromBody] GenerateImageRequestDto dto, CancellationToken ct)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Prompt))
                return BadRequest(new { message = "Prompt required" });

            // get user id from token
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                         ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            // read config
            var aiEndpoint = _configuration["AI:Endpoint"];
            if (string.IsNullOrWhiteSpace(aiEndpoint))
                return StatusCode(500, new { message = "AI:Endpoint missing in config" });

            var aiApiKey = _configuration["AI:ApiKey"]; // optional

            var client = _httpFactory.CreateClient(); // you can use CreateClient("ai") if configured
            if (!string.IsNullOrEmpty(aiApiKey))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", aiApiKey);

            // *** IMPORTANT: the AI expects { "description": "..." } according to your note ***
            var payload = new
            {
                description = dto.Prompt
            };

            HttpResponseMessage aiResponse;
            string aiBody = "";
            try
            {
                var json = JsonSerializer.Serialize(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                aiResponse = await client.PostAsync(aiEndpoint, content, ct);
                aiBody = await aiResponse.Content.ReadAsStringAsync(ct);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("AI request cancelled");
                return StatusCode(504, new { message = "AI request timed out" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI request failed");
                return StatusCode(502, new { message = "Failed to contact AI service", detail = ex.Message });
            }

            if (!aiResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("AI returned {Status}: {Body}", aiResponse.StatusCode, aiBody);
                return StatusCode(502, new { message = "AI service error", detail = aiBody });
            }

            // Determine what the AI returned:
            Stream imageStream = null!;
            string contentType = aiResponse.Content.Headers.ContentType?.MediaType ?? "";

            try
            {
                if (contentType.StartsWith("image", StringComparison.OrdinalIgnoreCase))
                {
                    imageStream = await aiResponse.Content.ReadAsStreamAsync(ct);
                }
                else
                {
                    // try parse JSON
                    JsonDocument? doc = null;
                    try
                    {
                        doc = JsonDocument.Parse(aiBody);
                    }
                    catch
                    {
                        doc = null;
                    }

                    if (doc != null)
                    {
                        var root = doc.RootElement;

                        // common: { "image_url": "https://..." }
                        if (root.TryGetProperty("image_url", out var urlEl) && urlEl.ValueKind == JsonValueKind.String)
                        {
                            var url = urlEl.GetString();
                            if (string.IsNullOrEmpty(url)) return StatusCode(502, new { message = "AI returned empty image_url" });

                            // download image bytes (from AI URL)
                            var download = await client.GetAsync(url, ct);
                            if (!download.IsSuccessStatusCode)
                                return StatusCode(502, new { message = "Failed to download image from AI URL", detail = await download.Content.ReadAsStringAsync(ct) });

                            imageStream = await download.Content.ReadAsStreamAsync(ct);
                            contentType = download.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
                        }
                        // alternative: { "image_base64": "..." }
                        else if (root.TryGetProperty("image_base64", out var b64El) && b64El.ValueKind == JsonValueKind.String)
                        {
                            var b64 = b64El.GetString() ?? "";
                            var bytes = Convert.FromBase64String(b64);
                            imageStream = new MemoryStream(bytes);
                            contentType = "image/png";
                        }
                        // maybe nested: { "data": { "image_base64": "..." } }
                        else if (root.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Object &&
                                 dataEl.TryGetProperty("image_base64", out var nestedB64))
                        {
                            var b64 = nestedB64.GetString() ?? "";
                            var bytes = Convert.FromBase64String(b64);
                            imageStream = new MemoryStream(bytes);
                            contentType = "image/png";
                        }
                        else
                        {
                            // unknown json -> return raw for debugging
                            return StatusCode(502, new { message = "Unexpected AI response", detail = aiBody });
                        }
                    }
                    else
                    {
                        // not json, not image: unexpected
                        return StatusCode(502, new { message = "Unexpected AI response", detail = aiBody });
                    }
                }

                // upload to Cloudinary (assumes _cloudinary.UploadImageAsync(Stream, fileName, folder, ct) returns (Url, PublicId))
                await using (imageStream)
                {
                    var fileName = $"{(string.IsNullOrWhiteSpace(dto.Title) ? "generated" : dto.Title)}-{Guid.NewGuid():N}.png";
                    var (url, publicId) = await _cloudinary.UploadImageAsync(imageStream, fileName, "generated", ct);

                    // save history to DB
                    var gi = new GeneratedImage
                    {
                        ApplicationUserId = userId,
                        ImageUrl = url,
                        CloudinaryPublicId = publicId,
                        ProjectTitle = string.IsNullOrWhiteSpace(dto.Title) ? null : dto.Title,
                        Prompt = dto.Prompt,
                        CreatedAt = DateTime.UtcNow
                    };
                    _db.GeneratedImages.Add(gi);
                    await _db.SaveChangesAsync(ct);

                    var resultDto = new GeneratedImageDto
                    {
                        Id = gi.Id,
                        Url = gi.ImageUrl,
                        PublicId = gi.CloudinaryPublicId,
                        Title = gi.ProjectTitle,
                        Prompt = gi.Prompt,
                        CreatedAt = gi.CreatedAt
                    };

                    return Ok(resultDto);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing AI response / uploading image");
                return StatusCode(500, new { message = "Failed to store generated image", detail = ex.Message });
            }
        }

        // ----------------------
        // Get image generation history for the current user
        // ----------------------
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpGet("image-history")]
        public async Task<IActionResult> ImageHistory(int page = 1, int pageSize = 20, CancellationToken ct = default)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                         ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var q = _db.GeneratedImages
                       .Where(g => g.ApplicationUserId == userId)
                       .OrderByDescending(g => g.CreatedAt);

            var total = await q.CountAsync(ct);
            var items = await q.Skip((page - 1) * pageSize).Take(pageSize)
                               .Select(g => new GeneratedImageDto
                               {
                                   Id = g.Id,
                                   Url = g.ImageUrl,
                                   PublicId = g.CloudinaryPublicId,
                                   Prompt = g.Prompt,
                                   Title = g.ProjectTitle,
                                   CreatedAt = g.CreatedAt
                               })
                               .ToListAsync(ct);

            return Ok(new { total, page, pageSize, items });
        }
 


        // -----------------------
        // Helpers
        // -----------------------
        
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
