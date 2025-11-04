using System.Text;
using DecorMate_Backend_Web_app.Data;
using DecorMate_Backend_Web_app.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using DecorMate_Backend_Web_app.Services;
using DecorMateBackend.Services;
using DecorMateBackend.Models;
using DecorMateBackend.Repositories;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;
var services = builder.Services;

// ----------------------------
// Bind settings
services.Configure<SmtpSettings>(configuration.GetSection("Smtp"));
services.Configure<JwtSettings>(configuration.GetSection("Jwt"));

// ----------------------------
// DbContext
services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));
// ----------------------------
// Register repository services

services.AddScoped <IUnitOfWork , UnitOfWork>();
services.AddScoped <IUserRepository, UserRepository>();
services.AddScoped <IRefreshTokenRepository , RefreshTokenRepository>();
services.AddScoped <IImageRepository , ImageRepository>();
services.AddScoped <IVendorRepository , VendorRepository>();
// ----------------------------
// Identity
services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = true;

    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// ----------------------------
// JWT settings read
var jwtSection = configuration.GetSection("Jwt");
var jwtKey = jwtSection.GetValue<string>("Key") ?? throw new Exception("Jwt:Key is missing in configuration");
var jwtIssuer = jwtSection.GetValue<string>("Issuer") ?? "DecorMate";
var jwtAudience = jwtSection.GetValue<string>("Audience") ?? "DecorMateClients";

// ----------------------------
// Authentication
services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
    options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
})
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.RequireHttpsMetadata = true;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.FromSeconds(30),
        RoleClaimType = ClaimTypes.Role,
        NameClaimType = ClaimTypes.NameIdentifier
    };
})
.AddGoogle(googleOptions =>
{
    googleOptions.ClientId = configuration["Authentication:Google:ClientId"];
    googleOptions.ClientSecret = configuration["Authentication:Google:ClientSecret"];
})
.AddFacebook(fb =>
{
    fb.AppId = configuration["Authentication:Facebook:AppId"];
    fb.AppSecret = configuration["Authentication:Facebook:AppSecret"];
});

// ----------------------------
// Cookie options
services.ConfigureApplicationCookie(opts =>
{
    opts.LoginPath = "/Auth/Login";
    opts.LogoutPath = "/Auth/Logout";
});

// ----------------------------
// Authorization
services.AddAuthorization(options =>
{
    options.AddPolicy("CompanyOnly", policy =>
    {
        policy.AddAuthenticationSchemes(IdentityConstants.ApplicationScheme);
        policy.RequireRole("Company");
    });

    options.AddPolicy("MobileUserOnly", policy =>
    {
        policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
        policy.RequireRole("User");
    });
});

// ----------------------------
// Other services
services.AddHttpClient();
services.AddScoped<JwtService>();
builder.Services.Configure<DecorMateBackend.Models.SmtpSettings>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddTransient<DecorMateBackend.Services.SmtpEmailSender>();
builder.Services.AddTransient<DecorMateBackend.Models.IEmailSenderO>(sp => sp.GetRequiredService<DecorMateBackend.Services.SmtpEmailSender>());
builder.Services.AddScoped<DecorMateBackend.Services.EmailService>();
services.AddSingleton<CloudinaryService>();
services.AddControllersWithViews();
services.AddRazorPages();
services.AddEndpointsApiExplorer();
services.AddSwaggerGen();

// ----------------------------
// Build app
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var service = scope.ServiceProvider;
    var logger = service.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = service.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        var roleManager = service.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = service.GetRequiredService<UserManager<ApplicationUser>>();

        var roles = new[] { "User", "Company", "Admin" };
        foreach (var r in roles)
        {
            if (!await roleManager.RoleExistsAsync(r))
                await roleManager.CreateAsync(new IdentityRole(r));
        }

        var seedEmail = configuration["Seed:CompanyEmail"];
        var seedPass = configuration["Seed:CompanyPassword"];
        if (!string.IsNullOrEmpty(seedEmail) && !string.IsNullOrEmpty(seedPass))
        {
            var existing = await userManager.FindByEmailAsync(seedEmail);
            if (existing == null)
            {
                var cmp = new ApplicationUser
                {
                    UserName = seedEmail,
                    Email = seedEmail,
                    EmailConfirmed = true,
                    FirstName = "Company",
                    LastName = "Admin"
                };
                var res = await userManager.CreateAsync(cmp, seedPass);
                if (res.Succeeded) await userManager.AddToRoleAsync(cmp, "Company");
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error while migrating or seeding database on startup.");
    }
}

// ----------------------------
// Swagger
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "DecorMate API V1");
    c.RoutePrefix = "swagger";
});

// ----------------------------
// Middleware pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseCors(policy => policy
    .AllowAnyOrigin()
    .AllowAnyHeader()
    .AllowAnyMethod());

app.UseAuthentication();
app.UseAuthorization();

// ----------------------------
// Default route → Home/Index
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

app.MapRazorPages();
app.MapControllers();

app.Run();
