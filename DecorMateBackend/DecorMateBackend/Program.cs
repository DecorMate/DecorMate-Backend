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
using System.IdentityModel.Tokens.Jwt;

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
// Authentication: Cookies (Identity) + JwtBearer (API) + External providers
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
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = ctx =>
        {
            var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(ctx.Exception, "JWT authentication failed: {msg}", ctx.Exception.Message);
            return Task.CompletedTask;
        },
        OnTokenValidated = ctx =>
        {
            var logger = ctx.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Token validated for {sub}", ctx.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value);
            return Task.CompletedTask;
        }
    };
    })
// Google + Facebook (keep chaining to same AuthenticationBuilder)
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
services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddSingleton<CloudinaryService>();
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

    // Better JWT scheme for Swagger UI
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "Enter 'Bearer {token}'",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
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

// Swagger in dev or always (you already enabled)
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

// Authentication & Authorization (order matters)
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();
app.MapControllers();

app.Run();
