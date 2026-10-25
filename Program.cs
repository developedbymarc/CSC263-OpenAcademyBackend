using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenAcademy.Configuration;
using OpenAcademy.Constants;
using OpenAcademy.Data;
using OpenAcademy.Entities.Users;
using OpenAcademy.Startup;
using Scalar.AspNetCore;

DotNetEnv.Env.Load();
var builder = WebApplication.CreateBuilder(args);

// Add extra configuration providers (vaults, etc.) here, BEFORE Build()
builder.Services.AddAppConfiguration();

// NOTE: AddAppDataProtection must come AFTER any extra config providers
builder.Services.AddAppDataProtection(builder.Configuration);

// --- CSRF ---
builder.Services.AddAntiforgery();
builder.Services
    .AddOptions<AntiforgeryOptions>()
    .Configure<IOptions<CsrfOptions>>((af, csrf) =>
    {
        af.HeaderName = CsrfOptions.HeaderName;
        af.Cookie.Name = csrf.Value.CookieName;
        af.Cookie.HttpOnly = CsrfOptions.HttpOnly;
        af.Cookie.SecurePolicy = CsrfOptions.SecurePolicy;
        af.Cookie.SameSite = CsrfOptions.SameSite;
        af.Cookie.Path = CsrfOptions.Path;
    });

// --- CORS ---
builder.Services.AddCors();
builder.Services
    .AddOptions<CorsOptions>()
    .Configure<IOptions<FrontendOptions>>((cors, frontend) =>
        cors.AddDefaultPolicy(p => p
            .WithOrigins(frontend.Value.Origin)
            .AllowCredentials()
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
            .WithHeaders(CsrfOptions.HeaderName, "Content-Type")));

// --- Controllers ---
builder.Services
    .AddControllers(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()))
    .AddJsonOptions(options =>
    {
        // Accepts both names and numbers when reading (e.g. "Instructor" / 0), writes names
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// --- Database ---
builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
{
    var db = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;

    options.UseNpgsql(db.ConnectionString, npgsql =>
    {
        npgsql.EnableRetryOnFailure(
            maxRetryCount: db.MaxRetryCount,
            maxRetryDelay: db.MaxRetryDelay,
            errorCodesToAdd: null);
        npgsql.CommandTimeout(db.CommandTimeoutInSeconds);
    });
});

// --- Identity ---
builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.User.AllowedUserNameCharacters = Characters.Lowercase + Characters.Uppercase + Characters.Digits + "-._";

        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireDigit = true;
        options.Password.RequiredUniqueChars = 1;

        options.SignIn.RequireConfirmedEmail = true;

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(30);
    })
    .AddRoles<ApplicationRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
{
    options.TokenLifespan = TimeSpan.FromHours(1);
});

// --- JWT authentication ---
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
    {
        var jwtOptions = jwt.Value;

        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            
            ValidAlgorithms = [jwtOptions.Algorithm],
            ValidateLifetime = true,
            ClockSkew = jwtOptions.ClockSkew,
        };

        bearer.Events = new JwtBearerEvents
        {
            // A null token falls through to the Authorization header
            OnMessageReceived = ctx =>
            {
                ctx.Token = ctx.Request.Cookies[jwtOptions.CookieName];
                return Task.CompletedTask;
            },
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddOpenApi();

builder.Services.AddScoped<RootUserBootstrapper>();

var app = builder.Build();
app.Services.GetRequiredService<IStartupValidator>().Validate();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    // seeding roles
    var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();

    var startupLogger = services.GetRequiredService<ILogger<Program>>();
    foreach (var roleName in RoleNames.All)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            var result = await roleManager.CreateAsync(new ApplicationRole(roleName));
            if (!result.Succeeded)
                startupLogger.LogCritical("Failed to seed role {Role}: {Errors}", roleName,
                    string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }
    
    // bootstrapping root
    if (args.Contains("bootstrap-root"))
    {
        Environment.ExitCode = await BootstrapRootCommand.RunAsync(services);
        return;   // exits without starting the web server
    }
    
    var bootstrapper = services.GetRequiredService<RootUserBootstrapper>();
    if (!await bootstrapper.RootExistsAsync())
    {
        startupLogger.LogWarning(
            "No root user exists. Create one with: podman compose run --rm api bootstrap-root");
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
