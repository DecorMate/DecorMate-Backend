using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DecorMate_Backend_Web_app.Data;
using DecorMate_Backend_Web_app.Models;
using DecorMate_Backend_Web_app.Models.DTOs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using DecorMateBackend.Models.DTOs;
using Microsoft.AspNetCore.Identity;

namespace DecorMateBackend.Controllers
{
    public class PaymentsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _config;
        private readonly ILogger<PaymentsController> _logger;
        private readonly IHttpClientFactory _httpFactory;
        private readonly UserManager<ApplicationUser> _userManager;

        public PaymentsController(
            ApplicationDbContext db,
            IConfiguration config,
            ILogger<PaymentsController> logger,
            IHttpClientFactory httpFactory,
            UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _config = config;
            _logger = logger;
            _httpFactory = httpFactory;
            _userManager = userManager;
        }

        // GET /Payments/Plan
        [HttpGet("/Payments/Plan")]
        public IActionResult Plan()
        {
            // prices here for display
            var model = new
            {
                Standard = new { Price = 0m, Description = "Standard — free listing" },
                Premium = new { Price = 50m, Description = "Premium — featured/sponsored (monthly)" } // example currency units
            };
            return View("~/Views/Payments/Plan.cshtml", model);
        }

        // POST /Payments/Start
        [HttpPost("/Payments/Start")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Start([FromForm] PaymentRequestDto dto)
        {
            if (!ModelState.IsValid) return View("~/Views/Payments/Plan.cshtml", dto);

            // get user if signed in (for MVC)
            string? userId = null;
            if (User?.Identity?.IsAuthenticated ?? false)
            {
                userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            }

            // determine amount (adjust to your pricing)
            decimal amount = dto.Plan?.ToLower() == "premium" ? 50m : 0m;

            var record = new PaymentRecord
            {
                ApplicationUserId = userId ?? "ANONYMOUS",
                Plan = dto.Plan,
                Amount = amount,
                PhoneNumber = dto.PhoneNumber,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };  
            _db.PaymentRecords.Add(record);
            await _db.SaveChangesAsync();

            // if amount == 0 -> immediate success (standard free)
            if (amount == 0m)
            {
                record.Status = "Completed";
                record.TransactionId = "FREE-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                record.CompletedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                TempData["Success"] = "You have been enrolled to the Standard plan.";
                return RedirectToAction("PaymentConfirmation", new { id = record.Id });
            }

            // For paid plans: prepare Vodafone Cash API call (placeholder)
            // We simulate creating a payment request and redirect to a processing page where user will confirm on phone
            // Replace below with real Vodafone Cash integration
            var callbackUrl = Url.Action("VodafoneCallback", "Payments", new { id = record.Id }, Request.Scheme);

            // Simulated external request payload
            var payload = new
            {
                phone = dto.PhoneNumber,
                amount = amount,
                description = $"DecorMate {dto.Plan} subscription",
                callback = callbackUrl
            };

            // call external provider (placeholder)
            try
            {
                var client = _httpFactory.CreateClient();
                // Example: call your provider endpoint
                var providerUrl = _config["Payments:VodafoneApiEndpoint"]; // configure in appsettings
                if (string.IsNullOrEmpty(providerUrl))
                {
                    // no external provider configured - fall back to simulated flow
                    _logger.LogInformation("Vodafone endpoint is not configured. Using simulated flow.");
                    // redirect to a local "processing" page that shows instructions
                    return RedirectToAction("PaymentProcessing", new { id = record.Id });
                }
                else
                {
                    // Example: POST to provider (adjust headers/auth as required)
                    var resp = await client.PostAsJsonAsync(providerUrl, payload);
                    if (!resp.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Provider returned {Status}", resp.StatusCode);
                        return RedirectToAction("PaymentProcessing", new { id = record.Id });
                    }

                    // provider returns transaction id or redirect url
                    var obj = await resp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
                    if (obj != null && obj.TryGetValue("transactionId", out var tx))
                    {
                        record.TransactionId = tx;
                        await _db.SaveChangesAsync();
                    }

                    // Show processing page
                    return RedirectToAction("PaymentProcessing", new { id = record.Id });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to call payment provider");
                TempData["Error"] = "Failed to start payment. Try again later.";
                return RedirectToAction("Plan");
            }
        }

        // GET /Payments/Processing/{id}
        [HttpGet("/Payments/Processing")]
        public async Task<IActionResult> PaymentProcessing(int id)
        {
            var rec = await _db.PaymentRecords.FindAsync(id);
            if (rec == null) return NotFound();

            // show page with instructions: e.g., "send Vodacash to 012345678" or "check your phone"
            return View("~/Views/Payments/PaymentProcessing.cshtml", rec);
        }

        // GET /Payments/Confirmation/{id}
        [HttpGet("/Payments/Confirmation")]
        public async Task<IActionResult> PaymentConfirmation(int id)
        {
            var rec = await _db.PaymentRecords.FindAsync(id);
            if (rec == null) return NotFound();

            return View("~/Views/Payments/PaymentConfirmation.cshtml", rec);
        }

        // Simulated callback that provider would call to notify completion
        // POST /Payments/Callback/{id}
        [HttpPost("/Payments/Callback/{id}")]
        public async Task<IActionResult> VodafoneCallback(int id, [FromForm] string? status, [FromForm] string? transactionId)
        {
            var rec = await _db.PaymentRecords.FindAsync(id);
            if (rec == null) return NotFound();

            rec.TransactionId = transactionId ?? rec.TransactionId;
            rec.Status = status == "success" ? "Completed" : "Failed";
            if (rec.Status == "Completed") rec.CompletedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            // provider callback may expect simple response
            return Ok();
        }

        // Optional: list user payments (MVC)
        [HttpGet("/Payments/History")]
        [Authorize]
        public async Task<IActionResult> History()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Challenge();

            var list = await _db.PaymentRecords
                .Where(p => p.ApplicationUserId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return View("~/Views/Payments/History.cshtml", list);
        }
    }
}
