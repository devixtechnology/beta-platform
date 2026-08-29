using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Authorization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using BetaPlatform.Data;
using BetaPlatform.Data.Entities;
using BetaPlatform.Services;
using BetaPlatform.Services.Api;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ---- EF Core (Pomelo MySQL) ----
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var serverVersion = new MySqlServerVersion(new Version(8, 0, 35));
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, serverVersion));

// ---- ASP.NET Core Identity (Admin + Client roles — 004 contracts/user-management.md) ----
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// A deactivated account must lose access on its next request, not at its next sign-out. Rotating
// the security stamp invalidates issued cookies; this interval is how often they are re-checked
// against the store (research T4).
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.FromMinutes(1);
});

// ---- Bearer tokens for the integration API (005 research R1/R4) ----
// Registered as an ADDITIONAL, NON-DEFAULT scheme. AddIdentity above has already set the default
// authenticate/challenge schemes to cookies; calling AddAuthentication(JwtBearerDefaults...) here
// would re-point them and break every browser screen. Each API controller names this scheme
// explicitly, which is what makes an unauthenticated API call answer a bare 401 instead of a 302
// to /Auth/Login.
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
var jwtConfigurationError = jwtOptions.Validate(builder.Environment.IsProduction());
if (jwtConfigurationError is not null)
{
    // Fail loudly at startup rather than puzzlingly at first sign-in. Whoever holds this key mints
    // tokens for any account and any role, so an unusable or placeholder key is not a warning.
    throw new InvalidOperationException(jwtConfigurationError);
}

builder.Services.AddAuthentication().AddJwtBearer(options =>
{
    // What is issued is exactly what arrives back: short claim names, no inbound remapping.
    options.MapInboundClaims = false;
    options.Events = JwtBearerEventHandlers.Create();
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtOptions.Issuer,
        ValidateAudience = true,
        ValidAudience = jwtOptions.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
        ValidateLifetime = true,
        // Default skew is five minutes, which would silently make an 8-hour token 8h05m and
        // FR-002 untestable to the minute.
        ClockSkew = TimeSpan.Zero,
        NameClaimType = ApiClaimTypes.Name,
        RoleClaimType = ApiClaimTypes.Role
    };
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Auth/Login";
    options.LogoutPath = "/Auth/Logout";
    // A refused client must see a refusal, not a login form on an account they are already using.
    options.AccessDeniedPath = "/Auth/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    // Stated rather than inherited: the unattended production display polls every 5 s, so its
    // session is renewed continuously and cannot idle out while the screen is live (research T3).
    options.SlidingExpiration = true;
});

// ---- Localization (bilingual AR/EN + RTL — FR-062) ----
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// ---- Telemetry options (Telemetry:StaleAfterMinutes — research D4) ----
builder.Services.Configure<TelemetryOptions>(
    builder.Configuration.GetSection(TelemetryOptions.SectionName));

// ---- Application services (DI — constitution Principle II) ----
builder.Services.AddScoped<IMachineStatusService, MachineStatusService>();
builder.Services.AddScoped<IMachineService, MachineService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IWorkOrderService, WorkOrderService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IUserAdminService, UserAdminService>();

// ---- Integration API services (005) ----
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
// Contract-first slice: these return representative data and persist nothing. The behaviour
// slice swaps them for implementations delegating to IProductService/IWorkOrderService, and
// changes nothing else (research R7).
builder.Services.AddScoped<IProductApiService, SampleProductApiService>();
builder.Services.AddScoped<IWorkOrderApiService, SampleWorkOrderApiService>();

// ---- MVC with a global authorization requirement (FR-001) ----
builder.Services.AddControllersWithViews(options =>
    {
        // Name validation errors by the JSON field the caller actually sent ("productCode"), not by
        // the CLR property ("ProductCode"). Without this the errors dictionary disagrees with the
        // request body it is complaining about, and a client keying off the field name breaks.
        options.ModelMetadataDetailsProviders.Add(
            new SystemTextJsonValidationMetadataProvider(JsonNamingPolicy.CamelCase));
    })
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

// One error shape for the API surface, and no diagnostics in it (FR-030/FR-031, research R5).
builder.Services.AddProblemDetails();

// Machine-readable contract at /openapi/v1.json (FR-032, research R6).
builder.Services.AddOpenApi(options =>
    options.AddDocumentTransformer<OpenApiDocumentTransformer>());

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

var supportedCultures = new[] { new CultureInfo("en"), new CultureInfo("ar") };
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("en");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.RequestCultureProviders = new List<IRequestCultureProvider>
    {
        new CookieRequestCultureProvider()
    };
});

var app = builder.Build();

// An unhandled fault under /api answers with a bare ProblemDetails, never the MVC error view and
// never a stack trace (FR-031). Branched so the browser screens keep the behaviour they have today.
app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/api"),
    apiBranch => apiBranch.UseExceptionHandler());

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseRequestLocalization(app.Services.GetRequiredService<
    Microsoft.Extensions.Options.IOptions<RequestLocalizationOptions>>().Value);

app.UseAuthentication();
app.UseAuthorization();

// The published contract (FR-032). Anonymous by design: the global fallback policy would
// otherwise require a sign-in to read a document that carries only endpoint shapes, no data.
app.MapOpenApi().AllowAnonymous();

// Interactive API reference over that same document. Anonymous for the same reason: the page only
// renders the contract and can call nothing without a token the reader has signed in for. It adds
// no second source of truth — change an action and both the document and this page follow.
app.MapScalarApiReference("/docs", options =>
{
    options
        .WithTitle("Beta Platform Integration API")
        .WithOpenApiRoutePattern("/openapi/{documentName}.json")
        .AddPreferredSecuritySchemes("bearerAuth");
}).AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

// Apply migrations + seed admin/role on startup.
using (var scope = app.Services.CreateScope())
{
    await DbSeeder.SeedAsync(scope.ServiceProvider, app.Configuration, app.Environment);
}

app.Run();
