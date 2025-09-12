using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using DecorMate_Backend_Web_app.Data;
using DecorMate_Backend_Web_app.Models;
using DecorMate_Backend_Web_app.Models.DTOs;
using Microsoft.AspNetCore.Authentication;
using DecorMate_Backend_Web_app.Services;
using DecorMateBackend.Models.Enums;
using DecorMateBackend.Services;

namespace DecorMate_Backend_Web_app.Controllers
{
    public class AuthController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _db;
        private readonly EmailService _emailService;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ApplicationDbContext db,
           EmailService emailService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _db = db;
            _emailService = emailService;
        }
        [Authorize(Policy = "CompanyOnly")]
        [HttpGet("/Auth/Profile")]
        public async Task<IActionResult> Profile()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Challenge();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var model = new ProfileViewModel
            {
                Id = user.Id,
                Email = user.Email!,
                FirstName = user.FirstName ?? string.Empty,
                LastName = user.LastName ?? string.Empty,
                ProfilePictureUrl = user.ProfilePictureUrl,
                PhoneNumber = user.PhoneNumber,
                CompanyName = user.CompanyName 
            };

            return View("~/Views/Auth/Profile.cshtml", model);
        }

        // POST: /Auth/Profile
        [Authorize(Policy = "CompanyOnly")]
        [HttpPost("/Auth/Profile")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProfilePost(ProfileViewModel model)
        {
            if (!ModelState.IsValid) return View("~/Views/Auth/Profile.cshtml", model);

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Challenge();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            // Update allowed fields
            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.ProfilePictureUrl = model.ProfilePictureUrl;
            user.PhoneNumber = model.PhoneNumber;
            // if ApplicationUser has CompanyName property:
            // user.CompanyName = model.CompanyName;

            var res = await _userManager.UpdateAsync(user);
            if (!res.Succeeded)
            {
                foreach (var err in res.Errors) ModelState.AddModelError(string.Empty, err.Description);
                return View("~/Views/Auth/Profile.cshtml", model);
            }

            TempData["Success"] = "Profile updated successfully.";
            return RedirectToAction("Profile");
        }
        // ---------------------------
        // Register (MVC)
        // GET: /Auth/Register
        [HttpGet("/Auth/Register")]
        [AllowAnonymous]
        public IActionResult Register() => View("~/Views/Auth/Register.cshtml");

        // POST: /Auth/Register
        [HttpPost("/Auth/Register")]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterPost(RegisterDto model)
        {
            if (!ModelState.IsValid) return View("~/Views/Auth/Register.cshtml", model);

            var existing = await _userManager.FindByEmailAsync(model.Email);
            if (existing != null)
            {
                ModelState.AddModelError(string.Empty, "Email already in use");
                return View("~/Views/Auth/Register.cshtml", model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                Provider = AuthProvider.Local
            };

            var createRes = await _userManager.CreateAsync(user, model.Password);
            if (!createRes.Succeeded)
            {
                foreach (var e in createRes.Errors) ModelState.AddModelError(string.Empty, e.Description);
                return View("~/Views/Auth/Register.cshtml", model);
            }
            await _userManager.AddToRoleAsync(user, "Company");

            await _signInManager.SignInAsync(user, isPersistent: false);
            RedirectToAction("RegisterConfirmation", "Auth");

            // Optionally assign role (for web you may assign Company based on form or admin)
            // await _userManager.AddToRoleAsync(user, "Company");

            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var callback = Url.Action("ConfirmEmail", "Auth", new { userId = user.Id, token }, protocol: Request.Scheme);
            await _emailService.SendConfirmationAsync(user, $"{Request.Scheme}://{Request.Host}/Auth/ConfirmEmail",false);

            return RedirectToAction(nameof(RegisterConfirmation));
        }

        [HttpGet("/Auth/RegisterConfirmation")]
        [AllowAnonymous]
        public IActionResult RegisterConfirmation() => View("~/Views/Auth/RegisterConfirmation.cshtml");

        // ---------------------------
        // Login (MVC)
        // GET: /Auth/Login
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
            if (!await _userManager.IsInRoleAsync(user, "Company"))
            {
                return View("Error",new ErrorViewModel{Message= "Only company accounts can login from here.", });
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
                // if user belongs to Company role, redirect to company dashboard maybe
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

        // POST: /Auth/Logout
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
        [HttpGet("/Auth/ConfirmEmail")]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            if (userId == null || token == null) return RedirectToAction("Index", "Home");

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var result = await _userManager.ConfirmEmailAsync(user, token);
            if (result.Succeeded) return View("~/Views/Auth/ConfirmEmail.cshtml");

            return View("~/Views/Shared/Error.cshtml", new ErrorViewModel { RequestId = HttpContext.TraceIdentifier, Message = "Email confirmation failed." });
        }

        // ---------------------------
        // Forgot Password (MVC)
        // GET: /Auth/ForgotPassword
        [HttpGet("/Auth/ForgotPassword")]
        [AllowAnonymous]
        public IActionResult ForgotPassword() => View("~/Views/Auth/ForgotPassword.cshtml");

        // POST: /Auth/ForgotPassword
        [HttpPost("/Auth/ForgotPassword")]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPasswordPost(ForgotPasswordDto model)
        {
            if (!ModelState.IsValid) return View("~/Views/Auth/ForgotPassword.cshtml", model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null) return RedirectToAction(nameof(ForgotPasswordConfirmation));

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var callback = Url.Action("ResetPassword", "Auth", new { email = user.Email, token }, protocol: Request.Scheme);
            await _emailService.SendConfirmationAsync(user, $"{Request.Scheme}://{Request.Host}/Auth/ConfirmEmail", true);


            return RedirectToAction(nameof(ForgotPasswordConfirmation));
        }

        [HttpGet("/Auth/ForgotPasswordConfirmation")]
        [AllowAnonymous]
        public IActionResult ForgotPasswordConfirmation() => View("~/Views/Auth/ForgotPasswordConfirmation.cshtml");

        // ---------------------------
        // Reset Password (MVC)
        // GET: /Auth/ResetPassword
        [HttpGet("/Auth/ResetPassword")]
        [AllowAnonymous]
        public IActionResult ResetPassword(string email, string token)
        {
            var model = new ResetPasswordDto { Email = email, Token = token };
            return View("~/Views/Auth/ResetPassword.cshtml", model);
        }

        // POST: /Auth/ResetPassword
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
    }
}
