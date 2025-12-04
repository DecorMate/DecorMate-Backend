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

// Configure Kestrel to listen on all network interfaces in development
if (builder.Environment.IsDevelopment())
{
    builder.WebHost.ConfigureKestrel(serverOptions =>
    {
        serverOptions.ListenAnyIP(5018); // HTTP
        serverOptions.ListenAnyIP(7247, listenOptions =>
        {
            listenOptions.UseHttps(); // HTTPS
        });
    });
}

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
    options.DefaultScheme = "SmartScheme";
    options.DefaultAuthenticateScheme = "SmartScheme";
    options.DefaultChallengeScheme = "SmartScheme";
})
.AddPolicyScheme("SmartScheme", "Bearer or Cookie", options =>
{
    options.ForwardDefaultSelector = context =>
    {
        var path = context.Request.Path;
        if (path.StartsWithSegments("/api") || path.StartsWithSegments("/chatHub"))
        {
            return JwtBearerDefaults.AuthenticationScheme;
        }
        return IdentityConstants.ApplicationScheme;
    };
})
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
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
    
    // Configure JWT for SignalR WebSocket connections
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // Read the token from the query string for SignalR
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chatHub"))
            {
                context.Token = accessToken;
            }
            
            return Task.CompletedTask;
        }
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
    opts.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
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
builder.Services.AddScoped<DecorMateBackend.Services.Interfaces.IAccountService, DecorMateBackend.Services.AccountService>();
services.AddScoped<EncryptionService>();
services.AddSingleton<CloudinaryService>();
services.AddScoped<ChatService>();
services.AddAutoMapper(typeof(Program));
services.AddControllersWithViews();
services.AddRazorPages();
services.AddEndpointsApiExplorer();
services.AddSwaggerGen();

// ----------------------------



// ----------------------------
// SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
});
builder.Logging.AddFilter("Microsoft.AspNetCore.SignalR", LogLevel.Debug);
builder.Logging.AddFilter("Microsoft.AspNetCore.Http.Connections", LogLevel.Debug);

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

// Enable WebSockets for SignalR
app.UseWebSockets();

app.UseRouting();

if (app.Environment.IsDevelopment())
{
    app.UseCors(policy => policy
        .SetIsOriginAllowed(origin => true) // Allow any origin in development
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
}
else
{
    app.UseCors(policy => policy
        .WithOrigins("http://localhost:5018", "https://localhost:7247", "http://localhost:3000", "https://decormate.runasp.net") // Add production domain if known, keeping existing + potential prod
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
}

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
app.MapHub<DecorMateBackend.Hubs.ChatHub>("/chatHub");

// ----------------------------


app.Run();
