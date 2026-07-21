using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QBC.Api.Data;
using QBC.Api.Domain;
using QBC.Api.Options;
using QBC.Api.Services;
using QBC.Api.Services.Square;

var builder = WebApplication.CreateBuilder(args);

// ---- Options ----
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<SquareOptions>(builder.Configuration.GetSection(SquareOptions.SectionName));
builder.Services.Configure<FrontendCorsOptions>(builder.Configuration.GetSection(FrontendCorsOptions.SectionName));

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

// ---- CORS for the SPA ----
const string SpaPolicy = "spa";
builder.Services.AddCors(o => o.AddPolicy(SpaPolicy, p =>
    p.WithOrigins(cors.AllowedOrigins)
     .AllowAnyHeader()
     .AllowAnyMethod()));

// ---- App services ----
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IMembershipService, MembershipService>();

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
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors(SpaPolicy);
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
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        log.LogError(ex,
            "Database migration failed. Ensure SQL Server is reachable and that an initial " +
            "migration exists (dotnet ef migrations add InitialCreate).");
        throw;
    }
}

app.Run();
