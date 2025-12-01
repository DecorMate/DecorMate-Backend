using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using DecorMate_Backend_Web_app.Data;
using DecorMate_Backend_Web_app.Models;
using DecorMate_Backend_Web_app.Models.DTOs;
using DecorMateBackend.Services;
using DecorMate_Backend_Web_app.Services;
using DecorMateBackend.Models.DTOs;
using DecorMateBackend.Repositories;
using System.Security.Claims;

namespace DecorMateBackend.Controllers
{
    public class AuthController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _db;
        private readonly EmailService _emailService;
        private readonly CloudinaryService _cloudinary;
        private readonly ILogger<AuthController> _logger;
        private readonly IUnitOfWork _unitOfWork;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ApplicationDbContext db,
            EmailService emailService,
            CloudinaryService cloudinary,
            ILogger<AuthController> logger,
            IUnitOfWork unitOfWork)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _db = db;
            _emailService = emailService;
            _cloudinary = cloudinary;
            _logger = logger;
            _unitOfWork = unitOfWork;
        }

        // ---------------------------
        // Profile (Company only)
        // GET: /Auth/Profile
        // ---------------------------
        [Authorize(Policy = "CompanyOnly")]
        [HttpGet("/Auth/Profile")]
        public async Task<IActionResult> Profile()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var vm = new MvcProfileUpdateViewModel
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                PhoneNumber = user.PhoneNumber,
                CompanyName = user.CompanyName,
                // don't populate ProfileImage - that's an upload field
                Location = user.Location, // optional: if ApplicationUser has it
                ProfessionalCategory = user.ProfessionalCategory // optional
            };

            ViewData["ProfilePictureUrl"] = user.ProfilePictureUrl;
            return View("~/Views/Auth/Profile.cshtml", vm);
        }

        // ---------------------------
        // Profile POST (update)
        // ---------------------------
        [Authorize(Policy = "CompanyOnly")]
        [HttpPost("/Auth/Profile")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProfilePost(MvcProfileUpdateViewModel model, CancellationToken ct)
        {
            if (!ModelState.IsValid) return View("~/Views/Auth/Profile.cshtml", model);

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            // apply textual updates (only allowed fields)
            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.PhoneNumber = model.PhoneNumber;
            if (!string.IsNullOrWhiteSpace(model.CompanyName))
            {
                // allow CompanyName only if Company role (should be always true due to policy)
                if (await _userManager.IsInRoleAsync(user, "Company"))
                    user.CompanyName = model.CompanyName;
            }

            // optional fields
            user.Location = model.Location ?? user.Location;
            user.ProfessionalCategory = model.ProfessionalCategory ?? user.ProfessionalCategory;

            // handle profile image upload (if provided)
            string? newUrl = null;
            string? newPublicId = null;
            var previousPublicId = user.ProfilePicturePublicId;
            var previousUrl = user.ProfilePictureUrl;

            if (model.ProfileImage != null && model.ProfileImage.Length > 0)
            {
                // basic validations
                var allowed = new[] { "image/jpeg", "image/png", "image/webp" };
                if (!allowed.Contains(model.ProfileImage.ContentType?.ToLower()))
                {
                    ModelState.AddModelError(string.Empty, "Unsupported image type. Allowed: jpeg, png, webp.");
                    return View("~/Views/Auth/Profile.cshtml", model);
                }

                const long maxBytes = 5 * 1024 * 1024; // 5MB
                if (model.ProfileImage.Length > maxBytes)
                {
                    ModelState.AddModelError(string.Empty, "Image too large. Max 5MB.");
                    return View("~/Views/Auth/Profile.cshtml", model);
                }

                try
                {
                    await using var ms = new MemoryStream();
                    await model.ProfileImage.CopyToAsync(ms, ct);
                    ms.Position = 0;

                    // upload to cloudinary (folder "profiles")
                    var (url, publicId) = await _cloudinary.UploadImageAsync(ms, model.ProfileImage.FileName, "profiles", ct);

                    newUrl = url;
                    newPublicId = publicId;

                    user.ProfilePictureUrl = newUrl;
                    user.ProfilePicturePublicId = newPublicId;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed uploading profile image for user {UserId}", userId);
                    ModelState.AddModelError(string.Empty, "Failed to upload image. Try again later.");
                    return View("~/Views/Auth/Profile.cshtml", model);
                }
            }

            // persist changes
            var res = await _userManager.UpdateAsync(user);
            if (!res.Succeeded)
            {
                // rollback uploaded image if DB update failed
                if (!string.IsNullOrEmpty(newPublicId))
                {
                    try { await _cloudinary.DeleteAsync(newPublicId, ct); } catch { /* ignore */ }
                }

                foreach (var e in res.Errors) ModelState.AddModelError(string.Empty, e.Description);
                return View("~/Views/Auth/Profile.cshtml", model);
            }

            // best-effort delete previous image from Cloudinary if replaced
            try
            {
                if (!string.IsNullOrEmpty(previousPublicId) && !string.IsNullOrEmpty(newPublicId) && previousPublicId != newPublicId)
                {
                    await _cloudinary.DeleteAsync(previousPublicId, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete previous Cloudinary asset for user {UserId}", userId);
            }

            TempData["Success"] = "Profile updated successfully.";
            return RedirectToAction("Profile");
        }

        // ---------------------------
        // Register (MVC)
        // GET: /Auth/Register
        // ---------------------------
        [HttpGet("/Auth/Register")]
        [AllowAnonymous]
        public IActionResult Register()
        {
            // you said you want a top navigation: home/about/plan — that's view work
            return View("~/Views/Auth/Register.cshtml", new MvcRegisterViewModel());
        }

        // POST: /Auth/Register
        [HttpPost("/Auth/Register")]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterPost(MvcRegisterViewModel model, CancellationToken ct)
        {
            if (!ModelState.IsValid) return View("~/Views/Auth/Register.cshtml", model);

            // if email exists but not confirmed, optionally delete stale record (like you had)
            var existing = await _userManager.FindByEmailAsync(model.Email);
            if (existing != null)
            {
                if (existing.EmailConfirmed)
                {
                    ModelState.AddModelError(string.Empty, "Email already in use");
                    return View("~/Views/Auth/Register.cshtml", model);
                }
                // delete stale entry to allow re-register
                await _userManager.DeleteAsync(existing);
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                Provider = AuthProvider.Local,
                EmailConfirmed = false,
                CompanyName = model.CompanyName,
                Location = model.Location,
                ProfessionalCategory = model.IsCompany ? model.ProfessionalCategory : null
            };

            var createRes = await _userManager.CreateAsync(user, model.Password);
            if (!createRes.Succeeded)
            {
                foreach (var e in createRes.Errors) ModelState.AddModelError(string.Empty, e.Description);
                return View("~/Views/Auth/Register.cshtml", model);
            }

            // assign Company role by default for MVC registrations
            await _userManager.AddToRoleAsync(user, "Company");

            // For companies: send confirmation email with button/link only (no OTP)
            // Pass base URL without query parameters - EmailService will add them properly
            var callbackUrl = $"{Request.Scheme}://{Request.Host}/Auth/ConfirmEmail";
            try
            {
                // includeButton = true for MVC, otp = null for companies (button/link only)
                await _emailService.SendConfirmationAsync(user, callbackUrl, includeButton: true, otp: null, ct: ct);
                _logger.LogInformation("Register: Confirmation email sent successfully to {Email}", user.Email);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send registration email to {Email}", user.Email);
                // choose to return success but warn
                TempData["Warning"] = "Registered but failed to send confirmation email. Contact support.";
                return RedirectToAction(nameof(RegisterConfirmation));
            }

            return RedirectToAction(nameof(RegisterConfirmation));
        }

        [HttpGet("/Auth/RegisterConfirmation")]
        [AllowAnonymous]
        public IActionResult RegisterConfirmation() => View("~/Views/Auth/RegisterConfirmation.cshtml");

        // ---------------------------
        // Login (MVC)
        // GET: /Auth/Login
        // ---------------------------
        [HttpGet("/Auth/Login")]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View("~/Views/Auth/Login.cshtml");
        }

        // POST: /Auth/Login
        [HttpPost("/Auth/Login")]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LoginPost(LoginDto model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (!ModelState.IsValid) return View("~/Views/Auth/Login.cshtml", model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return View("~/Views/Auth/Login.cshtml", model);
            }

            // only Company role allowed on MVC login
            if (!await _userManager.IsInRoleAsync(user, "Company"))
            {
                return View("~/Views/Shared/Error.cshtml", new ErrorViewModel { RequestId = HttpContext.TraceIdentifier, Message = "Only company accounts can login from here." });
            }

            if (!await _userManager.IsEmailConfirmedAsync(user))
            {
                ModelState.AddModelError(string.Empty, "Email not confirmed.");
                return View("~/Views/Auth/Login.cshtml", model);
            }

            var res = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);
            if (res.Succeeded)
            {
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
                return RedirectToAction("Index", "Home");
            }

            if (res.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty, "Account locked.");
                return View("~/Views/Auth/Login.cshtml", model);
            }

            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View("~/Views/Auth/Login.cshtml", model);
        }

        // ---------------------------
        // External Login (Google/Facebook)
        // POST: /Auth/ExternalLogin
        // ---------------------------
        [HttpPost("/Auth/ExternalLogin")]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public IActionResult ExternalLogin(string provider, string? returnUrl = null)
        {
            var redirectUrl = Url.Action("ExternalLoginCallback", "Auth", new { returnUrl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return Challenge(properties, provider);
        }

        // ---------------------------
        // External Login Callback
        // GET: /Auth/ExternalLoginCallback
        // ---------------------------
        [HttpGet("/Auth/ExternalLoginCallback")]
        [AllowAnonymous]
        public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
        {
            returnUrl = returnUrl ?? Url.Action("Index", "Home");

            if (remoteError != null)
            {
                ModelState.AddModelError(string.Empty, $"Error from external provider: {remoteError}");
                return View("~/Views/Auth/Login.cshtml");
            }

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                ModelState.AddModelError(string.Empty, "Error loading external login information.");
                return View("~/Views/Auth/Login.cshtml");
            }

            // Sign in the user with this external login provider if the user already has a login
            var signInResult = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);

            if (signInResult.Succeeded)
            {
                return LocalRedirect(returnUrl);
            }

            if (signInResult.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty, "Account locked.");
                return View("~/Views/Auth/Login.cshtml");
            }

            // If the user does not have an account, then create one
            var email = info.Principal.FindFirstValue(System.Security.Claims.ClaimTypes.Email);
            var firstName = info.Principal.FindFirstValue(System.Security.Claims.ClaimTypes.GivenName) ?? "";
            var lastName = info.Principal.FindFirstValue(System.Security.Claims.ClaimTypes.Surname) ?? "";

            if (string.IsNullOrEmpty(email))
            {
                ModelState.AddModelError(string.Empty, "Email not provided by external provider.");
                return View("~/Views/Auth/Login.cshtml");
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                // Create new user
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FirstName = firstName,
                    LastName = lastName,
                    Provider = info.LoginProvider == "Google" ? AuthProvider.Google : AuthProvider.Facebook
                };

                var createResult = await _userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    foreach (var error in createResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    return View("~/Views/Auth/Login.cshtml");
                }

                // Assign Company role by default
                await _userManager.AddToRoleAsync(user, "Company");
            }
            else
            {
                // Check if user has Company role
                if (!await _userManager.IsInRoleAsync(user, "Company"))
                {
                    ModelState.AddModelError(string.Empty, "Only company accounts can login from here.");
                    return View("~/Views/Auth/Login.cshtml");
                }
            }

            // Add external login to user if it doesn't exist
            var existingLogins = await _userManager.GetLoginsAsync(user);
            var hasExternalLogin = existingLogins.Any(l => l.LoginProvider == info.LoginProvider && l.ProviderKey == info.ProviderKey);
            
            if (!hasExternalLogin)
            {
                var addLoginResult = await _userManager.AddLoginAsync(user, info);
                if (!addLoginResult.Succeeded)
                {
                    foreach (var error in addLoginResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    return View("~/Views/Auth/Login.cshtml");
                }
            }

            // Sign in the user
            await _signInManager.SignInAsync(user, isPersistent: false);
            return LocalRedirect(returnUrl);
        }

        // ---------------------------
        // Logout
        // POST: /Auth/Logout
        // ---------------------------
        [HttpPost("/Auth/Logout")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        // ---------------------------
        // Confirm Email
        // GET: /Auth/ConfirmEmail
        // ---------------------------
        [HttpGet("/Auth/ConfirmEmail")]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmEmail(string guid)
        {
            if (string.IsNullOrEmpty(guid) || !Guid.TryParse(guid, out var confirmationGuid))
            {
                _logger.LogWarning("Invalid confirmation GUID provided");
                return View("~/Views/Shared/Error.cshtml", new ErrorViewModel 
                { 
                    RequestId = HttpContext.TraceIdentifier, 
                    Message = "Invalid confirmation link." 
                });
            }

            // Retrieve the email confirmation record using GUID
            var emailConfirmation = await _unitOfWork.EmailConfirmations.GetEmailConfirmationByGuidAsync(confirmationGuid, includeUser: true);
            
            if (emailConfirmation == null)
            {
                _logger.LogWarning("Email confirmation not found for GUID: {Guid}", confirmationGuid);
                return View("~/Views/Shared/Error.cshtml", new ErrorViewModel 
                { 
                    RequestId = HttpContext.TraceIdentifier, 
                    Message = "Confirmation link not found or has expired." 
                });
            }

            // Check if already used
            if (emailConfirmation.IsUsed)
            {
                _logger.LogWarning("Email confirmation already used for GUID: {Guid}", confirmationGuid);
                return View("~/Views/Shared/Error.cshtml", new ErrorViewModel 
                { 
                    RequestId = HttpContext.TraceIdentifier, 
                    Message = "This confirmation link has already been used." 
                });
            }

            // Check if expired
            if (emailConfirmation.IsExpired)
            {
                _logger.LogWarning("Email confirmation expired for GUID: {Guid}", confirmationGuid);
                return View("~/Views/Shared/Error.cshtml", new ErrorViewModel 
                { 
                    RequestId = HttpContext.TraceIdentifier, 
                    Message = "This confirmation link has expired. Please request a new one." 
                });
            }

            // Get the user
            var user = emailConfirmation.ApplicationUser;
            if (user == null)
            {
                _logger.LogError("User not found for email confirmation GUID: {Guid}", confirmationGuid);
                return View("~/Views/Shared/Error.cshtml", new ErrorViewModel 
                { 
                    RequestId = HttpContext.TraceIdentifier, 
                    Message = "User not found." 
                });
            }

            // Confirm email using the stored token
            var result = await _userManager.ConfirmEmailAsync(user, emailConfirmation.Token);
            
            if (result.Succeeded)
            {
                // Mark confirmation as used
                await _unitOfWork.EmailConfirmations.MarkAsUsedAsync(emailConfirmation.Id);
                await _unitOfWork.SaveChangesAsync();
                
                _logger.LogInformation("Email confirmed successfully for user: {Email}", user.Email);
                return View("~/Views/Auth/ConfirmEmail.cshtml");
            }

            // Log errors
            _logger.LogWarning("Email confirmation failed for user {Email}. Errors: {Errors}", 
                user.Email, string.Join(", ", result.Errors.Select(e => e.Description)));
            
            return View("~/Views/Shared/Error.cshtml", new ErrorViewModel 
            { 
                RequestId = HttpContext.TraceIdentifier, 
                Message = "Email confirmation failed. Please try again or request a new confirmation link." 
            });
        }


        // ---------------------------
        // Forgot Password (MVC)
        // GET/POST
        // ---------------------------
        [HttpGet("/Auth/ForgotPassword")]
        [AllowAnonymous]
        public IActionResult ForgotPassword() => View("~/Views/Auth/ForgotPassword.cshtml");

        [HttpPost("/Auth/ForgotPassword")]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPasswordPost(ForgotPasswordDto model)
        {
            if (!ModelState.IsValid) return View("~/Views/Auth/ForgotPassword.cshtml", model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null) return RedirectToAction(nameof(ForgotPasswordConfirmation));

            // Generate reset token + OTP and send via EmailService
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            // store base64 token and OTP on user (optional) or include token in callback URL
            var callback = Url.Action("ResetPassword", "Auth", new { email = user.Email, token }, protocol: Request.Scheme);

            // use EmailService to send confirmation with link/button
            try
            {
                // includeButton true so user can click in email
                await _emailService.SendConfirmationAsync(user, callback ?? string.Empty, includeButton: true, otp: null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send forgot-password email to {Email}", user.Email);
            }

            return RedirectToAction(nameof(ForgotPasswordConfirmation));
        }

        [HttpGet("/Auth/ForgotPasswordConfirmation")]
        [AllowAnonymous]
        public IActionResult ForgotPasswordConfirmation() => View("~/Views/Auth/ForgotPasswordConfirmation.cshtml");

        // ---------------------------
        // Reset Password (MVC)
        // GET/POST
        // ---------------------------
        [HttpGet("/Auth/ResetPassword")]
        [AllowAnonymous]
        public IActionResult ResetPassword(string email, string token)
        {
            var model = new ResetPasswordDto { Email = email, Token = token };
            return View("~/Views/Auth/ResetPassword.cshtml", model);
        }

        [HttpPost("/Auth/ResetPassword")]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPasswordPost(ResetPasswordDto model)
        {
            if (!ModelState.IsValid) return View("~/Views/Auth/ResetPassword.cshtml", model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null) return RedirectToAction(nameof(ResetPasswordConfirmation));

            var res = await _userManager.ResetPasswordAsync(user, model.Token, model.NewPassword);
            if (!res.Succeeded)
            {
                foreach (var e in res.Errors) ModelState.AddModelError(string.Empty, e.Description);
                return View("~/Views/Auth/ResetPassword.cshtml", model);
            }

            return RedirectToAction(nameof(ResetPasswordConfirmation));
        }

        [HttpGet("/Auth/ResetPasswordConfirmation")]
        [AllowAnonymous]
        public IActionResult ResetPasswordConfirmation() => View("~/Views/Auth/ResetPasswordConfirmation.cshtml");

        // ---------------------------
        // Helpers
        // ---------------------------
        private static string GenerateOtp(int length = 6)
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var sb = new System.Text.StringBuilder();
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            var bytes = new byte[length];
            rng.GetBytes(bytes);
            for (int i = 0; i < length; i++) sb.Append(chars[bytes[i] % chars.Length]);
            return sb.ToString();
        }
    }
}
