using System.Text;
using DecorMate_Backend_Web_app.Data;
using DecorMate_Backend_Web_app.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using DecorMate_Backend_Web_app.Services;

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
// Authentication: Cookies (Identity) + JwtBearer (API)
services.AddAuthentication(options =>
{
    // Keep cookie as default for MVC (Identity)
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
    options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
})
// Identity cookie behavior (Identity already registers cookie for ApplicationScheme)
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

        // important: tell the system where to read role claims from
        RoleClaimType = ClaimTypes.Role,   
        NameClaimType = ClaimTypes.NameIdentifier
    };
});

// External providers (Google/Facebook) - optional
services.AddAuthentication()
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
// Configure the Identity application cookie options
services.ConfigureApplicationCookie(opts =>
{
    opts.LoginPath = "/Auth/Login";
    opts.LogoutPath = "/Auth/Logout";
    // opts.ExpireTimeSpan = TimeSpan.FromDays(14);
});

// ----------------------------
// Authorization policies
services.AddAuthorization(options =>
{
    // Pages protected for companies (use Cookie auth + Company role)
    options.AddPolicy("CompanyOnly", policy =>
    {
        policy.AddAuthenticationSchemes(IdentityConstants.ApplicationScheme);
        policy.RequireRole("Company");
    });

    // API endpoints for mobile users (JWT + User role)
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
// Register concrete email sender (MailKit implementation)
services.AddScoped<IEmailSender, SmtpEmailSender>();

services.AddControllersWithViews();
services.AddRazorPages();
services.AddEndpointsApiExplorer();
services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "DecorMate API",
        Version = "v1",
        Description = "API documentation for Auth and other endpoints"
    });

    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Please insert JWT with Bearer into field",
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] { }
        }
    });
});

// Build app
var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "DecorMate API V1");
    c.RoutePrefix = "swagger";
});
// ----------------------------
// Seed roles (and optional seed user) at startup
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    var roles = new[] { "User", "Company", "Admin" };
    foreach (var r in roles)
    {
        if (!await roleManager.RoleExistsAsync(r))
            await roleManager.CreateAsync(new IdentityRole(r));
    }

    // Optional: seed a company account (configure in appsettings - Seed section)
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
                FirstName = "Company",
                LastName = "Admin",
                EmailConfirmed = true
            };
            var res = await userManager.CreateAsync(cmp, seedPass);
            if (res.Succeeded)
            {
                await userManager.AddToRoleAsync(cmp, "Company");
            }
        }
    }
}

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

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();
app.MapControllers();

app.Run();
