using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QBC.Api.Data;
using QBC.Api.Models;
using QBC.Api.Options;
using QBC.Api.Services;
using QBC.Api.Services.Square;

var builder = WebApplication.CreateBuilder(args);

// ---- Options ----
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<SquareOptions>(builder.Configuration.GetSection(SquareOptions.SectionName));
builder.Services.Configure<FrontendCorsOptions>(builder.Configuration.GetSection(FrontendCorsOptions.SectionName));
builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection(AdminOptions.SectionName));

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
var cors = builder.Configuration.GetSection(FrontendCorsOptions.SectionName).Get<FrontendCorsOptions>() ?? new FrontendCorsOptions();

if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key is missing or too short (need >= 32 chars). Set it via user-secrets or environment variables.");
}

// ---- Database ----
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.")));

// ---- Identity ----
builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

// ---- AuthN / AuthZ (JWT bearer) ----
JwtSecurityTokenHandler.DefaultMapInboundClaims = false;
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });
builder.Services.AddAuthorization();

// ---- Rate limiting ----
// Throttles the unauthenticated auth surface (login/register) per client to
// blunt brute-force, account-enumeration, and signup-spam. Partition key is the
// forwarded client IP when present (we sit behind a proxy in production), else
// the socket IP. This key is only used for coarse throttling — never for auth —
// so a spoofed header at worst widens one attacker's own bucket; credential
// safety still rests on Identity's account lockout.
const string AuthRateLimit = "auth";
// Effectively off under integration tests (many auth calls share one client IP);
// enforced everywhere else.
var authPermitLimit = builder.Environment.IsEnvironment("Testing") ? 100_000 : 10;
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AuthRateLimit, httpContext =>
    {
        var forwarded = httpContext.Request.Headers["X-Forwarded-For"].ToString();
        var clientKey = !string.IsNullOrWhiteSpace(forwarded)
            ? forwarded.Split(',')[0].Trim()
            : httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(clientKey, _ =>
            new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = authPermitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            });
    });
});

// ---- CORS for the SPA ----
const string SpaPolicy = "spa";
builder.Services.AddCors(o => o.AddPolicy(SpaPolicy, p =>
    p.WithOrigins(cors.AllowedOrigins)
     .AllowAnyHeader()
     .AllowAnyMethod()));

// ---- App services ----
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IMembershipService, MembershipService>();
builder.Services.AddScoped<IDayPassService, DayPassService>();

// Typed HTTP client for Square: the owner's access token is attached here,
// server-side only, and never leaves the backend.
builder.Services.AddHttpClient<ISquareGateway, SquareGateway>((sp, client) =>
{
    var opt = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SquareOptions>>().Value;
    client.BaseAddress = new Uri(opt.ApiBaseUrl);
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", opt.AccessToken);
    client.DefaultRequestHeaders.Add("Square-Version", opt.ApiVersion);
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
});

// SignInManager depends on IHttpContextAccessor — register it explicitly.
builder.Services.AddHttpContextAccessor();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ---- Pipeline ----
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// NOTE: TLS is terminated upstream — the frontend (Vercel) proxies requests to
// this backend over HTTP. We must NOT force an in-app HTTPS redirect or HSTS
// here: it would bounce the proxied HTTP request to an HTTPS endpoint this host
// doesn't serve, breaking every request. If this API is ever fronted by its own
// valid TLS certificate, re-enable UseHsts()/UseHttpsRedirection().

app.UseCors(SpaPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Apply EF Core migrations at startup. Create the first one before running:
//   dotnet ef migrations add InitialCreate
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var log = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    try
    {
        // The InMemory provider used by integration tests is not relational and
        // cannot run migrations — it materializes the schema on demand instead.
        if (db.Database.IsRelational())
            db.Database.Migrate();
    }
    catch (Exception ex)
    {
        log.LogError(ex,
            "Database migration failed. Ensure SQL Server is reachable and that an initial " +
            "migration exists (dotnet ef migrations add InitialCreate).");
        throw;
    }

    // Ensure the Admin role exists and grant it to the configured owner email(s),
    // so the customer CRM is reachable by the gym owner.
    await SeedAdminsAsync(scope.ServiceProvider, log);
}

app.Run();

static async Task SeedAdminsAsync(IServiceProvider sp, ILogger log)
{
    var admin = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AdminOptions>>().Value;
    var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
    var users = sp.GetRequiredService<UserManager<ApplicationUser>>();

    if (!await roles.RoleExistsAsync(AdminOptions.RoleName))
        await roles.CreateAsync(new IdentityRole(AdminOptions.RoleName));

    foreach (var email in admin.Emails.Where(e => !string.IsNullOrWhiteSpace(e)))
    {
        var user = await users.FindByEmailAsync(email.Trim());
        if (user is null)
        {
            log.LogInformation("Admin email {Email} has no account yet; will be promoted once registered.", email);
            continue;
        }
        if (!await users.IsInRoleAsync(user, AdminOptions.RoleName))
            await users.AddToRoleAsync(user, AdminOptions.RoleName);
    }
}

// Exposes the implicit Program class to the integration test project
// (WebApplicationFactory<Program>). Must sit at the end — after all top-level
// statements. No effect on the running app.
public partial class Program { }
